<template>
  <main class="app-shell">
    <div v-if="shareFeedback" class="toast">
      {{ shareFeedback }}
    </div>
    <section v-if="!isConnected" class="grid">
      <CreateRoomForm :loading="isCreatingRoom" @submit="handleCreateRoom" />

      <section class="panel">
        <header class="panel__header">
          <div>
            <p class="eyebrow">{{ t('joinRoom.eyebrow') }}</p>
            <h2>{{ t('joinRoom.title') }}</h2>
          </div>
        </header>
        <form class="form-grid" @submit.prevent="handleJoinSubmit">
          <label>
            {{ t('joinRoom.roomIdLabel') }}
            <input v-model="roomIdInput" required :placeholder="t('joinRoom.roomIdPlaceholder')" />
          </label>
          <button type="submit" :disabled="joiningRoom">{{ t('joinRoom.submit') }}</button>
        </form>
        <p v-if="errorMessage" class="error">{{ errorMessage }}</p>
      </section>
    </section>

    <section v-else class="grid single">
      <MetronomeControls
        :room-state="roomState"
        :connection-status="connectionStatus"
        :drift-ms="driftMs"
        :is-busy="mutationBusy"
        :current-beat="currentBeat"
        :audio-ready="audioReady"
        :theme="theme"
        @start="handleStart"
        @stop="handleStop"
        @tempo-change="handleTempoChange"
        @toggle-audio="handleAudioToggle"
        @time-signature-change="handleTimeSignatureChange"
        @share="handleShareRoom"
        @change-instrument="handleChangeInstrument"
        @leave="handleLeaveRoom"
        @toggle-theme="toggleTheme"
      />
    </section>

    <ParticipantList v-if="isConnected" :participants="participants" />
    <p v-if="audioError" class="subtle-error">{{ audioError }}</p>
    <InstrumentModal
      v-if="isConnected && showInstrumentModal"
      :instruments="INSTRUMENTS"
      @select="handleInstrumentSelect"
    />
  </main>
</template>

<script setup lang="ts">
import { HubConnectionBuilder, HubConnectionState, LogLevel, type HubConnection } from '@microsoft/signalr';
import { nanoid } from 'nanoid';
import { computed, onBeforeUnmount, onMounted, ref, shallowRef, watch } from 'vue';
import CreateRoomForm from '@/components/CreateRoomForm.vue';
import MetronomeControls from '@/components/MetronomeControls.vue';
import ParticipantList from '@/components/ParticipantList.vue';
import InstrumentModal from '@/components/InstrumentModal.vue';
import { createRoom, fetchRoom } from '@/services/apiClient';
import { resolveHubUrl } from '@/config';
import { MetronomeEngine } from '@/services/metronomeEngine';
import { INSTRUMENTS, type InstrumentOption } from '@/constants/instruments';
import type {
  ClockSyncResponse,
  ConnectionStatus,
  MetronomeSyncPayload,
  Participant,
  RoomSnapshot,
  RoomState
} from '@/types';
import { useLocalization, type TranslationKey } from '@/localization';

type ThemeMode = 'dark' | 'light';
const THEME_STORAGE_KEY = 'clickband-theme';

const initialRoute = resolveInitialRoute();
const clientId = ref(nanoid());
const roomIdInput = ref(initialRoute.roomId);
const { t } = useLocalization();
const roomState = ref<RoomState | null>(null);
const participants = ref<Participant[]>([]);
const inviteUrl = ref<string | null>(null);
const connection = shallowRef<HubConnection | null>(null);
const connectionStatus = ref<ConnectionStatus>('idle');
const joiningRoom = ref(false);
const isCreatingRoom = ref(false);
const mutationBusy = ref(false);
const errorMessage = ref<string | null>(null);
const currentRoomId = ref<string | null>(roomIdInput.value || null);
const metronome = new MetronomeEngine();
const currentBeat = ref<number | null>(null);
const clockOffsetMs = ref(0);
const driftMs = ref<number | null>(null);
const audioReady = ref(false);
const audioErrorKey = ref<TranslationKey | null>(null);
const audioError = computed(() => (audioErrorKey.value ? t(audioErrorKey.value) : null));
const shareFeedback = ref<string | null>(null);
const isConnected = computed(() => roomState.value !== null);
const selectedInstrument = ref<InstrumentOption | null>(
  initialRoute.instrumentId
    ? INSTRUMENTS.find((option) => option.id === initialRoute.instrumentId) ?? null
    : null
);
const showInstrumentModal = ref(false);
const theme = ref<ThemeMode>(resolveStoredTheme());
watch(
  theme,
  (next) => {
    applyTheme(next);
    persistTheme(next);
  },
  { immediate: true }
);

const hubUrl = resolveHubUrl();

onMounted(() => {
  metronome.onBeat((beat) => (currentBeat.value = beat));
  audioReady.value = metronome.isAudioEnabled();
  if (currentRoomId.value) {
    joinRoom(currentRoomId.value);
  }
});

onBeforeUnmount(() => {
  metronome.stop();
  connection.value?.stop();
});

async function handleCreateRoom(payload: { tempoBpm: number; timeSignature: string }) {
  try {
    isCreatingRoom.value = true;
    const room = await createRoom(payload);
    inviteUrl.value = room.inviteUrl;
    roomIdInput.value = room.roomId;
    await joinRoom(room.roomId);
  } catch (error) {
    console.error(error);
    errorMessage.value = t('errors.createRoom');
  } finally {
    isCreatingRoom.value = false;
  }
}

async function handleJoinSubmit() {
  if (!roomIdInput.value) return;
  await joinRoom(roomIdInput.value);
}

async function joinRoom(roomId: string) {
  joiningRoom.value = true;
  errorMessage.value = null;
  try {
    console.info('[clickband] Attempting to join room', { roomId, clientId: clientId.value });
    await fetchRoom(roomId);
    const hub = await ensureConnection();

    const displayLabel = selectedInstrument.value?.label ?? t('participants.fallbackInstrument');
    await hub.invoke('JoinRoom', roomId, {
      clientId: clientId.value,
      displayName: displayLabel,
      capabilities: {
        supportsWebAudio: true,
        metadata: {
          userAgent: navigator.userAgent,
          instrument: selectedInstrument.value?.id ?? 'unknown'
        }
      }
    });
    currentRoomId.value = roomId;
    console.info('[clickband] room join invocation succeeded', { roomId });
    await performClockSync(roomId);
    if (!audioReady.value) {
      await ensureAudioReady();
    }
    if (!selectedInstrument.value) {
      showInstrumentModal.value = true;
    }
  } catch (error) {
    console.error('[clickband] Failed joining room', error);
    errorMessage.value = t('errors.joinRoom');
  } finally {
    joiningRoom.value = false;
  }
}

async function performClockSync(roomId: string) {
  if (!connection.value) return;
  const samples: number[] = [];
  for (let i = 0; i < 5; i += 1) {
    const sentAt = Date.now();
    const response = (await connection.value.invoke('PingServer', roomId, clientId.value, sentAt)) as ClockSyncResponse;
    const serverTime = new Date(response.serverTimestampUtc).getTime();
    const offset = serverTime - Date.now();
    samples.push(offset);
    await new Promise((resolve) => setTimeout(resolve, 120));
  }
  samples.sort((a, b) => a - b);
  const median = samples[Math.floor(samples.length / 2)];
  clockOffsetMs.value = median;
  driftMs.value = Math.abs(median);
  metronome.applyDriftCorrection(median);
  console.info('[clickband] Clock sync complete', { roomId, samples, offset: median });
}

function registerHandlers(hub: HubConnection) {
  hub.onreconnecting(() => (connectionStatus.value = 'reconnecting'));
  hub.onreconnected(() => (connectionStatus.value = 'connected'));
  hub.onclose(() => (connectionStatus.value = 'disconnected'));

  hub.on('RoomSnapshot', (snapshot: RoomSnapshot) => {
    roomState.value = snapshot.room;
    participants.value = snapshot.participants;
    inviteUrl.value = snapshot.inviteUrl;
    connectionStatus.value = 'connected';
    const self = snapshot.participants.find((p) => p.clientId === clientId.value);
    if (self?.instrumentId) {
      const match = INSTRUMENTS.find((instrument) => instrument.id === self.instrumentId);
      if (match) {
        selectedInstrument.value = match;
        showInstrumentModal.value = false;
      }
    } else if (!selectedInstrument.value) {
      showInstrumentModal.value = true;
    }
    console.info('[clickband] Received room snapshot', snapshot);
  });

  hub.on('ParticipantJoined', (participant: Participant) => {
    const exists = participants.value.some((p) => p.clientId === participant.clientId);
    if (!exists) {
      participants.value = [...participants.value, participant];
    }
    if (participant.clientId === clientId.value && participant.instrumentId) {
      const match = INSTRUMENTS.find((instrument) => instrument.id === participant.instrumentId);
      if (match) {
        selectedInstrument.value = match;
        showInstrumentModal.value = false;
      }
    }
    console.info('[clickband] Participant joined', participant);
  });

  hub.on('ParticipantLeft', (clientLeft: string) => {
    participants.value = participants.value.filter((p) => p.clientId !== clientLeft);
    console.info('[clickband] Participant left', clientLeft);
  });

  hub.on('MetronomeStart', (payload: MetronomeSyncPayload) => {
    metronome.start(payload, clockOffsetMs.value);
    currentBeat.value = 0;
    if (roomState.value) {
      roomState.value = {
        ...roomState.value,
        status: 1,
        scheduledStartAt: payload.startAtUtc,
        lastServerBeatTimestamp: payload.startAtUtc
      };
    }
    console.info('[clickband] Metronome starts', payload);
  });

  hub.on('MetronomeStop', (state: RoomState) => {
    metronome.stop();
    currentBeat.value = null;
    roomState.value = state;
    console.info('[clickband] Metronome stopped', state);
  });

  hub.on('TempoChanged', (state: RoomState) => {
    roomState.value = state;
    console.info('[clickband] Tempo changed', state);
  });

  hub.on('TimeSignatureChanged', (state: RoomState) => {
    roomState.value = state;
    console.info('[clickband] Time signature changed', state);
  });

  hub.on('ParticipantUpdated', (participant: Participant) => {
    participants.value = participants.value.map((existing) =>
      existing.clientId === participant.clientId ? participant : existing
    );
    if (participant.clientId === clientId.value && participant.instrumentId) {
      const match = INSTRUMENTS.find((instrument) => instrument.id === participant.instrumentId);
      if (match) {
        selectedInstrument.value = match;
        showInstrumentModal.value = false;
      }
    }
  });
}

async function ensureConnection(): Promise<HubConnection> {
  if (!connection.value) {
    const hub = new HubConnectionBuilder()
      .withUrl(hubUrl)
      .withAutomaticReconnect()
      .configureLogging(import.meta.env.DEV ? LogLevel.Information : LogLevel.Warning)
      .build();
    registerHandlers(hub);
    connection.value = hub;
  }

  if (connection.value.state === HubConnectionState.Disconnected) {
    connectionStatus.value = 'connecting';
    console.info('[clickband] Starting SignalR connection', { hubUrl });
    await connection.value.start();
    connectionStatus.value = 'connected';
    console.info('[clickband] SignalR connection established', { connectionId: connection.value.connectionId });
  }

  return connection.value;
}

async function handleStart() {
  if (!currentRoomId.value || !connection.value) return;
  mutationBusy.value = true;
  try {
    await ensureAudioReady();
    console.info('[clickband] Requesting metronome start', { roomId: currentRoomId.value });
    await connection.value.invoke('RequestMetronomeStart', currentRoomId.value);
  } finally {
    mutationBusy.value = false;
  }
}

async function handleStop() {
  if (!currentRoomId.value || !connection.value) return;
  mutationBusy.value = true;
  try {
    console.info('[clickband] Requesting metronome stop', { roomId: currentRoomId.value });
    await connection.value.invoke('RequestMetronomeStop', currentRoomId.value);
  } finally {
    mutationBusy.value = false;
  }
}

async function handleTempoChange(tempo: number) {
  if (!currentRoomId.value || !connection.value) return;
  mutationBusy.value = true;
  try {
    console.info('[clickband] Requesting tempo change', { roomId: currentRoomId.value, tempo });
    await connection.value.invoke('RequestTempoChange', currentRoomId.value, tempo);
  } finally {
    mutationBusy.value = false;
  }
}

async function ensureAudioReady(): Promise<boolean> {
  if (audioReady.value) {
    return true;
  }
  const enabled = await metronome.enableAudio();
  audioReady.value = enabled;
  audioErrorKey.value = enabled ? null : 'audio.enablePrompt';
  return enabled;
}

async function handleAudioToggle(enabled: boolean) {
  if (enabled) {
    await ensureAudioReady();
  } else {
    metronome.disableAudio();
    audioReady.value = false;
  }
}

async function handleTimeSignatureChange(signature: string) {
  if (!currentRoomId.value || !connection.value) return;
  mutationBusy.value = true;
  try {
    console.info('[clickband] Requesting time signature change', { roomId: currentRoomId.value, signature });
    await connection.value.invoke('RequestTimeSignatureChange', currentRoomId.value, signature);
  } catch (error) {
    console.error('[clickband] Time signature change failed', error);
  } finally {
    mutationBusy.value = false;
  }
}

async function handleLeaveRoom() {
  if (!currentRoomId.value) {
    return;
  }

  try {
    if (connection.value) {
      await connection.value.invoke('LeaveRoom', currentRoomId.value);
    }
  } catch (error) {
    console.warn('[clickband] Failed leaving room', error);
  }

  metronome.stop();
  roomState.value = null;
  participants.value = [];
  currentRoomId.value = null;
  inviteUrl.value = null;
  roomIdInput.value = '';
  connectionStatus.value = 'idle';
  currentBeat.value = null;
  driftMs.value = null;
  selectedInstrument.value = null;
  joiningRoom.value = false;

  if (typeof window !== 'undefined') {
    const basePath = window.location.pathname.includes('/rooms') ? '/' : window.location.pathname;
    window.history.replaceState({}, '', basePath);
  }
}

async function handleShareRoom() {
  if (!roomState.value) return;
  const shareLink = getShareLink(roomState.value.roomId);
  try {
    if (navigator.clipboard?.writeText) {
      await navigator.clipboard.writeText(shareLink);
    } else {
      const textarea = document.createElement('textarea');
      textarea.value = shareLink;
      textarea.style.position = 'fixed';
      textarea.style.opacity = '0';
      document.body.appendChild(textarea);
      textarea.focus();
      textarea.select();
      document.execCommand('copy');
      document.body.removeChild(textarea);
    }
    shareFeedback.value = t('share.success');
  } catch (error) {
    console.error('[clickband] Clipboard copy failed', error);
    shareFeedback.value = t('share.error');
  }

  window.setTimeout(() => (shareFeedback.value = null), 4000);
}

function handleChangeInstrument() {
  showInstrumentModal.value = true;
}

function getShareLink(roomId: string): string {
  if (inviteUrl.value) {
    return inviteUrl.value;
  }
  const origin = window.location.origin.replace(/\/$/, '');
  const params = new URLSearchParams();
  params.set('roomId', roomId);
  if (selectedInstrument.value) {
    params.set('instrument', selectedInstrument.value.id);
  }
  const query = params.toString();
  return `${origin}/rooms${query ? `?${query}` : ''}`;
}

async function handleInstrumentSelect(option: InstrumentOption) {
  selectedInstrument.value = option;
  showInstrumentModal.value = false;
  if (connection.value && currentRoomId.value) {
    try {
      await connection.value.invoke('SetInstrument', option.id, option.label);
    } catch (error) {
      console.error('[clickband] Failed to update instrument', error);
    }
  }
}

function toggleTheme() {
  theme.value = theme.value === 'dark' ? 'light' : 'dark';
}

function applyTheme(mode: ThemeMode) {
  if (typeof document === 'undefined') return;
  document.documentElement.setAttribute('data-theme', mode);
}

function persistTheme(mode: ThemeMode) {
  if (typeof window === 'undefined') return;
  window.localStorage.setItem(THEME_STORAGE_KEY, mode);
}

function resolveStoredTheme(): ThemeMode {
  if (typeof window === 'undefined') {
    return 'dark';
  }
  const stored = window.localStorage.getItem(THEME_STORAGE_KEY);
  return stored === 'light' || stored === 'dark' ? stored : 'dark';
}

function resolveInitialRoute(): { roomId: string; instrumentId?: string } {
  if (typeof window === 'undefined') {
    return { roomId: '' };
  }
  const parts = window.location.pathname.split('/').filter(Boolean);
  const roomsIndex = parts.indexOf('rooms');
  const params = new URLSearchParams(window.location.search);
  let roomId = roomsIndex !== -1 && parts[roomsIndex + 1] ? parts[roomsIndex + 1] : '';
  if (!roomId) {
    roomId = params.get('roomId') ?? '';
  }
  const instrumentId = params.get('instrument') ?? undefined;
  return { roomId, instrumentId };
}
</script>

<style scoped>
.app-shell {
  padding: 2.5rem 1.25rem;
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
  max-width: 640px;
  margin: 0 auto;
}

.grid {
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
}

.grid.single {
  flex-direction: column;
}

.panel {
  background: linear-gradient(160deg, var(--panel-bg), var(--panel-highlight));
  border-radius: 1.5rem;
  padding: 1.75rem;
  box-shadow: var(--shadow-elevation);
  border: 1px solid var(--panel-border-soft);
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
}

.panel__header {
  display: flex;
  flex-direction: column;
  gap: 0.35rem;
  color: var(--text-primary);
}

.eyebrow {
  text-transform: uppercase;
  letter-spacing: 0.08em;
  font-size: 0.75rem;
  color: var(--text-muted);
}

.form-grid {
  display: grid;
  gap: 0.75rem;
}

.error {
  color: var(--text-primary);
  background: var(--error-bg);
  padding: 0.85rem 1.1rem;
  border-radius: 1rem;
  border: 1px solid var(--error-border);
}

.subtle-error {
  color: var(--warning);
  background: var(--warning-bg);
  padding: 0.85rem 1.1rem;
  border-radius: 1rem;
  border: 1px solid var(--warning-border);
}

.toast {
  position: fixed;
  top: 1.25rem;
  left: 50%;
  transform: translateX(-50%);
  z-index: 2000;
  background: var(--success-bg);
  color: var(--text-primary);
  border: 1px solid var(--success-border);
  border-radius: 999px;
  padding: 0.65rem 1.5rem;
  box-shadow: var(--shadow-elevation);
  min-width: 240px;
  text-align: center;
  letter-spacing: 0.04em;
}

@media (max-width: 640px) {
  .app-shell {
    padding: 1rem;
  }

  .grid {
    gap: 1rem;
  }
}
</style>
