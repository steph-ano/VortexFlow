import axios, { AxiosError, AxiosInstance, InternalAxiosRequestConfig } from 'axios';
import type { ApiError, AuthUser, LoginResponse, TrendListResponse } from '../types';
import { useAuthStore } from '../stores/auth';

// Centralised axios instance. Single base URL, timeouts, and error normalisation.
// Authentication tokens are NOT stored in localStorage; the access token lives
// in the Pinia auth store (in-memory) and the refresh token in an HttpOnly
// cookie. The auth store patches the in-memory token via setAccessToken().

const api: AxiosInstance = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL ?? '/api',
  timeout: 10_000,
  headers: { 'Content-Type': 'application/json' },
  withCredentials: true, // needed so the refresh_token cookie is sent on /auth/refresh
});

api.interceptors.request.use((config: InternalAxiosRequestConfig) => {
  const auth = useAuthStore();
  if (auth.accessToken) {
    config.headers.set('Authorization', `Bearer ${auth.accessToken}`);
  }
  return config;
});

// A single-flight refresh: when several requests fail with 401 at the same
// time, only the first triggers a refresh; the rest await its result.
let refreshPromise: Promise<string | null> | null = null;

async function tryRefresh(): Promise<string | null> {
  if (refreshPromise) return refreshPromise;
  refreshPromise = (async () => {
    try {
      // Use a fresh, cookie-bearing request that does NOT go through the
      // interceptor (so we don't recursively re-inject a stale token).
      const res = await axios.post<LoginResponse>(
        `${import.meta.env.VITE_API_BASE_URL ?? '/api'}/auth/refresh`,
        {},
        { withCredentials: true, timeout: 10_000 },
      );
      return res.data.accessToken ?? null;
    } catch {
      return null;
    } finally {
      refreshPromise = null;
    }
  })();
  return refreshPromise;
}

api.interceptors.response.use(
  (response) => response,
  async (error: AxiosError<ApiError>) => {
    const auth = useAuthStore();
    const original = error.config as (InternalAxiosRequestConfig & { _retried?: boolean }) | undefined;
    const status = error.response?.status;

    if (status === 401 && original && !original._retried) {
      original._retried = true;
      const newToken = await tryRefresh();
      if (newToken) {
        auth.setAccessToken(newToken);
        original.headers?.set('Authorization', `Bearer ${newToken}`);
        return api.request(original);
      }
      auth.clear();
      // Soft redirect handled by router guard; the router can be imported lazily
      // to avoid a circular dependency. We use a global event so the guard
      // can react to auth state changes.
      window.dispatchEvent(new CustomEvent('vortexflow:unauthorized'));
    }

    // Normalise error so callers don't have to deal with axios internals.
    const data = error.response?.data as ApiError | undefined;
    const normalised: ApiError = {
      title: data?.title ?? error.message,
      detail: data?.detail ?? (typeof data === 'string' ? data : undefined),
      status: status,
      errors: data?.errors,
      correlationId: data?.correlationId,
    };
    return Promise.reject(normalised);
  },
);

// Typed API surface for stores. Keeps the call sites free of axios.
export const apiClient = {
  login: (email: string, password: string) =>
    api.post<LoginResponse>('/auth/login', { email, password }),
  logout: () => api.post<void>('/auth/logout', {}),
  me: () => api.get<AuthUser>('/auth/me'),
  refresh: () => api.post<LoginResponse>('/auth/refresh', {}),
  trends: {
    current: () => api.get<TrendListResponse>('/trends/current'),
  },
  campaigns: {
    list: () => api.get('/campaigns'),
    create: (body: { name: string; description: string }) =>
      api.post('/campaigns', body),
    schedulePost: (campaignId: string, body: { content: string; platform: string; scheduledDate: string }) =>
      api.post(`/campaigns/${campaignId}/posts`, body),
  },
  posts: {
    list: () => api.get('/campaigns/posts'),
    reschedule: (postId: string, date: string) =>
      api.put(`/campaigns/posts/${postId}/reschedule`, { date }),
    retry: (postId: string) => api.post(`/campaigns/posts/${postId}/retry`),
  },
};

export default api;
