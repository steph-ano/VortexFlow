<template>
  <div>
    <h2 class="text-3xl font-bold text-white mb-8">Live Trends Dashboard</h2>
    
    <div class="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-6 mb-8">
      <GlassCard class="h-96">
        <h3 class="text-lg font-semibold text-slate-300 mb-4">Menciones por Hashtag</h3>
        <v-chart class="h-full w-full" :option="volumeChartOption" autoresize />
      </GlassCard>
      
      <GlassCard class="h-96">
        <h3 class="text-lg font-semibold text-slate-300 mb-4">Sentimiento</h3>
        <v-chart class="h-full w-full" :option="sentimentChartOption" autoresize />
      </GlassCard>
      
      <GlassCard class="h-96">
        <h3 class="text-lg font-semibold text-slate-300 mb-4">Últimas Tendencias</h3>
        <div class="overflow-y-auto h-72 pr-2 space-y-3">
          <div v-for="trend in store.trends" :key="trend.eventId" class="bg-dark-900/50 p-3 rounded-lg border border-slate-700/50">
            <div class="flex justify-between items-start">
              <div class="flex flex-wrap gap-1">
                <span v-for="tag in trend.hashtags" :key="tag" class="text-xs bg-primary/20 text-primary px-2 py-1 rounded-full">
                  {{ tag }}
                </span>
              </div>
              <span class="text-xs text-slate-400">{{ trend.platform }}</span>
            </div>
            <div class="mt-2 text-sm text-slate-300 flex justify-between">
              <span>Vol: {{ trend.metrics.volume }}</span>
              <span :class="trend.metrics.sentiment > 0 ? 'text-green-400' : 'text-red-400'">
                Sentimiento: {{ trend.metrics.sentiment.toFixed(2) }}
              </span>
            </div>
          </div>
        </div>
      </GlassCard>
    </div>
  </div>
</template>

<script setup lang="ts">
import { onMounted, onUnmounted, computed } from 'vue';
import { useTrendsStore } from '../stores/trends';
import GlassCard from '../components/common/GlassCard.vue';
import { use } from 'echarts/core';
import { CanvasRenderer } from 'echarts/renderers';
import { BarChart, GaugeChart } from 'echarts/charts';
import { TitleComponent, TooltipComponent, GridComponent } from 'echarts/components';
import VChart from 'vue-echarts';

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
  const topTrends = [...store.trends].slice(0, 10);
  const data = topTrends.map(t => ({
    name: t.hashtags[0] || 'unknown',
    value: t.metrics.volume
  }));
  
  return {
    tooltip: { trigger: 'axis' },
    xAxis: { type: 'category', data: data.map(d => d.name), axisLabel: { color: '#94a3b8' } },
    yAxis: { type: 'value', axisLabel: { color: '#94a3b8' }, splitLine: { lineStyle: { color: '#334155' } } },
    series: [
      {
        data: data.map(d => d.value),
        type: 'bar',
        itemStyle: { color: '#0ea5e9', borderRadius: [4, 4, 0, 0] }
      }
    ]
  };
});

const sentimentChartOption = computed(() => {
  let avgSentiment = 0;
  if (store.trends.length > 0) {
    const sum = store.trends.reduce((acc, t) => acc + t.metrics.sentiment, 0);
    avgSentiment = sum / store.trends.length;
  }
  
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
        data: [{ value: parseFloat(avgSentiment.toFixed(2)) }]
      }
    ]
  };
});
</script>
