import { defineStore } from 'pinia';
import api from '../services/api';

export const useCalendarStore = defineStore('calendar', {
  state: () => ({
    posts: [] as any[],
    loading: false
  }),
  actions: {
    async fetchPosts() {
      this.loading = true;
      try {
        const response = await api.get('/api/campaigns/posts');
        this.posts = response.data || [];
      } catch (error) {
        console.error('Error fetching posts:', error);
      } finally {
        this.loading = false;
      }
    },
    async updatePostDate(postId: string, newDate: string) {
      try {
        await api.put(`/api/campaigns/posts/${postId}/reschedule`, { date: newDate });
        const post = this.posts.find(p => p.id === postId);
        if (post) post.scheduledDate = newDate;
      } catch (error) {
        throw error;
      }
    }
  }
});
