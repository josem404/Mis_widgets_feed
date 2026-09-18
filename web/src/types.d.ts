declare module "*.css";

interface FeedWebViewMessageEvent {
  data: unknown;
}

interface FeedWebViewHost {
  postMessage(message: string): void;
  addEventListener(type: "message", listener: (event: FeedWebViewMessageEvent) => void): void;
  removeEventListener(type: "message", listener: (event: FeedWebViewMessageEvent) => void): void;
}

interface Window {
  chrome?: {
    webview?: FeedWebViewHost;
  };
}
