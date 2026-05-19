import { defineStore } from 'pinia';
import api from '../services/api';
import * as signalR from '@microsoft/signalr';

export const useTrendsStore = defineStore('trends', {
  state: () => ({
    trends: [] as any[],
    connection: null as signalR.HubConnection | null,
    loading: false
  }),
  actions: {
    async fetchCurrentTrends() {
      this.loading = true;
      try {
        const response = await api.get('/api/trends/current');
        this.trends = response.data.trends || [];
      } catch (error) {
        console.error('Error fetching trends:', error);
      } finally {
        this.loading = false;
      }
    },
    
    initSignalR() {
      if (this.connection) return;
      
      const baseUrl = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5000';
      
      this.connection = new signalR.HubConnectionBuilder()
        .withUrl(`${baseUrl}/trendshub`)
        .withAutomaticReconnect()
        .build();
        
      this.connection.on('TrendsUpdated', (event: any) => {
        const index = this.trends.findIndex(t => t.eventId === event.eventId);
        if (index > -1) {
          this.trends[index] = event;
        } else {
          this.trends.unshift(event);
          if (this.trends.length > 50) this.trends.pop();
        }
      });
      
      this.connection.start()
        .then(() => console.log('SignalR connected'))
        .catch(err => console.error('Error starting SignalR:', err));
    },
    
    stopSignalR() {
      if (this.connection) {
        this.connection.stop();
        this.connection = null;
      }
    }
  }
});
