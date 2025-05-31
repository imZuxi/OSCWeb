using System;
using System.Threading.Tasks;
using Newtonsoft.Json;
using OSC2Web.Sterlizable;
using WebSocketSharp;
using static OSC2Web.Sterlizable.JsonContractResolvers;

namespace OSC2Web
{
    internal static class WebSocketManager
    {
        static WebSocket webSocket;
        private static int reconnectAttempts = 0;
        private static bool shuttingDown = false;
        private const int maxReconnectDelay = 60; // seconds

        internal static void CreateSocket()
        {
            KeepAliveTimer.Elapsed += (s, e) => { SendMessage("KeepAlive", Config.Instance.connectionId); };
            InitSocket();
        }

        private static void InitSocket()
        {
            webSocket = new WebSocket(Config.Instance.connectionUrl);

            webSocket.OnOpen += (sender, e) =>
            {
                Console.WriteLine("WebSocket connection opened.");
                reconnectAttempts = 0;

                if (!string.IsNullOrEmpty(Config.Instance.connectionId))
                    SendMessage("restoreSession", Config.Instance.connectionId);
                else
                    SendMessage("createSession", "");

                KeepAliveTimer.Start();
            };

            webSocket.OnMessage += (sender, e) =>
            {
                OnMessageRecv(e.Data);
            };

            webSocket.OnError += (sender, e) =>
            {
                Console.WriteLine($"WebSocket error: {e.Message}");
            };

            webSocket.OnClose += (sender, e) =>
            {
                Console.WriteLine($"WebSocket closed: {e.Reason}");

                KeepAliveTimer.Stop();

                if (!shuttingDown)
                    Reconnect();
            };

            try
            {
                webSocket.Connect();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to connect: {ex.Message}");
                Reconnect();
            }
        }

        private static async void Reconnect()
        {
            reconnectAttempts++;
            int delay = Math.Min(reconnectAttempts * 5, maxReconnectDelay); // 5s, 10s, 15s... max 60s

            Console.WriteLine($"Reconnecting in {delay} seconds...");
            await Task.Delay(TimeSpan.FromSeconds(delay));

            if (!shuttingDown)
                InitSocket();
        }

        internal static void Shutdown()
        {
            shuttingDown = true;
            KeepAliveTimer.Stop();
            if (webSocket != null)
            {
                if (webSocket.IsAlive)
                    webSocket.Close();
                webSocket = null;
            }
        }

        internal static void SendMessage(string type, object Message)
        {
            WebsocketMessage message = new Sterlizable.WebsocketMessage()
            {
                MessageType = type.ToLower(),
                Data = Message
            };
            string smess = JsonConvert.SerializeObject(message);
            if (Config.Instance.debug)
            Console.WriteLine(smess);
            SendMessage(smess);
        }

        internal static void SendMessage(string message)
        {
            if (webSocket?.IsAlive == true)
                webSocket.Send(message);
        }

        internal static void OnMessageRecv(string message)
        {
            if (string.IsNullOrEmpty(message))
                return;
            if (Config.Instance.debug)
            Console.WriteLine(message);
            WebsocketMessage wsMessage = JsonConvert.DeserializeObject<WebsocketMessage>(message);
            switch (wsMessage.MessageType.ToLower())
            {
                case "connectionaccepted":
                    Console.WriteLine("Connection accepted!");
                    Console.WriteLine(wsMessage.Data.ToString());
                    break;
                case "session_created":
                    Console.WriteLine("Session created:");
                    Config.Instance.connectionId = (string)wsMessage.Data;
                    Config.Instance.Save();
                    break;
                case "control":
                    Program.OnChangeRequested(JsonConvert.DeserializeObject<ChangeParam>(wsMessage.Data.ToString()));
                    break;
                case "error":
                    Console.WriteLine("Error from server:");
                    Console.WriteLine(wsMessage.Data.ToString());
                    break;
                case "ping":
                    Console.WriteLine("Error from server:");
                    Console.WriteLine(wsMessage.Data.ToString());
                    break;

                default:
                   // Console.WriteLine($"Unhandled message type: {wsMessage.MessageType}");
                  //  Console.WriteLine(wsMessage.Data.ToString());
                    break;
            }
        }

        internal static void UpdateParams(this AvatarConfig value)
        {
            var settings = new JsonSerializerSettings
            {
                ContractResolver = new StripInputOutputContractResolver(),
                Formatting = Formatting.Indented
            };

            try
            {
                WebsocketMessage message = new Sterlizable.WebsocketMessage()
                {
                    MessageType = "updateparams",
                    Data = value
                };
            
                SendMessage(JsonConvert.SerializeObject(message, settings));
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        private static System.Timers.Timer KeepAliveTimer = new System.Timers.Timer(TimeSpan.FromSeconds(30));
    }
}
