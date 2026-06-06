<template>
  <div class="flex flex-col lg:flex-row gap-6 h-[calc(100vh-8rem)]">
    <GlassCard class="w-full lg:w-80 flex-shrink-0 flex flex-col h-full overflow-hidden">
      <h3 class="text-xl font-bold text-white mb-4">Unscheduled Posts</h3>
      <div class="flex-1 overflow-y-auto pr-2 space-y-3">
        <div
          v-for="post in store.unscheduled"
          :key="post.id"
          class="bg-slate-900/50 p-3 rounded-lg border border-slate-700/50 cursor-grab hover:bg-slate-700/50 transition-colors post-drag"
          :data-post-id="post.id"
        >
          <p class="text-sm text-slate-200 line-clamp-2">{{ post.content }}</p>
          <div class="mt-2 flex justify-between items-center text-xs">
            <span class="text-slate-400">{{ post.platform }}</span>
            <span class="px-2 py-0.5 rounded-full bg-slate-800 text-slate-300">{{ post.status }}</span>
          </div>
        </div>
        <div v-if="store.unscheduled.length === 0" class="text-center text-slate-500 mt-8">
          No unscheduled posts
        </div>
      </div>
    </GlassCard>

    <GlassCard class="flex-1 flex flex-col h-full overflow-hidden">
      <div class="h-full w-full calendar-container">
        <FullCalendar :options="calendarOptions" class="h-full" />
      </div>
    </GlassCard>
  </div>
</template>

<script setup lang="ts">
import { onMounted, onUnmounted, ref, watch } from 'vue';
import FullCalendar from '@fullcalendar/vue3';
import dayGridPlugin from '@fullcalendar/daygrid';
import timeGridPlugin from '@fullcalendar/timegrid';
import interactionPlugin, { Draggable } from '@fullcalendar/interaction';
import GlassCard from '../components/common/GlassCard.vue';
import { useCalendarStore } from '../stores/calendar';
import { useToast } from 'vue-toastification';

const store = useCalendarStore();
const toast = useToast();

const calendarOptions = ref({
  plugins: [dayGridPlugin, timeGridPlugin, interactionPlugin],
  initialView: 'dayGridMonth',
  headerToolbar: {
    left: 'prev,next today',
    center: 'title',
    right: 'dayGridMonth,timeGridWeek',
  },
  editable: true,
  droppable: true,
  events: [] as Array<{ id: string; title: string; start: string; backgroundColor: string; borderColor: string }>,

  eventDrop: async (info: { event: { id: string; start: Date | null }; revert: () => void }) => {
    if (!info.event.start) {
      info.revert();
      return;
    }
    try {
      await store.updatePostDate(info.event.id, info.event.start.toISOString());
      toast.success('Post rescheduled');
    } catch {
      info.revert();
      toast.error('Failed to reschedule post');
    }
  },

  drop: async (info: {
    draggedEl: HTMLElement;
    date: Date | null;
  }) => {
    const id = info.draggedEl.getAttribute('data-post-id');
    if (!id || !info.date) return;
    try {
      await store.updatePostDate(id, info.date.toISOString());
      toast.success('Post scheduled');
      info.draggedEl.parentNode?.removeChild(info.draggedEl);
    } catch {
      toast.error('Failed to schedule post');
    }
  },
});

watch(
  () => store.scheduled,
  (events) => {
    calendarOptions.value.events = events.map((p) => ({
      id: p.id,
      title: p.content.length > 20 ? `${p.content.slice(0, 20)}…` : p.content,
      start: p.scheduledDate,
      backgroundColor:
        p.status === 'Published' ? '#14b8a6' : p.status === 'Failed' ? '#ef4444' : '#0ea5e9',
      borderColor: 'transparent',
    }));
  },
  { immediate: true, deep: true },
);

let draggable: Draggable | null = null;

onMounted(() => {
  store.fetchPosts();
  const containerEl = document.getElementById('external-events');
  if (containerEl) {
    draggable = new Draggable(containerEl, {
      itemSelector: '.post-drag',
      eventData: (eventEl) => ({
        id: eventEl.getAttribute('data-post-id'),
        title: eventEl.querySelector('p')?.textContent ?? 'Post',
      }),
    });
  }
});

onUnmounted(() => {
  // Draggable binds Sortable.js handlers to the container; without explicit
  // destruction the container is leaked when the view is unmounted.
  draggable?.destroy();
  draggable = null;
});
</script>

<style>
.calendar-container .fc-theme-standard td,
.calendar-container .fc-theme-standard th,
.calendar-container .fc-theme-standard .fc-scrollgrid {
  border-color: rgba(51, 65, 85, 0.5);
}
.calendar-container .fc-col-header-cell-cushion,
.calendar-container .fc-daygrid-day-number {
  color: #cbd5e1;
}
.calendar-container .fc-button-primary {
  background-color: rgba(14, 165, 233, 0.8) !important;
  border-color: transparent !important;
}
.calendar-container .fc-button-primary:hover {
  background-color: rgb(14, 165, 233) !important;
}
.calendar-container .fc-day-today {
  background-color: rgba(14, 165, 233, 0.1) !important;
}
</style>
