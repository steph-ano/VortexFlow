<template>
  <div class="min-h-screen flex items-center justify-center bg-slate-950">
    <div class="absolute inset-0 bg-slate-900/60 backdrop-blur-sm"></div>
    <GlassCard class="relative w-full max-w-md z-10">
      <h1 class="text-2xl font-bold text-center text-white mb-6">VortexFlow</h1>
      <form @submit.prevent="handleLogin" class="space-y-4" novalidate>
        <div>
          <label for="email" class="block text-sm font-medium text-slate-300 mb-1">Email</label>
          <input
            id="email"
            v-model="email"
            type="email"
            autocomplete="username"
            required
            class="glass-input w-full"
            placeholder="you@example.com"
          />
        </div>
        <div>
          <label for="password" class="block text-sm font-medium text-slate-300 mb-1">Password</label>
          <input
            id="password"
            v-model="password"
            type="password"
            autocomplete="current-password"
            required
            minlength="12"
            class="glass-input w-full"
          />
        </div>
        <p v-if="auth.lastError" class="text-sm text-red-400" role="alert">
          {{ auth.lastError }}
        </p>
        <button type="submit" class="glass-button w-full" :disabled="loading">
          {{ loading ? 'Signing in…' : 'Sign In' }}
        </button>
      </form>
    </GlassCard>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { useAuthStore } from '../stores/auth';
import { useToast } from 'vue-toastification';
import GlassCard from '../components/common/GlassCard.vue';

const email = ref('');
const password = ref('');
const loading = ref(false);

const auth = useAuthStore();
const router = useRouter();
const route = useRoute();
const toast = useToast();

async function handleLogin() {
  loading.value = true;
  try {
    await auth.login(email.value, password.value);
    const redirect = (route.query.redirect as string) || '/dashboard';
    router.push(redirect);
    toast.success('Logged in successfully');
  } catch {
    // error message is already in auth.lastError; the template renders it
  } finally {
    loading.value = false;
  }
}
</script>
