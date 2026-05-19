<template>
  <div class="min-h-screen bg-dark-900 flex text-slate-200">
    <aside class="w-64 bg-dark-800/80 border-r border-slate-700/50 backdrop-blur-lg flex flex-col fixed inset-y-0 z-20">
      <div class="h-16 flex items-center px-6 border-b border-slate-700/50">
        <h1 class="text-xl font-bold text-primary tracking-wide">VortexFlow</h1>
      </div>
      <nav class="flex-1 py-4 px-3 space-y-1">
        <router-link to="/dashboard" class="flex items-center px-3 py-2 rounded-lg transition-colors hover:bg-slate-700/50 text-slate-300 hover:text-white" active-class="bg-primary/20 text-primary border border-primary/30">
          Dashboard
        </router-link>
        <router-link to="/calendar" class="flex items-center px-3 py-2 rounded-lg transition-colors hover:bg-slate-700/50 text-slate-300 hover:text-white" active-class="bg-primary/20 text-primary border border-primary/30">
          Calendar
        </router-link>
        <router-link to="/campaigns" class="flex items-center px-3 py-2 rounded-lg transition-colors hover:bg-slate-700/50 text-slate-300 hover:text-white" active-class="bg-primary/20 text-primary border border-primary/30">
          Campaigns
        </router-link>
      </nav>
      <div class="p-4 border-t border-slate-700/50">
        <button @click="logout" class="flex items-center w-full px-3 py-2 text-sm text-slate-400 hover:text-white transition-colors">
          Logout
        </button>
      </div>
    </aside>

    <main class="flex-1 ml-64 p-8 relative min-h-screen">
      <!-- Decoración de fondo -->
      <div class="absolute top-[-20%] left-[-10%] w-[50%] h-[50%] rounded-full bg-primary/10 blur-[120px] pointer-events-none"></div>
      <div class="absolute bottom-[-20%] right-[-10%] w-[50%] h-[50%] rounded-full bg-secondary/10 blur-[120px] pointer-events-none"></div>
      
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
const authStore = useAuthStore();

const logout = () => {
  authStore.logout();
  router.push('/login');
};
</script>
