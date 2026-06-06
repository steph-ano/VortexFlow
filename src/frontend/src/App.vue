<template>
  <router-view></router-view>
</template>

<script setup lang="ts">
import { onMounted } from 'vue';
import { useAuthStore } from './stores/auth';
import { useRouter } from 'vue-router';

const auth = useAuthStore();
const router = useRouter();

// On boot, attempt a silent refresh + /me. The router guard will run after
// this resolves and the state is consistent for the initial navigation.
onMounted(async () => {
  if (auth.status === 'idle') {
    await auth.hydrate();
  }
  window.addEventListener('vortexflow:unauthorized', () => {
    router.push({ path: '/login', query: { redirect: router.currentRoute.value.fullPath } });
  });
});
</script>
