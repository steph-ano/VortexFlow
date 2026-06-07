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
    // routes that don't need them avoid loading them. Vite 8 / Rollup 4 only
    // accept the function form of `manualChunks`, so we map module ids to
    // chunk names.
    rollupOptions: {
      output: {
        manualChunks: (id) => {
          if (id.includes('node_modules')) {
            if (
              id.includes('/echarts/') ||
              id.includes('/vue-echarts/') ||
              id.includes('/zrender/')
            ) {
              return 'echarts';
            }
            if (id.includes('/@fullcalendar/')) {
              return 'fullcalendar';
            }
            if (id.includes('/@microsoft/signalr/')) {
              return 'signalr';
            }
          }
          return undefined;
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
