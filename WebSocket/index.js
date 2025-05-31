const WebSocket = require('ws');
const { v4: uuidv4 } = require('uuid');
require('dotenv').config({ path: '../API/.env' });
const { GetRedisClient, SetRedisKeyEXAsync } = require('../API/helpers/redis'); // custom redis handler;
const redis = GetRedisClient();
redis.on('error', (err) => console.error('Redis error:', err));
const wss = new WebSocket.Server({ port: 1284 });

const SESSION_TTL_SECONDS = 60 * 60 * 24 * 7; // 1 week

const activeSessions = new Map();
const sessionListeners = new Map();
function generateSessionId() {
    return `link_${uuidv4()}`;
}

function sendMessage(ws, type, data) {
    ws.send(JSON.stringify({
        type: type,
        data: data
    }));
}

function GetRedisKeyId(connectionId) {
    return "projects:osc:" + connectionId;
}

wss.on('connection', (ws) => {
    console.log("New connection established.");
    ws.sessionId = null;

    ws.on('message', async (message) => {

        let parsed;
        try {
            parsed = JSON.parse(message);
            console.log(parsed);
        } catch (err) {
            console.error("Failed to parse message:", err);
            return;
        }

        const type = parsed.type?.toLowerCase();
        const data = parsed.data;

        switch (type) {
            case "createsession": {
                const sessionId = generateSessionId();
                const shortcode = crc32(sessionId);
                ws.sessionId = sessionId;
                activeSessions.set(sessionId, ws);
                await redis.setEx(GetRedisKeyId(sessionId), SESSION_TTL_SECONDS, JSON.stringify({ params: {} }));
                await redis.setEx(`privatevar:datahost:${shortcode}`, SESSION_TTL_SECONDS, JSON.stringify({ type: "url", isShort: 1, filename: `https://control.cute.bet/osc/${sessionId}` }));
                sendMessage(ws, "session_created", sessionId);
                sendMessage(ws, "connectionAccepted", `https://cute.bet/s/${shortcode}`);
                break;
            }

            case "restoresession": {
                const sessionId = data;
                ws.sessionId = sessionId;
                activeSessions.set(sessionId, ws);
                const redisKey = GetRedisKeyId(sessionId);
                let shortcode = crc32(sessionId);
                let stored = await redis.get(redisKey);
                if (!stored) {
                    await redis.setEx(redisKey, SESSION_TTL_SECONDS, JSON.stringify({ params: {} }));
                   // await redis.setEx(`privatevar:datahost:${shortcode}`, SESSION_TTL_SECONDS, JSON.stringify({ type: "url", isShort: 1, filename: `https://control.cute.bet/osc/${sessionId}` }));
                }
                await redis.setEx(`privatevar:datahost:${shortcode}`, SESSION_TTL_SECONDS, JSON.stringify({ type: "url", isShort: 1, filename: `https://control.cute.bet/osc/${sessionId}` }));
                sendMessage(ws, "connectionAccepted", `https://cute.bet/s/${shortcode}`);
                break;
            }

            case "updateparams": {
                if (!ws.sessionId) {
                    sendMessage(ws, "error", "No active session.");
                    break;
                }

                const redisKey = GetRedisKeyId(ws.sessionId);
                await redis.setEx(redisKey, SESSION_TTL_SECONDS, JSON.stringify(data));
                sendMessage(ws, "params_updated", data);

                // Notify listeners
                const listeners = sessionListeners.get(ws.sessionId);
                if (listeners) {
                    const redisKey = GetRedisKeyId(ws.sessionId);
                    let stored = await redis.get(redisKey);
                    for (const listenerWs of listeners) {
                        if (listenerWs !== ws && listenerWs.readyState === WebSocket.OPEN) {
                            listenerWs.send(JSON.stringify({ type: "updateparams", data: JSON.parse(stored) }))
                        }
                    }
                }

                break;
            }


            case "control": {
                const targetSessionId = parsed.control;
                const command = data?.command;
                const targetWs = activeSessions.get(targetSessionId);

                if (targetWs && targetWs.readyState === WebSocket.OPEN) {

                    //targetWs.send(JSON.stringify({type:"control", data: data }))
                    sendMessage(targetWs, "control", data);
                    sendMessage(ws, "control", `Sent control command to ${targetSessionId}`);
                } else {
                    sendMessage(ws, "error", "Target session not found or disconnected.");
                }
                break;
            }

            case "getparams": {
                console.log("Active Sessions:", Array.from(activeSessions.keys()));
                const targetSessionId = parsed.control;
                console.log("Requested session:", targetSessionId);
                if (!sessionListeners.has(targetSessionId)) {
                    sessionListeners.set(targetSessionId, new Set());
                }
                sessionListeners.get(targetSessionId).add(ws);
                console.log(`Subscribed client to future updates for ${targetSessionId}`);
                const targetWs = activeSessions.get(targetSessionId);
                if (targetWs && targetWs.readyState === WebSocket.OPEN) {
                    const redisKey = GetRedisKeyId(targetSessionId);
                    let stored = await redis.get(redisKey);
                    sendMessage(ws, "updateparams", JSON.parse(stored));

                    // Register this ws as a listener for future updates to this session

                } else {
                    sendMessage(ws, "error", "Target session not found or disconnected.");
                }
                break;
            }

            case "keepalive": {
                sendMessage(ws, "pong", "Still alive.");
                break;
            }

            default: {
                sendMessage(ws, "error", "Unknown message type.");
                break;
            }
        }
    });

    ws.on('close', () => {
        if (ws.sessionId) {
            activeSessions.delete(ws.sessionId);
            console.log(`Session ${ws.sessionId} disconnected.`);
        }
    });
});

console.log("WebSocket server listening on ws://localhost:1284");



function crc32(str) {
  const table = new Uint32Array(256);
  for (let i = 0; i < 256; i++) {
    let c = i;
    for (let k = 0; k < 8; k++) {
      c = c & 1 ? 0xEDB88320 ^ (c >>> 1) : c >>> 1;
    }
    table[i] = c;
  }

  let crc = 0xFFFFFFFF;
  for (let i = 0; i < str.length; i++) {
    const byte = str.charCodeAt(i);
    crc = table[(crc ^ byte) & 0xFF] ^ (crc >>> 8);
  }

  return (crc ^ 0xFFFFFFFF) >>> 0; // Unsigned 32-bit integer
}