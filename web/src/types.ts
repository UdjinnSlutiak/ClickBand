export interface RoomState {
  roomId: string;
  tempoBpm: number;
  timeSignature: string;
  status: number;
  createdAt: string;
  lastUpdatedAt: string;
  scheduledStartAt?: string;
  lastServerBeatTimestamp?: string;
  createdBy?: string;
}

export interface Participant {
  clientId: string;
  displayName: string;
  joinedAt: string;
  instrumentId?: string;
  capabilities?: Record<string, string>;
}

export interface RoomSnapshot {
  room: RoomState;
  participants: Participant[];
  inviteUrl: string;
}

export interface RoomResponsePayload {
  roomId: string;
  tempoBpm: number;
  timeSignature: string;
  status: number;
  createdAt: string;
  lastUpdatedAt: string;
  scheduledStartAt?: string;
  lastServerBeatTimestamp?: string;
  createdBy?: string;
  inviteUrl: string;
  participants: Participant[];
}

export interface MetronomeSyncPayload {
  roomId: string;
  tempoBpm: number;
  beatIntervalMs: number;
  serverTimestampUtc: string;
  startAtUtc: string;
  timeSignature?: string;
}

export interface ClockSyncResponse {
  serverTimestampUtc: string;
  maxDriftMs: number;
}

export type ConnectionStatus = 'idle' | 'connecting' | 'connected' | 'reconnecting' | 'disconnected';
