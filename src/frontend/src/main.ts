import { createApp } from 'vue';
import { createPinia } from 'pinia';
import App from './App.vue';
import router from './router';
import Toast from 'vue-toastification';
import 'vue-toastification/dist/index.css';
import './style.css';
import { useEcharts } from './composables/useEcharts';

// ECharts component/renderer registration is global and idempotent. Doing it
// once at boot (instead of inside Dashboard.vue) avoids the duplicate-
// registration warning on remount and centralises the module list.
useEcharts();

const app = createApp(App);

app.use(createPinia());
app.use(router);
app.use(Toast, {
  position: 'top-right',
  timeout: 3000,
  closeOnClick: true,
});

app.mount('#app');
