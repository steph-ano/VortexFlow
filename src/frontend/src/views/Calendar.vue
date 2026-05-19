<template>
  <div class="flex flex-col lg:flex-row gap-6 h-[calc(100vh-8rem)]">
    <!-- Panel lateral (Kanban) para posts sin programar o fallidos -->
    <GlassCard class="w-full lg:w-80 flex-shrink-0 flex flex-col h-full overflow-hidden">
      <h3 class="text-xl font-bold text-white mb-4">Unscheduled Posts</h3>
      <div id="external-events" class="flex-1 overflow-y-auto pr-2 space-y-3">
        <div v-for="post in unscheduledPosts" :key="post.id" 
             class="fc-event bg-dark-900/50 p-3 rounded-lg border border-slate-700/50 cursor-grab hover:bg-dark-700/50 transition-colors"
             :data-event="JSON.stringify({ id: post.id, title: post.content.substring(0,20) + '...' })">
          <p class="text-sm text-slate-200 line-clamp-2">{{ post.content }}</p>
          <div class="mt-2 flex justify-between items-center text-xs">
            <span class="text-slate-400">{{ post.platform }}</span>
            <span class="px-2 py-0.5 rounded-full bg-slate-800 text-slate-300">Pending</span>
          </div>
        </div>
        <div v-if="unscheduledPosts.length === 0" class="text-center text-slate-500 mt-8">
          No unscheduled posts
        </div>
      </div>
    </GlassCard>

    <!-- Calendario Principal -->
    <GlassCard class="flex-1 flex flex-col h-full overflow-hidden">
      <div class="h-full w-full calendar-container">
        <FullCalendar :options="calendarOptions" class="h-full" />
      </div>
    </GlassCard>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue';
import GlassCard from '../components/common/GlassCard.vue';
import FullCalendar from '@fullcalendar/vue3';
import dayGridPlugin from '@fullcalendar/daygrid';
import timeGridPlugin from '@fullcalendar/timegrid';
import interactionPlugin, { Draggable } from '@fullcalendar/interaction';
import { useCalendarStore } from '../stores/calendar';
import { useToast } from 'vue-toastification';

const store = useCalendarStore();
const toast = useToast();

const unscheduledPosts = computed(() => {
  return store.posts.filter(p => !p.scheduledDate || p.status === 'Draft');
});

const scheduledEvents = computed(() => {
  return store.posts
    .filter(p => p.scheduledDate && p.status !== 'Draft')
    .map(p => ({
      id: p.id,
      title: p.content.substring(0, 20) + '...',
      start: p.scheduledDate,
      backgroundColor: p.status === 'Published' ? '#14b8a6' : (p.status === 'Failed' ? '#ef4444' : '#0ea5e9'),
      borderColor: 'transparent'
    }));
});

const calendarOptions = ref({
  plugins: [dayGridPlugin, timeGridPlugin, interactionPlugin],
  initialView: 'dayGridMonth',
  headerToolbar: {
    left: 'prev,next today',
    center: 'title',
    right: 'dayGridMonth,timeGridWeek'
  },
  editable: true,
  droppable: true,
  events: scheduledEvents.value,
  
  eventDrop: async (info: any) => {
    try {
      await store.updatePostDate(info.event.id, info.event.start.toISOString());
      toast.success('Post rescheduled');
    } catch (err) {
      info.revert();
      toast.error('Failed to reschedule post');
    }
  },
  
  drop: async (info: any) => {
    const eventData = JSON.parse(info.draggedEl.getAttribute('data-event'));
    try {
      await store.updatePostDate(eventData.id, info.date.toISOString());
      toast.success('Post scheduled');
      info.draggedEl.parentNode.removeChild(info.draggedEl);
    } catch (err) {
      toast.error('Failed to schedule post');
    }
  }
});

watch(scheduledEvents, (newEvents) => {
  calendarOptions.value.events = newEvents;
});

onMounted(() => {
  store.fetchPosts();
  
  const containerEl = document.getElementById('external-events');
  if (containerEl) {
    new Draggable(containerEl, {
      itemSelector: '.fc-event',
      eventData: function(eventEl) {
        return JSON.parse(eventEl.getAttribute('data-event') || '{}');
      }
    });
  }
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
