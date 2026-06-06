import { setActivePinia, createPinia } from 'pinia';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { useAuthStore } from '../stores/auth';
import { apiClient } from '../services/api';

vi.mock('../services/api', () => ({
  apiClient: {
    login: vi.fn(),
    logout: vi.fn(),
    me: vi.fn(),
    refresh: vi.fn(),
  },
}));

describe('Auth store', () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.clearAllMocks();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('starts in idle state', () => {
    const store = useAuthStore();
    expect(store.status).toBe('idle');
    expect(store.isAuthenticated).toBe(false);
  });

  it('logs in successfully and caches the user', async () => {
    const store = useAuthStore();
    (apiClient.login as any).mockResolvedValueOnce({
      data: {
        accessToken: 'jwt-token',
        expiresAt: '2030-01-01T00:00:00Z',
        user: { id: 'u1', email: 'a@b.com', name: 'A', roles: ['Viewer'] },
      },
    });

    await store.login('a@b.com', 'p4ssw0rdP4ssw0rd');

    expect(store.accessToken).toBe('jwt-token');
    expect(store.isAuthenticated).toBe(true);
    expect(store.user?.email).toBe('a@b.com');
  });

  it('exposes the last error on failure', async () => {
    const store = useAuthStore();
    (apiClient.login as any).mockRejectedValueOnce({ detail: 'Invalid credentials.' });

    await expect(store.login('a@b.com', 'p4ssw0rdP4ssw0rd')).rejects.toBeDefined();
    expect(store.status).toBe('unauthenticated');
    expect(store.lastError).toBe('Invalid credentials.');
  });

  it('hydrate() calls refresh + me on boot and authenticates', async () => {
    const store = useAuthStore();
    (apiClient.refresh as any).mockResolvedValueOnce({
      data: { accessToken: 'jwt-token', expiresAt: '2030-01-01T00:00:00Z' },
    });
    (apiClient.me as any).mockResolvedValueOnce({
      data: { id: 'u1', email: 'a@b.com', name: 'A', roles: [] },
    });

    const ok = await store.hydrate();
    expect(ok).toBe(true);
    expect(store.isAuthenticated).toBe(true);
    expect(apiClient.refresh).toHaveBeenCalledTimes(1);
    expect(apiClient.me).toHaveBeenCalledTimes(1);
  });

  it('hydrate() leaves the store unauthenticated on failure', async () => {
    const store = useAuthStore();
    (apiClient.refresh as any).mockRejectedValueOnce(new Error('expired'));

    const ok = await store.hydrate();
    expect(ok).toBe(false);
    expect(store.isAuthenticated).toBe(false);
    expect(store.accessToken).toBeNull();
  });

  it('logout() clears local state regardless of server response', async () => {
    const store = useAuthStore();
    (apiClient.refresh as any).mockResolvedValueOnce({
      data: { accessToken: 'jwt-token', expiresAt: '2030-01-01T00:00:00Z' },
    });
    (apiClient.me as any).mockResolvedValueOnce({
      data: { id: 'u1', email: 'a@b.com', name: 'A', roles: [] },
    });
    await store.hydrate();
    expect(store.isAuthenticated).toBe(true);

    (apiClient.logout as any).mockRejectedValueOnce(new Error('network down'));
    await store.logout();
    expect(store.isAuthenticated).toBe(false);
    expect(store.accessToken).toBeNull();
  });
});
