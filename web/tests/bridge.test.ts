import { afterEach, describe, expect, it, vi } from "vitest";
import { FeedBridge, type FeedWebViewHost, type FeedWebViewMessageEvent } from "../src/bridge";

const requestId = "169f4214-8e67-4f08-9ee7-d0706ab3adf4";

class FakeHost implements FeedWebViewHost {
  readonly messages: string[] = [];
  listener?: (event: FeedWebViewMessageEvent) => void;

  postMessage(message: string): void {
    this.messages.push(message);
  }

  addEventListener(_type: "message", listener: (event: FeedWebViewMessageEvent) => void): void {
    this.listener = listener;
  }

  removeEventListener(_type: "message", listener: (event: FeedWebViewMessageEvent) => void): void {
    if (this.listener === listener) {
      this.listener = undefined;
    }
  }

  emit(data: unknown): void {
    this.listener?.({ data });
  }
}

afterEach(() => {
  vi.useRealTimers();
});

describe("FeedBridge", () => {
  it("sends a serialized ping through the one-argument host overload", async () => {
    const host = new FakeHost();
    const bridge = createBridge(host);

    const pending = bridge.ping();

    expect(host.messages).toHaveLength(1);
    expect(JSON.parse(host.messages[0]!)).toEqual({
      version: 1,
      type: "diagnostics.ping",
      requestId,
      payload: { sentAtUtc: "2026-09-18T09:30:00.000Z" },
    });

    bridge.dispose();
    await expect(pending).rejects.toThrow("se cerró");
  });

  it("correlates a valid pong and calculates round-trip time", async () => {
    const host = new FakeHost();
    const dates = [new Date("2026-09-18T09:30:00.000Z"), new Date("2026-09-18T09:30:00.042Z")];
    const bridge = createBridge(host, () => dates.shift()!);
    const pending = bridge.ping();

    host.emit(JSON.stringify({
      version: 1,
      type: "diagnostics.pong",
      requestId,
      ok: true,
      payload: { providerUtc: "2026-09-18T09:30:00.0200000Z" },
    }));

    await expect(pending).resolves.toEqual({
      requestId,
      providerUtc: "2026-09-18T09:30:00.0200000Z",
      roundTripMilliseconds: 42,
    });
    bridge.dispose();
  });

  it("ignores an unrelated response and times out", async () => {
    vi.useFakeTimers();
    const host = new FakeHost();
    const bridge = createBridge(host);
    const pending = bridge.ping();

    host.emit(JSON.stringify({
      version: 1,
      type: "diagnostics.pong",
      requestId: "2c5327ee-917a-421e-b9c5-6cb663cc7013",
      ok: true,
      payload: { providerUtc: "2026-09-18T09:30:00.0200000Z" },
    }));
    const assertion = expect(pending).rejects.toThrow("5 segundos");

    await vi.advanceTimersByTimeAsync(5_000);
    await assertion;
    bridge.dispose();
  });

  it("surfaces a correlated typed error", async () => {
    const host = new FakeHost();
    const bridge = createBridge(host);
    const pending = bridge.ping();

    host.emit(JSON.stringify({
      version: 1,
      type: "diagnostics.error",
      requestId,
      ok: false,
      error: { code: "invalid_payload", message: "Payload rechazado." },
    }));

    await expect(pending).rejects.toThrow("invalid_payload: Payload rechazado.");
    bridge.dispose();
  });
});

function createBridge(host: FakeHost, now?: () => Date): FeedBridge {
  return new FeedBridge(host, "https://example.github.io", {
    createRequestId: () => requestId,
    now: now ?? (() => new Date("2026-09-18T09:30:00.000Z")),
  });
}
