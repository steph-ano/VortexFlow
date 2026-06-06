<template>
  <div>
    <div class="flex justify-between items-center mb-8">
      <h2 class="text-3xl font-bold text-white">Campaigns</h2>
      <button class="glass-button" @click="showForm = !showForm">
        {{ showForm ? 'Cancel' : 'New Campaign' }}
      </button>
    </div>

    <form v-if="showForm" @submit.prevent="handleCreate" class="glass-panel p-6 mb-6 max-w-xl space-y-3">
      <div>
        <label for="campaign-name" class="block text-sm text-slate-300 mb-1">Name</label>
        <input
          id="campaign-name"
          v-model="form.name"
          required
          maxlength="120"
          class="glass-input w-full"
        />
      </div>
      <div>
        <label for="campaign-desc" class="block text-sm text-slate-300 mb-1">Description</label>
        <textarea
          id="campaign-desc"
          v-model="form.description"
          maxlength="1024"
          rows="3"
          class="glass-input w-full"
        ></textarea>
      </div>
      <p v-if="createError" class="text-sm text-red-400">{{ createError }}</p>
      <button type="submit" class="glass-button" :disabled="creating">
        {{ creating ? 'Creating…' : 'Create' }}
      </button>
    </form>

    <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
      <GlassCard
        v-for="campaign in campaigns"
        :key="campaign.id"
        class="flex flex-col"
      >
        <h3 class="text-xl font-bold text-sky-400 mb-2">{{ campaign.name }}</h3>
        <p class="text-slate-300 text-sm mb-4 flex-1">{{ campaign.description }}</p>
      </GlassCard>
      <p v-if="!loading && campaigns.length === 0" class="text-slate-500 col-span-full text-center">
        No campaigns yet. Create your first one.
      </p>
    </div>
  </div>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue';
import { apiClient } from '../services/api';
import GlassCard from '../components/common/GlassCard.vue';
import type { Campaign } from '../types';

const campaigns = ref<Campaign[]>([]);
const loading = ref(false);
const creating = ref(false);
const showForm = ref(false);
const createError = ref<string | null>(null);
const form = reactive({ name: '', description: '' });

async function load() {
  loading.value = true;
  try {
    const { data } = await apiClient.campaigns.list();
    campaigns.value = Array.isArray(data) ? data : [];
  } finally {
    loading.value = false;
  }
}

async function handleCreate() {
  creating.value = true;
  createError.value = null;
  try {
    const { data } = await apiClient.campaigns.create({ name: form.name, description: form.description });
    campaigns.value.push(data);
    form.name = '';
    form.description = '';
    showForm.value = false;
  } catch (err: any) {
    createError.value = err?.detail ?? 'Failed to create campaign';
  } finally {
    creating.value = false;
  }
}

onMounted(load);
</script>
