import { defineStore } from 'pinia';
import { markRaw, type Ref, shallowRef } from 'vue';
import * as signalR from '@microsoft/signalr';
import { apiClient } from '../services/api';
import { useAuthStore } from './auth';
import type { Trend } from '../types';

interface TrendsState {
  trends: Trend[];
  loading: boolean;
  error: string | null;
  // The SignalR connection holds non-serializable internals; using shallowRef
  // keeps it out of the reactive proxy and prevents accidental observation.
  // markRaw makes the object non-reactive. Both are belt-and-suspenders.
  connection: signalR.HubConnection | null;
  connectionState: signalR.HubConnectionState | 'idle';
}

export const useTrendsStore = defineStore('trends', {
  state: (): TrendsState => ({
    trends: [],
    loading: false,
    error: null,
    connection: null,
    connectionState: 'idle',
  }),
  actions: {
    async fetchCurrentTrends() {
      this.loading = true;
      this.error = null;
      try {
        const { data } = await apiClient.trends.current();
        this.trends = data.trends ?? [];
      } catch (err: any) {
        this.error = err?.detail ?? 'Failed to fetch trends';
        // Don't throw — the dashboard can still show cached state.
      } finally {
        this.loading = false;
      }
    },

    async initSignalR() {
      if (this.connection) return;

      const auth = useAuthStore();
      const baseUrl = (import.meta.env.VITE_API_BASE_URL ?? '/api').replace(/\/api\/?$/, '');

      this.connection = markRaw(
        new signalR.HubConnectionBuilder()
          .withUrl(`${baseUrl}/trendshub`, {
            // Send the current access token on the WebSocket query string.
            // The .NET SignalR JWT bearer handler reads it from the access_token
            // token by default.
            accessTokenFactory: () => auth.accessToken ?? '',
          })
          .withAutomaticReconnect([0, 2_000, 10_000, 30_000])
          .configureLogging(signalR.LogLevel.Warning)
          .build(),
      );

      const conn = this.connection;
      conn.on('TrendsUpdated', (event: Trend) => {
        const idx = this.trends.findIndex((t) => t.eventId === event.eventId);
        if (idx >= 0) {
          this.trends[idx] = event;
        } else {
          this.trends.unshift(event);
          if (this.trends.length > 50) this.trends.length = 50;
        }
      });

      conn.onreconnecting(() => {
        this.connectionState = signalR.HubConnectionState.Reconnecting;
      });
      conn.onreconnected(() => {
        this.connectionState = signalR.HubConnectionState.Connected;
      });
      conn.onclose(() => {
        this.connectionState = signalR.HubConnectionState.Disconnected;
      });

      try {
        await conn.start();
        this.connectionState = signalR.HubConnectionState.Connected;
      } catch (err: any) {
        this.connectionState = signalR.HubConnectionState.Disconnected;
        this.error = `Realtime updates unavailable: ${err?.message ?? 'unknown error'}`;
      }
    },

    async stopSignalR() {
      if (this.connection) {
        try {
          await this.connection.stop();
        } catch {
          // ignore
        }
        this.connection = null;
        this.connectionState = 'idle';
      }
    },
  },
});
