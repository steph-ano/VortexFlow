import { defineStore } from 'pinia';
import { apiClient } from '../services/api';
import type { ScheduledPost } from '../types';

interface CalendarState {
  posts: ScheduledPost[];
  loading: boolean;
  error: string | null;
}

export const useCalendarStore = defineStore('calendar', {
  state: (): CalendarState => ({
    posts: [],
    loading: false,
    error: null,
  }),
  getters: {
    unscheduled: (state) =>
      state.posts.filter((p) => !p.scheduledDate || p.status === 'Draft'),
    scheduled: (state) =>
      state.posts
        .filter((p) => !!p.scheduledDate && p.status !== 'Draft')
        .map((p) => ({
          id: p.id,
          content: p.content,
          platform: p.platform,
          scheduledDate: p.scheduledDate as string,
          status: p.status,
        })),
  },
  actions: {
    async fetchPosts() {
      this.loading = true;
      this.error = null;
      try {
        const { data } = await apiClient.posts.list();
        this.posts = Array.isArray(data) ? data : [];
      } catch (err: any) {
        this.error = err?.detail ?? 'Failed to load posts';
      } finally {
        this.loading = false;
      }
    },

    async updatePostDate(postId: string, newDate: string) {
      await apiClient.posts.reschedule(postId, newDate);
      const post = this.posts.find((p) => p.id === postId);
      if (post) post.scheduledDate = newDate;
    },
  },
});
