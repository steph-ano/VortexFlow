<template>
  <div>
    <h2 class="text-3xl font-bold text-white mb-8">Live Trends Dashboard</h2>
    <p v-if="store.error" class="text-sm text-amber-400 mb-4" role="alert">{{ store.error }}</p>

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
                  v-for="tag in trend.hashtags"
                  :key="tag"
                  class="text-xs bg-sky-500/20 text-sky-300 px-2 py-1 rounded-full"
                >
                  {{ tag }}
                </span>
              </div>
              <span class="text-xs text-slate-400">{{ trend.platform }}</span>
            </div>
            <div class="mt-2 text-sm text-slate-300 flex justify-between">
              <span>Vol: {{ trend.metrics?.volume ?? 0 }}</span>
              <span :class="(trend.metrics?.sentiment ?? 0) > 0 ? 'text-green-400' : 'text-red-400'">
                Sentiment: {{ (trend.metrics?.sentiment ?? 0).toFixed(2) }}
              </span>
            </div>
          </div>
          <p v-if="!store.loading && store.trends.length === 0" class="text-center text-slate-500 mt-8">
            No trends yet.
          </p>
        </div>
      </GlassCard>
    </div>
  </div>
</template>

<script setup lang="ts">
import { onMounted, onUnmounted, computed } from 'vue';
import { use } from 'echarts/core';
import { CanvasRenderer } from 'echarts/renderers';
import { BarChart, GaugeChart } from 'echarts/charts';
import { TitleComponent, TooltipComponent, GridComponent } from 'echarts/components';
import VChart from 'vue-echarts';
import GlassCard from '../components/common/GlassCard.vue';
import { useTrendsStore } from '../stores/trends';

use([CanvasRenderer, BarChart, GaugeChart, TitleComponent, TooltipComponent, GridComponent]);

const store = useTrendsStore();

onMounted(() => {
  store.fetchCurrentTrends();
  store.initSignalR();
});
onUnmounted(() => {
  store.stopSignalR();
});

const volumeChartOption = computed(() => {
  const data = store.trends.slice(0, 10).map((t) => ({
    name: t.hashtags[0] ?? 'unknown',
    value: t.metrics?.volume ?? 0,
  }));
  return {
    tooltip: { trigger: 'axis' },
    xAxis: { type: 'category', data: data.map((d) => d.name), axisLabel: { color: '#94a3b8' } },
    yAxis: { type: 'value', axisLabel: { color: '#94a3b8' }, splitLine: { lineStyle: { color: '#334155' } } },
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
  const avg = store.trends.length
    ? store.trends.reduce((acc, t) => acc + (t.metrics?.sentiment ?? 0), 0) / store.trends.length
    : 0;
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
        detail: { valueAnimation: true, formatter: '{value}', color: '#f8fafc', fontSize: 30, offsetCenter: [0, '30%'] },
        data: [{ value: Number(avg.toFixed(2)) }],
      },
    ],
  };
});
</script>
