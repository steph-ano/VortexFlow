<template>
  <div class="min-h-screen flex items-center justify-center bg-[url('https://images.unsplash.com/photo-1557683316-973673baf926?q=80&w=2000&auto=format&fit=crop')] bg-cover bg-center">
    <div class="absolute inset-0 bg-dark-900/60 backdrop-blur-sm"></div>
    <GlassCard class="relative w-full max-w-md z-10">
      <h1 class="text-2xl font-bold text-center text-white mb-6">VortexFlow</h1>
      <form @submit.prevent="handleLogin" class="space-y-4">
        <div>
          <label class="block text-sm font-medium text-slate-300 mb-1">Email</label>
          <input v-model="email" type="email" required class="glass-input w-full" placeholder="admin@vortexflow.local" />
        </div>
        <div>
          <label class="block text-sm font-medium text-slate-300 mb-1">Password</label>
          <input v-model="password" type="password" required class="glass-input w-full" placeholder="••••••••" />
        </div>
        <button type="submit" class="glass-button w-full" :disabled="loading">
          {{ loading ? 'Signing in...' : 'Sign In' }}
        </button>
      </form>
    </GlassCard>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue';
import { useRouter } from 'vue-router';
import { useAuthStore } from '../stores/auth';
import GlassCard from '../components/common/GlassCard.vue';
import { useToast } from 'vue-toastification';

const email = ref('admin@vortexflow.local');
const password = ref('Admin123!');
const loading = ref(false);
const authStore = useAuthStore();
const router = useRouter();
const toast = useToast();

const handleLogin = async () => {
  loading.value = true;
  try {
    await authStore.login({ email: email.value, password: password.value });
    router.push('/dashboard');
    toast.success('Logged in successfully');
  } catch (error: any) {
    toast.error(error.response?.data?.message || 'Login failed');
  } finally {
    loading.value = false;
  }
};
</script>
