import { defineStore } from 'pinia';
import api from '../services/api';

export const useAuthStore = defineStore('auth', {
  state: () => ({
    user: null as any | null,
    token: localStorage.getItem('token') || null,
  }),
  getters: {
    isAuthenticated: (state) => !!state.token,
  },
  actions: {
    async login(credentials: any) {
      try {
        const response = await api.post('/api/auth/login', credentials);
        this.token = response.data.token;
        this.user = { email: credentials.email };
        localStorage.setItem('token', this.token as string);
        return true;
      } catch (error) {
        throw error;
      }
    },
    logout() {
      this.token = null;
      this.user = null;
      localStorage.removeItem('token');
    }
  }
});
