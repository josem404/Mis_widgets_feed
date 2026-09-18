export const BRIDGE_VERSION = 1;
export const MAXIMUM_MESSAGE_CHARACTERS = 8 * 1024;

export interface FeedWebViewMessageEvent {
  data: unknown;
}

export interface FeedWebViewHost {
  postMessage(message: string): void;
  addEventListener(type: "message", listener: (event: FeedWebViewMessageEvent) => void): void;
  removeEventListener(type: "message", listener: (event: FeedWebViewMessageEvent) => void): void;
}

export interface DiagnosticsPong {
  requestId: string;
  providerUtc: string;
  roundTripMilliseconds: number;
}

interface PendingRequest {
  sentAtMilliseconds: number;
  resolve: (pong: DiagnosticsPong) => void;
  reject: (error: Error) => void;
  timeout: ReturnType<typeof setTimeout>;
}

interface FeedBridgeOptions {
  now?: () => Date;
  createRequestId?: () => string;
  timeoutMilliseconds?: number;
}

export class FeedBridge {
  readonly #host: FeedWebViewHost;
  readonly #now: () => Date;
  readonly #createRequestId: () => string;
  readonly #timeoutMilliseconds: number;
  readonly #pending = new Map<string, PendingRequest>();
  readonly #messageListener: (event: FeedWebViewMessageEvent) => void;

  constructor(host: FeedWebViewHost, contentOrigin: string, options: FeedBridgeOptions = {}) {
    if (!contentOrigin.startsWith("https://")) {
      throw new Error("El bridge exige un origen HTTPS explicito.");
    }

    this.#host = host;
    this.#now = options.now ?? (() => new Date());
    this.#createRequestId = options.createRequestId ?? (() => crypto.randomUUID());
    this.#timeoutMilliseconds = options.timeoutMilliseconds ?? 5_000;
    this.#messageListener = (event) => this.#onMessage(event);
    this.#host.addEventListener("message", this.#messageListener);
  }

  ping(): Promise<DiagnosticsPong> {
    const requestId = this.#createRequestId();
    if (!isCanonicalUuid(requestId)) {
      throw new Error("El generador produjo un requestId no valido.");
    }

    const sentAt = this.#now();
    const request = JSON.stringify({
      version: BRIDGE_VERSION,
      type: "diagnostics.ping",
      requestId,
      payload: { sentAtUtc: sentAt.toISOString() },
    });

    return new Promise<DiagnosticsPong>((resolve, reject) => {
      const timeout = setTimeout(() => {
        this.#pending.delete(requestId);
        reject(new Error("El provider no respondió dentro de 5 segundos."));
      }, this.#timeoutMilliseconds);

      this.#pending.set(requestId, {
        sentAtMilliseconds: sentAt.getTime(),
        resolve,
        reject,
        timeout,
      });

      try {
        // EmbeddedBrowserWebView expone una unica sobrecarga. A diferencia de
        // window.postMessage, este metodo no admite targetOrigin como segundo argumento.
        this.#host.postMessage(request);
      } catch (error) {
        clearTimeout(timeout);
        this.#pending.delete(requestId);
        reject(error instanceof Error ? error : new Error("No se pudo enviar el mensaje al provider."));
      }
    });
  }

  dispose(): void {
    this.#host.removeEventListener("message", this.#messageListener);
    for (const pending of this.#pending.values()) {
      clearTimeout(pending.timeout);
      pending.reject(new Error("El bridge se cerró antes de recibir respuesta."));
    }
    this.#pending.clear();
  }

  #onMessage(event: FeedWebViewMessageEvent): void {
    if (typeof event.data !== "string" || event.data.length > MAXIMUM_MESSAGE_CHARACTERS) {
      return;
    }

    let value: unknown;
    try {
      value = JSON.parse(event.data);
    } catch {
      return;
    }

    if (!isRecord(value) || value.version !== BRIDGE_VERSION || typeof value.requestId !== "string") {
      return;
    }

    const pending = this.#pending.get(value.requestId);
    if (!pending) {
      return;
    }

    if (value.type === "diagnostics.error" && value.ok === false && isRecord(value.error)) {
      clearTimeout(pending.timeout);
      this.#pending.delete(value.requestId);
      const code = typeof value.error.code === "string" ? value.error.code : "unknown_error";
      const message = typeof value.error.message === "string" ? value.error.message : "Error sin detalle.";
      pending.reject(new Error(`${code}: ${message}`));
      return;
    }

    if (value.type !== "diagnostics.pong" || value.ok !== true || !isRecord(value.payload)) {
      return;
    }

    const providerUtc = value.payload.providerUtc;
    if (typeof providerUtc !== "string" || !Number.isFinite(Date.parse(providerUtc))) {
      return;
    }

    clearTimeout(pending.timeout);
    this.#pending.delete(value.requestId);
    pending.resolve({
      requestId: value.requestId,
      providerUtc,
      roundTripMilliseconds: Math.max(0, this.#now().getTime() - pending.sentAtMilliseconds),
    });
  }
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function isCanonicalUuid(value: string): boolean {
  return /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(value);
}
