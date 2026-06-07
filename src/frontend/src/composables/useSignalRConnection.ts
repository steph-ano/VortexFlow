import {
  onScopeDispose,
  ref,
  shallowRef,
  type Ref,
  type ShallowRef,
} from 'vue';
import * as signalR from '@microsoft/signalr';

export type RealtimeState = signalR.HubConnectionState | 'idle';

export interface SignalRConnection {
  connection: ShallowRef<signalR.HubConnection | null>;
  state: Ref<RealtimeState>;
  init: (hubPath: string, tokenFactory: () => string) => Promise<void>;
  stop: () => Promise<void>;
  on: <T>(eventName: string, handler: (payload: T) => void) => void;
}

/**
 * SignalR connection holder that lives OUTSIDE Pinia.
 *
 * The hub connection holds non-serializable internals (live WebSocket frames,
 * internal retry timers, the JSON-protocol parser) that would otherwise
 * explode the Vue reactivity proxy, break Pinia DevTools serialisation, and
 * produce spurious re-renders. `shallowRef` keeps the value identity-stable
 * and skips deep observation — which is exactly what we want: callers
 * should react to `state` changes and `on(...)` callbacks, never to the
 * connection object itself.
 *
 * The composable auto-stops the connection when the calling scope is
 * disposed (i.e. the component that called `useSignalRConnection()`
 * unmounts), so callers do not need to wire their own `onUnmounted` to
 * `stop()`.
 */
export function useSignalRConnection(): SignalRConnection {
  const connection = shallowRef<signalR.HubConnection | null>(null);
  const state = ref<RealtimeState>('idle');

  // Handlers registered via on() are remembered so that, when init() is
  // called (or re-called after a teardown), we re-attach them to the new
  // connection. Each handler is stored as a (payload: unknown) => void so
  // a single Map can hold handlers for multiple event names without losing
  // type information at the public boundary.
  const pending = new Map<string, Set<(payload: unknown) => void>>();

  const init = async (hubPath: string, tokenFactory: () => string): Promise<void> => {
    if (connection.value) return;

    connection.value = new signalR.HubConnectionBuilder()
      .withUrl(hubPath, { accessTokenFactory: () => tokenFactory() })
      .withAutomaticReconnect([0, 2_000, 10_000, 30_000])
      .configureLogging(signalR.LogLevel.Warning)
      .build();
    const conn = connection.value;

    // Replay handlers registered before init (and re-register handlers from
    // a previous connection that was torn down by stop()).
    for (const [name, set] of pending) {
      for (const h of set) conn.on(name, h);
    }

    conn.onreconnecting(() => {
      state.value = signalR.HubConnectionState.Reconnecting;
    });
    conn.onreconnected(() => {
      state.value = signalR.HubConnectionState.Connected;
    });
    conn.onclose(() => {
      state.value = signalR.HubConnectionState.Disconnected;
    });

    try {
      await conn.start();
      state.value = signalR.HubConnectionState.Connected;
    } catch (err) {
      state.value = signalR.HubConnectionState.Disconnected;
      throw err;
    }
  };

  const stop = async (): Promise<void> => {
    const conn = connection.value;
    if (!conn) return;
    try {
      await conn.stop();
    } catch {
      // Stop can reject when the underlying transport is already closed
      // (e.g. after onclose fired). The connection is gone either way.
    }
    connection.value = null;
    state.value = 'idle';
  };

  const on = <T,>(eventName: string, handler: (payload: T) => void): void => {
    const bag = pending.get(eventName) ?? new Set<(payload: unknown) => void>();
    const cast = handler as (payload: unknown) => void;
    bag.add(cast);
    pending.set(eventName, bag);
    connection.value?.on(eventName, cast);
  };

  onScopeDispose(() => {
    void stop();
  });

  return { connection, state, init, stop, on };
}
