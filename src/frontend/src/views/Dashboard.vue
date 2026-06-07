<template>
  <div>
    <h2 class="text-3xl font-bold text-white mb-8">Live Trends Dashboard</h2>
    <p v-if="store.error" class="text-sm text-amber-400 mb-4" role="alert">{{ store.error }}</p>
    <p
      v-if="realtimeError"
      class="text-xs text-slate-500 mb-4"
      role="status"
    >
      Realtime: {{ realtimeError }}
    </p>

    <div class="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-6 mb-8">
      <GlassCard class="h-96">
        <h3 class="text-lg font-semibold text-slate-300 mb-4">Mentions by Hashtag</h3>
        <v-chart class="h-full w-full" :option="volumeChartOption" autoresize />
      </GlassCard>

      <GlassCard class="h-96">
        <h3 class="text-lg font-semibold text-slate-300 mb-4">Sentiment</h3>
        <v-chart class="h-full w-full" :option="sentimentChartOption" autoresize />
      </GlassCard>

      <GlassCard class="h-96">
        <h3 class="text-lg font-semibold text-slate-300 mb-4">Latest Trends</h3>
        <div class="overflow-y-auto h-72 pr-2 space-y-3">
          <div
            v-for="trend in store.trends"
            :key="trend.eventId"
            class="bg-slate-900/50 p-3 rounded-lg border border-slate-700/50"
          >
            <div class="flex justify-between items-start">
              <div class="flex flex-wrap gap-1">
                <span
                  v-for="tag in trend.hashtags ?? []"
                  :key="tag"
                  class="text-xs bg-sky-500/20 text-sky-300 px-2 py-1 rounded-full"
                >
                  {{ tag }}
                </span>
              </div>
              <span class="text-xs text-slate-400">{{ trend.platform }}</span>
            </div>
            <div class="mt-2 text-sm text-slate-300 flex justify-between">
              <span>Vol: {{ formatVolume(trend.metrics?.volume) }}</span>
              <span
                :class="(trend.metrics?.sentiment ?? 0) > 0 ? 'text-green-400' : 'text-red-400'"
              >
                Sentiment: {{ formatSentiment(trend.metrics?.sentiment) }}
              </span>
            </div>
          </div>
          <p
            v-if="!store.loading && store.trends.length === 0"
            class="text-center text-slate-500 mt-8"
          >
            No trends yet.
          </p>
        </div>
      </GlassCard>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import VChart from 'vue-echarts';
import GlassCard from '../components/common/GlassCard.vue';
import { useTrendsStore } from '../stores/trends';
import { useAuthStore } from '../stores/auth';
import { useSignalRConnection } from '../composables/useSignalRConnection';
import { formatSentiment, formatVolume } from '../utils/format';
import type { Trend } from '../types';

const store = useTrendsStore();
const auth = useAuthStore();

// SignalR is held in a shallowRef-backed composable so the live WebSocket
// internals never enter the Pinia/Vue reactivity graph. The composable
// auto-stops the connection when this view unmounts. The user-visible
// `realtimeError` is a view-local ref because the error message is a UI
// concern, not part of the connection's domain state.
const realtime = useSignalRConnection();
const realtimeError = ref<string | null>(null);

const baseUrl = (import.meta.env.VITE_API_BASE_URL ?? '/api').replace(/\/api\/?$/, '');

onMounted(async () => {
  await store.fetchCurrentTrends();
  realtime.on<Trend>('TrendsUpdated', (event) => store.applyRealtimeEvent(event));
  try {
    await realtime.init(`${baseUrl}/trendshub`, () => auth.accessToken ?? '');
  } catch (err) {
    const e = err as { message?: string };
    realtimeError.value = `Realtime updates unavailable: ${e?.message ?? 'unknown error'}`;
  }
});

const volumeChartOption = computed(() => {
  const data = store.trends.slice(0, 10).map((t) => ({
    name: t.hashtags?.[0] ?? 'unknown',
    value: Number.isFinite(t.metrics?.volume) ? (t.metrics!.volume as number) : 0,
  }));
  return {
    tooltip: { trigger: 'axis' },
    xAxis: { type: 'category', data: data.map((d) => d.name), axisLabel: { color: '#94a3b8' } },
    yAxis: {
      type: 'value',
      axisLabel: { color: '#94a3b8' },
      splitLine: { lineStyle: { color: '#334155' } },
    },
    series: [
      {
        data: data.map((d) => d.value),
        type: 'bar',
        itemStyle: { color: '#0ea5e9', borderRadius: [4, 4, 0, 0] },
      },
    ],
  };
});

const sentimentChartOption = computed(() => {
  const values = store.trends
    .map((t) => Number(t.metrics?.sentiment))
    .filter((n) => Number.isFinite(n));
  const avg = values.length ? values.reduce((acc, n) => acc + n, 0) / values.length : 0;
  return {
    series: [
      {
        type: 'gauge',
        startAngle: 180,
        endAngle: 0,
        min: -1,
        max: 1,
        splitNumber: 8,
        itemStyle: { color: '#14b8a6' },
        progress: { show: true, width: 18 },
        pointer: { show: false },
        axisLine: { lineStyle: { width: 18, color: [[1, '#334155']] } },
        axisTick: { show: false },
        splitLine: { show: false },
        axisLabel: { show: false },
        detail: {
          valueAnimation: true,
          formatter: '{value}',
          color: '#f8fafc',
          fontSize: 30,
          offsetCenter: [0, '30%'],
        },
        data: [{ value: Number(formatSentiment(avg)) }],
      },
    ],
  };
});
</script>
