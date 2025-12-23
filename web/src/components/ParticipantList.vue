<template>
  <section class="panel">
    <header class="panel__header">
      <div>
        <p class="eyebrow">{{ t('participants.eyebrow') }}</p>
        <h2>{{ participants.length }}</h2>
      </div>
    </header>

    <ul v-if="participants.length" class="participant-list">
      <li v-for="participant in participants" :key="participant.clientId">
        <div class="avatar" :style="getAvatarStyle(participant)">
          <Icon :icon="getInstrument(participant)?.icon || defaultIcon" height="22" />
        </div>
        <div>
          <p class="name">{{ participant.displayName }}</p>
          <p class="time">{{ t('participants.joined', { time: formatRelative(participant.joinedAt) }) }}</p>
        </div>
      </li>
    </ul>
    <p v-else class="empty">{{ t('participants.empty') }}</p>
  </section>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import { Icon } from '@iconify/vue';
import { formatDistanceToNow } from 'date-fns';
import { enUS, uk as ukLocale } from 'date-fns/locale';
import { INSTRUMENTS, type InstrumentOption } from '@/constants/instruments';
import type { Participant } from '@/types';
import { useLocalization } from '@/localization';

const props = defineProps<{
  participants: Participant[];
}>();

const defaultIcon = 'mdi:music-note';
const instrumentLookup = computed(() =>
  Object.fromEntries(INSTRUMENTS.map((instrument) => [instrument.id, instrument]))
);
const { t, locale } = useLocalization();
const relativeLocale = computed(() => (locale.value === 'uk' ? ukLocale : enUS));

function formatRelative(value: string): string {
  return formatDistanceToNow(new Date(value), { addSuffix: true, locale: relativeLocale.value });
}

function getInstrument(participant: Participant): InstrumentOption | undefined {
  const id =
    participant.instrumentId ??
    participant.capabilities?.instrument ??
    participant.capabilities?.Instrument;
  if (!id) {
    return undefined;
  }
  return instrumentLookup.value[id];
}

function getAvatarStyle(participant: Participant) {
  const instrument = getInstrument(participant);
  if (!instrument) {
    return {
      background: 'var(--avatar-fallback-gradient)',
      color: 'var(--avatar-fallback-text)'
    };
  }
  return {
    background: instrument.color,
    color: instrument.textColor || 'var(--avatar-fallback-text)'
  };
}
</script>

<style scoped>
.panel {
  background: linear-gradient(160deg, var(--panel-bg), var(--panel-highlight));
  border-radius: 1.5rem;
  padding: 1.75rem;
  box-shadow: var(--shadow-elevation);
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
  border: 1px solid var(--panel-border-soft);
}

.panel__header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 1rem;
  color: var(--text-primary);
}

.eyebrow {
  text-transform: uppercase;
  letter-spacing: 0.08em;
  font-size: 0.75rem;
  color: var(--text-muted);
  margin-bottom: 0.25rem;
}

.participant-list {
  list-style: none;
  padding: 0;
  margin: 0;
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.participant-list li {
  display: flex;
  align-items: center;
  gap: 0.75rem;
}

.avatar {
  width: 46px;
  height: 46px;
  border-radius: 999px;
  display: grid;
  place-items: center;
  font-weight: 700;
  box-shadow: var(--participant-avatar-shadow);
  border: 1px solid var(--participant-avatar-border);
}

.name {
  font-weight: 600;
}

.time {
  color: var(--text-muted);
  font-size: 0.85rem;
}

.empty {
  color: var(--text-muted);
}
</style>
