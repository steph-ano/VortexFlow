import { defineConfig } from 'vite';
import vue from '@vitejs/plugin-vue';
import tailwindcss from '@tailwindcss/vite';

// https://vite.dev/config/
export default defineConfig({
  plugins: [vue(), tailwindcss()],
  build: {
    // Manual chunking keeps the initial bundle small. ECharts and FullCalendar
    // are the two largest third-party dependencies; isolating them into
    // separate chunks lets the browser cache them independently and lets the
    // routes that don't need them avoid loading them.
    rollupOptions: {
      output: {
        manualChunks: {
          echarts: ['echarts', 'vue-echarts', 'zrender'],
          fullcalendar: [
            '@fullcalendar/core',
            '@fullcalendar/vue3',
            '@fullcalendar/daygrid',
            '@fullcalendar/timegrid',
            '@fullcalendar/interaction',
          ],
          signalr: ['@microsoft/signalr'],
        },
      },
    },
  },
  server: {
    proxy: {
      '/api': {
        target: 'http://localhost:5032',
        changeOrigin: true,
      },
    },
  },
});
