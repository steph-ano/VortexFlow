<template>
  <div>
    <div class="flex justify-between items-center mb-8">
      <h2 class="text-3xl font-bold text-white">Campaigns</h2>
      <button class="glass-button">New Campaign</button>
    </div>
    
    <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
      <GlassCard v-for="campaign in campaigns" :key="campaign.id" class="flex flex-col">
        <h3 class="text-xl font-bold text-primary mb-2">{{ campaign.name }}</h3>
        <p class="text-slate-300 text-sm mb-4 flex-1">{{ campaign.description }}</p>
        <div class="flex justify-between items-center text-xs text-slate-400 mt-auto pt-4 border-t border-slate-700/50">
          <span>Start: {{ new Date(campaign.startDate).toLocaleDateString() }}</span>
          <span>End: {{ new Date(campaign.endDate).toLocaleDateString() }}</span>
        </div>
      </GlassCard>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue';
import GlassCard from '../components/common/GlassCard.vue';

const campaigns = ref([
  { id: '1', name: 'Summer Sale 2026', description: 'Promotional campaign for summer discounts across all social platforms.', startDate: '2026-06-01', endDate: '2026-08-31' },
  { id: '2', name: 'Product Launch X', description: 'Teasers and launch day content for the new X product line.', startDate: '2026-07-15', endDate: '2026-07-30' },
]);
</script>
