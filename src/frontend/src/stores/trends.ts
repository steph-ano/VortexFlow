import { defineStore } from 'pinia';
import { apiClient } from '../services/api';
import type { Trend } from '../types';

interface TrendsState {
  trends: Trend[];
  loading: boolean;
  error: string | null;
}

/**
 * Pure data store: holds the trend list and its fetch lifecycle.
 *
 * The realtime SignalR connection is intentionally NOT stored here — it
 * lives in a per-view `useSignalRConnection` composable, which keeps the
 * non-serialisable hub internals out of Pinia/DevTools and scopes the
 * WebSocket to the component that needs it. The composable's
 * `on('TrendsUpdated', ...)` handler pushes events into `this.trends`.
 */
export const useTrendsStore = defineStore('trends', {
  state: (): TrendsState => ({
    trends: [],
    loading: false,
    error: null,
  }),
  actions: {
    async fetchCurrentTrends(): Promise<void> {
      this.loading = true;
      this.error = null;
      try {
        const { data } = await apiClient.trends.current();
        this.trends = data.trends ?? [];
      } catch (err) {
        const e = err as { detail?: string };
        this.error = e?.detail ?? 'Failed to fetch trends';
        // Don't throw — the dashboard can still show cached state.
      } finally {
        this.loading = false;
      }
    },

    /**
     * Applies a realtime `Trend` event to the in-memory list. Idempotent
     * upsert keyed on `eventId`, capped at 50 entries to bound memory.
     */
    applyRealtimeEvent(event: Trend): void {
      const idx = this.trends.findIndex((t) => t.eventId === event.eventId);
      if (idx >= 0) {
        this.trends[idx] = event;
      } else {
        this.trends.unshift(event);
        if (this.trends.length > 50) this.trends.length = 50;
      }
    },
  },
});
