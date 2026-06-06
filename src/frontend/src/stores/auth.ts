import { defineStore } from 'pinia';
import { apiClient } from '../services/api';
import type { AuthUser } from '../types';

interface AuthState {
  accessToken: string | null;
  user: AuthUser | null;
  status: 'idle' | 'loading' | 'authenticated' | 'unauthenticated';
  lastError: string | null;
}

/**
 * Auth store. The access token is held ONLY in memory; persistence is via the
 * HttpOnly refresh_token cookie set by the .NET API. On app boot, the store
 * attempts a silent refresh + /me to repopulate state.
 */
export const useAuthStore = defineStore('auth', {
  state: (): AuthState => ({
    accessToken: null,
    user: null,
    status: 'idle',
    lastError: null,
  }),
  getters: {
    isAuthenticated: (state) => state.status === 'authenticated' && !!state.user,
  },
  actions: {
    setAccessToken(token: string | null) {
      this.accessToken = token;
    },

    async login(email: string, password: string): Promise<boolean> {
      this.status = 'loading';
      this.lastError = null;
      try {
        const { data } = await apiClient.login(email, password);
        this.accessToken = data.accessToken;
        this.user = data.user;
        this.status = 'authenticated';
        return true;
      } catch (err: any) {
        this.status = 'unauthenticated';
        this.lastError = err?.detail || err?.title || 'Login failed';
        throw err;
      }
    },

    async logout() {
      try {
        await apiClient.logout();
      } catch {
        // best-effort; the local state is cleared regardless
      }
      this.clear();
    },

    clear() {
      this.accessToken = null;
      this.user = null;
      this.status = 'unauthenticated';
    },

    /**
     * Bootstraps the auth state from the refresh cookie. Called once from
     * main.ts before the router takes over. Resolves to true when the user
     * is authenticated after the call.
     */
    async hydrate(): Promise<boolean> {
      this.status = 'loading';
      try {
        const refreshed = await apiClient.refresh();
        this.accessToken = refreshed.data.accessToken;
        const me = await apiClient.me();
        this.user = me.data;
        this.status = 'authenticated';
        return true;
      } catch {
        this.status = 'unauthenticated';
        this.accessToken = null;
        this.user = null;
        return false;
      }
    },
  },
});
