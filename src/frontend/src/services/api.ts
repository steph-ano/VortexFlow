import axios from 'axios';
import { useAuthStore } from '../stores/auth';
import { useToast } from 'vue-toastification';

const api = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL || 'http://localhost:5000',
  headers: {
    'Content-Type': 'application/json',
  },
});

api.interceptors.request.use(
  (config) => {
    const authStore = useAuthStore();
    if (authStore.token) {
      config.headers.Authorization = `Bearer ${authStore.token}`;
    }
    return config;
  },
  (error) => Promise.reject(error)
);

api.interceptors.response.use(
  (response) => response,
  (error) => {
    const authStore = useAuthStore();
    const toast = useToast();
    
    if (error.response?.status === 401) {
      authStore.logout();
      window.location.href = '/login';
    } else if (error.response?.status >= 500) {
      toast.error('An unexpected server error occurred.');
    }
    
    return Promise.reject(error);
  }
);

export default api;
