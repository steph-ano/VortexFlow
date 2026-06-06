<template>
  <div class="min-h-screen bg-slate-950 flex text-slate-200">
    <aside class="w-64 bg-slate-900/80 border-r border-slate-700/50 backdrop-blur-lg flex flex-col fixed inset-y-0 z-20">
      <div class="h-16 flex items-center px-6 border-b border-slate-700/50">
        <h1 class="text-xl font-bold text-sky-400 tracking-wide">VortexFlow</h1>
      </div>
      <nav class="flex-1 py-4 px-3 space-y-1">
        <router-link
          v-for="link in links"
          :key="link.to"
          :to="link.to"
          class="flex items-center px-3 py-2 rounded-lg transition-colors hover:bg-slate-700/50 text-slate-300 hover:text-white"
          active-class="bg-sky-500/20 text-sky-300 border border-sky-500/30"
        >
          {{ link.label }}
        </router-link>
      </nav>
      <div class="p-4 border-t border-slate-700/50 text-sm text-slate-400">
        <div v-if="auth.user" class="mb-2 truncate">{{ auth.user.email }}</div>
        <button
          @click="handleLogout"
          class="flex items-center w-full px-3 py-2 text-left hover:text-white transition-colors"
        >
          Logout
        </button>
      </div>
    </aside>

    <main class="flex-1 ml-64 p-8 relative min-h-screen">
      <div
        class="absolute top-[-20%] left-[-10%] w-[50%] h-[50%] rounded-full bg-sky-500/10 blur-[120px] pointer-events-none"
        aria-hidden="true"
      ></div>
      <div
        class="absolute bottom-[-20%] right-[-10%] w-[50%] h-[50%] rounded-full bg-teal-500/10 blur-[120px] pointer-events-none"
        aria-hidden="true"
      ></div>

      <div class="relative z-10">
        <router-view></router-view>
      </div>
    </main>
  </div>
</template>

<script setup lang="ts">
import { useRouter } from 'vue-router';
import { useAuthStore } from '../stores/auth';

const router = useRouter();
const auth = useAuthStore();

const links = [
  { to: '/dashboard', label: 'Dashboard' },
  { to: '/calendar', label: 'Calendar' },
  { to: '/campaigns', label: 'Campaigns' },
];

async function handleLogout() {
  await auth.logout();
  router.push('/login');
}
</script>
