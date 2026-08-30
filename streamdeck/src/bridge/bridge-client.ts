import WebSocket from "ws";
import type { ConnectionSnapshot, ProtocolEnvelope, WildsState } from "../telemetry.js";

type Listener = (snapshot: ConnectionSnapshot) => void;

export class BridgeClient {
  readonly #endpoint: string;
  readonly #listeners = new Set<Listener>();
  #socket?: WebSocket;
  #state?: WildsState;
  #connected = false;
  #closed = false;
  #retryMs = 500;
  #retryTimer?: NodeJS.Timeout;

  constructor(endpoint = "ws://127.0.0.1:47653/ws") {
    this.#endpoint = endpoint;
  }

  get snapshot(): ConnectionSnapshot {
    return { bridgeConnected: this.#connected, state: this.#state };
  }

  subscribe(listener: Listener): () => void {
    this.#listeners.add(listener);
    listener(this.snapshot);
    return () => this.#listeners.delete(listener);
  }

  start(): void {
    this.#closed = false;
    this.#connect();
  }

  stop(): void {
    this.#closed = true;
    if (this.#retryTimer) clearTimeout(this.#retryTimer);
    this.#socket?.close();
  }

  #connect(): void {
    if (this.#closed || this.#socket?.readyState === WebSocket.OPEN || this.#socket?.readyState === WebSocket.CONNECTING) return;

    const socket = new WebSocket(this.#endpoint);
    this.#socket = socket;
    socket.on("open", () => {
      this.#connected = true;
      this.#retryMs = 500;
      this.#notify();
    });
    socket.on("message", (data) => this.#onMessage(data.toString()));
    socket.on("error", () => socket.close());
    socket.on("close", () => {
      if (this.#socket !== socket) return;
      this.#socket = undefined;
      this.#connected = false;
      this.#notify();
      if (!this.#closed) {
        this.#retryTimer = setTimeout(() => this.#connect(), this.#retryMs);
        this.#retryMs = Math.min(10_000, Math.round(this.#retryMs * 1.7));
      }
    });
  }

  #onMessage(raw: string): void {
    try {
      const message = JSON.parse(raw) as ProtocolEnvelope;
      if (message.protocolVersion !== 1) return;
      if (message.type === "state") {
        this.#state = message.data as WildsState;
        this.#notify();
      }
    } catch {
      // A malformed bridge message is ignored; the next snapshot can recover.
    }
  }

  #notify(): void {
    const snapshot = this.snapshot;
    for (const listener of this.#listeners) listener(snapshot);
  }
}

