import "./style.css";
import { FeedBridge } from "./bridge";

const hostMode = requiredElement<HTMLSpanElement>("host-mode");
const pingButton = requiredElement<HTMLButtonElement>("ping-button");
const result = requiredElement<HTMLDivElement>("result");
const webView = window.chrome?.webview;

if (!webView) {
  hostMode.textContent = "Modo navegador";
  hostMode.dataset.mode = "browser";
  pingButton.disabled = true;
  result.textContent = "Abre esta página desde el Widgets Board para probar el provider nativo.";
} else {
  const bridge = new FeedBridge(webView, window.location.origin);
  hostMode.textContent = "Widgets Board";
  hostMode.dataset.mode = "board";

  pingButton.addEventListener("click", async () => {
    pingButton.disabled = true;
    result.dataset.state = "pending";
    result.textContent = "Esperando respuesta del provider…";

    try {
      const pong = await bridge.ping();
      result.dataset.state = "success";
      result.textContent = `Conexión correcta · ${pong.roundTripMilliseconds} ms · provider ${formatUtc(pong.providerUtc)}`;
    } catch (error) {
      result.dataset.state = "error";
      result.textContent = error instanceof Error ? error.message : "La prueba no pudo completarse.";
    } finally {
      pingButton.disabled = false;
    }
  });

  window.addEventListener("pagehide", () => bridge.dispose(), { once: true });
}

function requiredElement<T extends HTMLElement>(id: string): T {
  const element = document.getElementById(id);
  if (!element) {
    throw new Error(`Falta el elemento #${id}.`);
  }
  return element as T;
}

function formatUtc(value: string): string {
  return new Intl.DateTimeFormat("es-ES", {
    hour: "2-digit",
    minute: "2-digit",
    second: "2-digit",
  }).format(new Date(value));
}
