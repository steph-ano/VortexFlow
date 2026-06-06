// Domain types for the VortexFlow frontend. These are the canonical
// representation of the data exchanged with the .NET API. Avoid `any` in
// the stores; the only place that touches raw backend payloads is the
// adapter functions in services/api.ts.

export interface TrendMetrics {
  volume: number;
  sentiment: number;
  engagement?: number;
}

export interface Trend {
  eventId: string;
  platform: string;
  hashtags: string[];
  metrics: TrendMetrics;
  timestamp: string;
}

export interface TrendListResponse {
  trends: Trend[];
}

export interface ScheduledPost {
  id: string;
  campaignId: string;
  content: string;
  platform: string;
  scheduledDate: string | null;
  status: 'Pending' | 'Published' | 'Failed' | 'Draft';
}

export interface Campaign {
  id: string;
  name: string;
  description: string;
}

export interface AuthUser {
  id: string;
  email: string;
  name: string;
  roles: string[];
}

export interface LoginResponse {
  accessToken: string;
  expiresAt: string;
  user: AuthUser;
}

export interface ApiError {
  title?: string;
  detail?: string;
  status?: number;
  errors?: Record<string, string[]>;
  correlationId?: string;
}
