const STORAGE_KEY = "vaultlite_key";

const keyInput = document.getElementById("key");
const rememberCheckbox = document.getElementById("remember");
const inputArea = document.getElementById("input");
const outputArea = document.getElementById("output");
const statusEl = document.getElementById("status");
const generateBtn = document.getElementById("generate-key");
const encryptBtn = document.getElementById("encrypt");
const decryptBtn = document.getElementById("decrypt");
const copyBtn = document.getElementById("copy");

const actionButtons = [generateBtn, encryptBtn, decryptBtn];

const savedKey = localStorage.getItem(STORAGE_KEY);
if (savedKey) {
  keyInput.value = savedKey;
  rememberCheckbox.checked = true;
}

function setStatus(message, type) {
  statusEl.textContent = message || "";
  statusEl.className = "mt-3 small" + (type ? " text-" + type : "");
}

function setLoading(isLoading) {
  actionButtons.forEach(btn => btn.disabled = isLoading);
}

function persistKeyIfRemembered() {
  if (rememberCheckbox.checked) {
    localStorage.setItem(STORAGE_KEY, keyInput.value);
  }
}

rememberCheckbox.addEventListener("change", () => {
  if (rememberCheckbox.checked) {
    localStorage.setItem(STORAGE_KEY, keyInput.value);
  } else {
    localStorage.removeItem(STORAGE_KEY);
  }
});

keyInput.addEventListener("input", persistKeyIfRemembered);

generateBtn.addEventListener("click", async () => {
  setStatus("");
  setLoading(true);
  try {
    const res = await fetch("/key");
    const data = await res.json();
    if (!res.ok) throw new Error(data.error || "Errore nella generazione della chiave.");
    keyInput.value = data.key;
    persistKeyIfRemembered();
    setStatus("Nuova chiave generata.", "success");
  } catch (err) {
    setStatus(err.message || "Errore di rete.", "danger");
  } finally {
    setLoading(false);
  }
});

async function runCrypto(endpoint) {
  setStatus("");
  outputArea.value = "";
  setLoading(true);
  try {
    const res = await fetch(endpoint, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ key: keyInput.value, value: inputArea.value })
    });
    const data = await res.json();
    if (!res.ok) throw new Error(data.error || "Operazione non riuscita.");
    outputArea.value = data.result;
  } catch (err) {
    setStatus(err.message || "Errore di rete.", "danger");
  } finally {
    setLoading(false);
  }
}

encryptBtn.addEventListener("click", () => runCrypto("/encrypt"));
decryptBtn.addEventListener("click", () => runCrypto("/decrypt"));

copyBtn.addEventListener("click", async () => {
  if (!outputArea.value) return;
  try {
    await navigator.clipboard.writeText(outputArea.value);
    setStatus("Risultato copiato negli appunti.", "success");
  } catch {
    outputArea.select();
    setStatus("Copia manuale: testo selezionato.", "danger");
  }
});
