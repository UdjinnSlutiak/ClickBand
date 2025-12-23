import axios from 'axios';
import { resolveApiBaseUrl } from '@/config';
import type { RoomResponsePayload } from '@/types';

const baseURL = resolveApiBaseUrl();

export const apiClient = axios.create({
  baseURL,
  timeout: 10000
});

export interface CreateRoomPayload {
  tempoBpm?: number;
  timeSignature?: string;
  requestedBy?: string;
}

export async function createRoom(payload: CreateRoomPayload): Promise<RoomResponsePayload> {
  const { data } = await apiClient.post<RoomResponsePayload>('/rooms', payload);
  return data;
}

export async function fetchRoom(roomId: string): Promise<RoomResponsePayload> {
  const { data } = await apiClient.get<RoomResponsePayload>(`/rooms/${roomId}`);
  return data;
}
