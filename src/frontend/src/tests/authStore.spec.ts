import { setActivePinia, createPinia } from 'pinia'
import { describe, it, expect, beforeEach, vi } from 'vitest'
import { useAuthStore } from '../stores/auth'
import api from '../services/api'

vi.mock('../services/api', () => ({
  default: {
    post: vi.fn(),
  }
}))

describe('Auth Store', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    vi.clearAllMocks()
  })

  it('initializes with no token', () => {
    const store = useAuthStore()
    expect(store.token).toBeNull()
    expect(store.isAuthenticated).toBe(false)
  })

  it('logs in successfully and sets token', async () => {
    const store = useAuthStore()
    const mockToken = 'mock-jwt-token'
    
    // @ts-ignore
    api.post.mockResolvedValueOnce({ data: { token: mockToken } })
    
    await store.login({ email: 'test@test.com', password: 'password' })
    
    expect(store.token).toBe(mockToken)
    expect(store.isAuthenticated).toBe(true)
    expect(store.user.email).toBe('test@test.com')
    expect(localStorage.getItem('token')).toBe(mockToken)
  })

  it('logs out and clears token', () => {
    const store = useAuthStore()
    store.token = 'existing-token'
    localStorage.setItem('token', 'existing-token')
    
    store.logout()
    
    expect(store.token).toBeNull()
    expect(store.isAuthenticated).toBe(false)
    expect(localStorage.getItem('token')).toBeNull()
  })
})
