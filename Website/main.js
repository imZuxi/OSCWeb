let paramStates = {};

let currentAvatarId = null;


const controlId = window.location.pathname.split("/").pop();
const socket = new WebSocket("wss://control.cute.bet/ws");
socket.addEventListener('open', () => {
  console.log('WebSocket connected');
  document.getElementById('status').innerHTML = `Connected.`;

  // Request parameters for this control
  socket.send(JSON.stringify({
    type: 'getparams',
    control: controlId
  }));

  // Keepalive
  setInterval(() => {
    if (socket.readyState === WebSocket.OPEN) {
      socket.send(JSON.stringify({ type: 'keepalive' }));
    }
  }, 5000);
});
function sendValue(name, value) {
  fetch('https://control.cute.bet/params', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ name, value })
  }).catch(console.error);
}

function normalizeValue(val) {
  if (typeof val === 'string') {
    const lower = val.toLowerCase();
    if (lower === 'true') return true;
    if (lower === 'false') return false;
    if (!isNaN(lower)) return parseFloat(lower);
  }
  return typeof val === 'boolean' ? val : parseFloat(val);
}

function formatDisplayName(name) {
  const parts = name.split('_');
  return parts.length > 1 ? parts.slice(1).join('_') : name;
}

function createControl(name, value) {
  const paramDiv = document.createElement('div');
  paramDiv.className = 'param';
  paramDiv.id = `param-${name}`;

  const label = document.createElement('span');
  label.textContent = formatDisplayName(name);

  const val = normalizeValue(value);

  if (typeof val === 'boolean') {
    const toggle = document.createElement('div');
    toggle.className = 'toggle' + (val ? ' active' : '');
    toggle.onclick = () => {
      const newVal = !paramStates[name];
      paramStates[name] = newVal;
      toggle.classList.toggle('active', newVal);
      sendValue(name, newVal); // Send on toggle click
    };
    paramDiv.appendChild(label);
    paramDiv.appendChild(toggle);
  }

  else if (typeof val === 'number' && !isNaN(val)) {
    const sliderWrapper = document.createElement('div');
    sliderWrapper.className = 'slider-wrapper';

    const slider = document.createElement('input');
    slider.type = 'range';
    slider.step = 1;
    slider.min = 0;
    slider.max = 100;

    // Convert actual value to percentage
    let percent = Math.round(val * 100);
    if (percent > 100) percent = 100;
    slider.value = percent;

    const valueLabel = document.createElement('span');
    valueLabel.className = 'slider-value';
    valueLabel.textContent = percent + "%";

    // Update the slider value on input (UI update)
    slider.addEventListener('input', () => {
      const pct = parseInt(slider.value);
      const actual = pct / 100;
      paramStates[name] = actual;
      valueLabel.textContent = pct + "%";
    });

    // Send the value only when mouse is released (mouseup)
    slider.addEventListener('mouseup', () => {
      const pct = parseInt(slider.value);
      const actual = pct / 100;
      sendValue(name, actual); // Send to server on mouseup
    });

    sliderWrapper.appendChild(slider);
    sliderWrapper.appendChild(valueLabel);

    paramDiv.appendChild(label);
    paramDiv.appendChild(sliderWrapper);
  }

  return paramDiv;
}

function updateParams(newParams) {
  const container = document.getElementById('params');
  if (!container) return;

  container.innerHTML = '';
  paramStates = newParams;

  const toggles = [];
  const sliders = [];

  for (const [name, rawVal] of Object.entries(newParams)) {
    const val = normalizeValue(rawVal);
    if (typeof val === 'boolean') {
      toggles.push({ name, val });
    } else if (typeof val === 'number' && !isNaN(val)) {
      sliders.push({ name, val });
    }
  }

  // Append toggles first
  for (const { name, val } of toggles) {
    const el = createControl(name, val);
    if (el) container.appendChild(el);
  }

  // Append sliders last
  for (const { name, val } of sliders) {
    const el = createControl(name, val);
    if (el) container.appendChild(el);
  }
}

function updateParamsFromApi(apiData) {
  if (!apiData.parameters || !Array.isArray(apiData.parameters)) return;

  const parsedParams = {};
  for (const p of apiData.parameters) {
    if (typeof p.name === 'string') {
      parsedParams[p.name] = normalizeValue(p.Value);
    }
  }

  updateParams(parsedParams);
}

function sendValue(name, value) {
  if (socket.readyState !== WebSocket.OPEN) return;

  socket.send(JSON.stringify({
    type: 'control',
    control: controlId,
    data: 
      { name, value: value }

  }));
}

// Handle incoming WebSocket messages
socket.onmessage = event => {
  try {
    const message = JSON.parse(event.data);
    if (message.type === "updateparams" && message.data?.parameters) {
      const { avatarId, parameters } = message.data;

      // Full UI rebuild if avatarId changed
      if (currentAvatarId !== avatarId) {
        currentAvatarId = avatarId;

        const updated = {};
        for (const param of parameters) {
          if (param.name && param.Value !== undefined) {
            updated[param.name] = normalizeValue(param.Value);
          }
        }
        updateParams(updated);
        return;
      }

      // Incremental updates
      for (const param of parameters) {
        const name = param.name;
        const newVal = normalizeValue(param.Value);
        const oldVal = paramStates[name];

        if (newVal !== oldVal) {
          paramStates[name] = newVal;

          const el = document.getElementById(`param-${name}`);
          if (!el) continue;

          if (typeof newVal === 'boolean') {
            const toggle = el.querySelector('.toggle');
            if (toggle) toggle.classList.toggle('active', newVal);
          }

          if (typeof newVal === 'number') {
            const slider = el.querySelector('input[type="range"]');
            const label = el.querySelector('.slider-value');
            const percent = Math.round(newVal * 100);
            if (slider) slider.value = percent;
            if (label) label.textContent = percent + "%";
          }
        }
      }
    }
  } catch (err) {
    console.error("Invalid WebSocket message:", err);
  }
};