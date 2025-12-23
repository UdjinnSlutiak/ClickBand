<template>
  <section class="panel">
    <header class="panel__header">
      <div>
        <p class="eyebrow">{{ t('metronome.eyebrow') }}</p>
        <h2>{{ formatRoomId(roomState?.roomId) }}</h2>
      </div>
      <div class="panel__header-actions">
        <label class="locale-picker">
          <span>{{ t('locale.label') }}</span>
          <select v-model="localeModel">
            <option v-for="option in localeOptions" :key="option.value" :value="option.value">
              {{ option.label }}
            </option>
          </select>
        </label>
        <button type="button" class="theme-toggle" @click="$emit('toggle-theme')">
          <span class="theme-toggle__icon" aria-hidden="true">{{ themeIcon }}</span>
          {{ themeLabel }}
        </button>
        <SyncStatusBadge :status="connectionStatus" :drift-ms="driftMs" />
      </div>
    </header>

    <div v-if="roomState" class="controls-grid">
      <div class="tempo-card">
        <p class="eyebrow">{{ t('metronome.tempoLabel') }}</p>
        <div class="tempo">
          <strong>{{ bpmModel }}</strong>
          <span>{{ t('metronome.bpmLabel') }}</span>
        </div>
        <div class="tempo-controls">
          <button
            type="button"
            class="tempo-step"
            :aria-label="t('metronome.decreaseTempo')"
            @click="decrementTempo"
            :disabled="bpmModel <= TEMPO_MIN"
          >
            -
          </button>
          <input v-model.number="bpmModel" type="range" :min="TEMPO_MIN" :max="TEMPO_MAX" :step="TEMPO_STEP" />
          <button
            type="button"
            class="tempo-step"
            :aria-label="t('metronome.increaseTempo')"
            @click="incrementTempo"
            :disabled="bpmModel >= TEMPO_MAX"
          >
            +
          </button>
        </div>
        <button class="ghost" :disabled="isBusy || bpmModel === roomState.tempoBpm" @click="$emit('tempo-change', bpmModel)">
          {{ t('metronome.applyTempo') }}
        </button>
      </div>

      <div class="signature-card">
        <p class="eyebrow">{{ t('metronome.timeSignatureLabel') }}</p>
        <select v-model="timeSignatureModel" :disabled="isBusy" @change="$emit('time-signature-change', timeSignatureModel)">
          <option v-for="signature in timeSignatures" :key="signature" :value="signature">
            {{ signature }}
          </option>
        </select>
        <p class="beat" v-if="(currentBeat ?? 0) > 0">
          {{ t('metronome.beatLabel', { count: currentBeat ?? 0 }) }}
        </p>
      </div>

      <div class="actions">
        <button :disabled="isBusy || roomState.status === 1" @click="$emit('start')">
          {{ t('metronome.start') }}
        </button>
        <button class="ghost" :disabled="isBusy || roomState.status === 0" @click="$emit('stop')">
          {{ t('metronome.stop') }}
        </button>
        <div class="sound-toggle">
          <label class="switch">
            <input type="checkbox" :checked="audioReady" @change="emitSoundToggle" />
            <span class="slider" />
          </label>
          <span>{{ audioReady ? t('metronome.soundEnabled') : t('metronome.enableSound') }}</span>
        </div>
      </div>

      <div class="secondary-actions">
        <button class="secondary" type="button" @click="$emit('share')">
          {{ t('metronome.shareRoom') }}
        </button>
        <button class="secondary" type="button" @click="$emit('change-instrument')">
          {{ t('metronome.changeInstrument') }}
        </button>
        <button class="danger" type="button" @click="$emit('leave')">
          {{ t('metronome.leaveRoom') }}
        </button>
      </div>
    </div>

    <p v-else class="empty">{{ t('metronome.emptyState') }}</p>
  </section>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import type { ConnectionStatus, RoomState } from '@/types';
import { TIME_SIGNATURES } from '@/constants/timeSignatures';
import SyncStatusBadge from './SyncStatusBadge.vue';
import { useLocalization, type Locale } from '@/localization';

type ThemeMode = 'dark' | 'light';

const props = defineProps<{
  roomState: RoomState | null;
  connectionStatus: ConnectionStatus;
  driftMs: number | null;
  isBusy: boolean;
  currentBeat?: number | null;
  audioReady: boolean;
  theme: ThemeMode;
}>();

const emit = defineEmits<{
  start: [];
  stop: [];
  'tempo-change': [number];
  'toggle-audio': [boolean];
  share: [];
  'change-instrument': [];
  leave: [];
  'time-signature-change': [string];
  'toggle-theme': [];
}>();

const TEMPO_MIN = 40;
const TEMPO_MAX = 240;
const TEMPO_STEP = 1;

const bpmModel = ref(props.roomState?.tempoBpm ?? 120);
const timeSignatureModel = ref(props.roomState?.timeSignature ?? TIME_SIGNATURES[0]);
const timeSignatures = TIME_SIGNATURES;
const { t, locale, setLocale, localeOptions } = useLocalization();
const localeModel = computed<Locale>({
  get: () => locale.value,
  set: (value) => setLocale(value)
});
const themeLabel = computed(() => (props.theme === 'dark' ? t('theme.light') : t('theme.dark')));
const themeIcon = computed(() => (props.theme === 'dark' ? '☀' : '☾'));

watch(
  () => props.roomState?.tempoBpm,
  (next) => {
    if (typeof next === 'number') {
      bpmModel.value = next;
    }
  }
);

watch(
  () => props.roomState?.timeSignature,
  (next) => {
    if (typeof next === 'string' && next.length > 0) {
      timeSignatureModel.value = next;
    }
  }
);

function formatRoomId(id?: string | null): string {
  if (!id) {
    return t('metronome.noRoom');
  }
  if (id.length <= 8) {
    return id;
  }
  return `${id.slice(0, 4)}…${id.slice(-4)}`;
}
function emitSoundToggle(event: Event) {
  const target = event.target as HTMLInputElement;
  emit('toggle-audio', target.checked);
}

function adjustTempo(delta: number) {
  bpmModel.value = Math.min(TEMPO_MAX, Math.max(TEMPO_MIN, bpmModel.value + delta));
}

function incrementTempo() {
  adjustTempo(TEMPO_STEP);
}

function decrementTempo() {
  adjustTempo(-TEMPO_STEP);
}
</script>

<style scoped>
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
  justify-content: space-between;
  align-items: flex-start;
  gap: 1rem;
  color: var(--text-primary);
  flex-wrap: wrap;
}

.panel__header-actions {
  display: grid;
  flex-wrap: wrap;
  gap: 0.5rem;
  align-items: flex-start;
  justify-content: flex-end;
}

.theme-toggle {
  padding: 0.35rem 0.85rem;
  font-size: 0.85rem;
  letter-spacing: 0.05em;
  text-transform: none;
  background: var(--ghost-bg);
  border-color: var(--ghost-border);
  box-shadow: var(--ghost-shadow);
  display: inline-flex;
  align-items: center;
  gap: 0.35rem;
}

.theme-toggle__icon {
  font-size: 1rem;
}

.eyebrow {
  text-transform: uppercase;
  letter-spacing: 0.08em;
  font-size: 0.75rem;
  color: var(--text-muted);
  margin-bottom: 0.25rem;
}

.controls-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
  gap: 1.5rem;
}

.tempo {
  display: flex;
  align-items: baseline;
  gap: 0.35rem;
  font-size: 1.5rem;
}

.tempo strong {
  font-size: 3rem;
}

.tempo-controls {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  margin: 0.75rem 0;
}

.tempo-controls input[type='range'] {
  flex: 1;
  accent-color: var(--accent);
}

.tempo-step {
  width: 44px;
  height: 44px;
  padding: 0;
  border-radius: 50%;
  background: var(--surface-button);
  color: var(--text-primary);
  border: 1px solid var(--surface-button-border);
  font-size: 1.5rem;
  line-height: 1;
  text-transform: none;
  letter-spacing: normal;
  box-shadow: var(--surface-button-shadow);
}

.tempo-step:disabled {
  background: var(--surface-button-disabled);
  color: var(--surface-button-disabled-text);
  border-color: var(--surface-button-border-muted);
}

.signature-card {
  padding: 1rem;
  border-radius: 1rem;
  background: var(--surface-glow);
  border: 1px solid var(--surface-button-border-muted);
}

.beat {
  margin: 0;
  font-weight: 600;
  color: var(--text-primary);
}

.locale-picker {
  display: flex;
  flex-direction: column;
  gap: 0.15rem;
  font-size: 0.75rem;
  font-weight: 600;
  color: var(--text-muted);
}

.locale-picker select {
  background: var(--surface-button);
  border-radius: 0.75rem;
  border: 1px solid var(--surface-button-border);
  color: var(--text-primary);
  padding: 0.35rem 0.65rem;
  font-weight: 600;
}

.actions {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.secondary-actions {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
  margin-top: 0.5rem;
}

.ghost {
  background: var(--ghost-bg);
  color: var(--accent-strong);
  border: 1px solid var(--ghost-border);
  box-shadow: var(--ghost-shadow);
}

.sound-indicator {
  margin: 0;
  font-weight: 600;
  color: var(--success);
}
.sound-toggle {
  display: flex;
  align-items: center;
  gap: 0.65rem;
  font-weight: 600;
  color: var(--text-primary);
}

.switch {
  position: relative;
  display: inline-block;
  width: 46px;
  height: 24px;
}

.switch input {
  opacity: 0;
  width: 0;
  height: 0;
}

.slider {
  position: absolute;
  cursor: pointer;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background-color: var(--switch-track);
  transition: 0.2s;
  border-radius: 999px;
}

.slider:before {
  position: absolute;
  content: '';
  height: 18px;
  width: 18px;
  left: 3px;
  bottom: 3px;
  background-color: var(--switch-thumb);
  transition: 0.2s;
  border-radius: 50%;
}

.switch input:checked + .slider {
  background-color: var(--accent);
}

.switch input:checked + .slider:before {
  transform: translateX(22px);
}

.secondary {
  background: var(--secondary-bg);
  border: 1px dashed var(--secondary-border);
  color: var(--accent-strong);
}

.danger {
  background: var(--danger-button-bg);
  border: 1px solid var(--danger-button-border);
  color: var(--text-primary);
}

.empty {
  color: var(--text-muted);
}
</style>
