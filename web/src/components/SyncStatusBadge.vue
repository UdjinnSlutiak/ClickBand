<template>
  <span class="sync-badge" :data-state="status">
    {{ statusLabel }}
    <small v-if="driftLabel">&middot; {{ driftLabel }}</small>
  </span>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import type { ConnectionStatus } from '@/types';
import { useLocalization, type TranslationKey } from '@/localization';

const props = defineProps<{
  status: ConnectionStatus;
  driftMs?: number | null;
}>();

const statusKeys: Record<ConnectionStatus, TranslationKey> = {
  idle: 'syncStatus.idle',
  connecting: 'syncStatus.connecting',
  connected: 'syncStatus.connected',
  reconnecting: 'syncStatus.reconnecting',
  disconnected: 'syncStatus.disconnected'
};

const { t } = useLocalization();

const statusLabel = computed(() => t(statusKeys[props.status]));
const driftLabel = computed(() =>
  typeof props.driftMs === 'number' ? t('syncStatus.drift', { value: props.driftMs.toFixed(1) }) : ''
);
</script>

<style scoped>
.sync-badge {
  display: inline-flex;
  align-items: center;
  gap: 0.35rem;
  font-size: 0.85rem;
  font-weight: 600;
  padding: 0.35rem 0.75rem;
  border-radius: 999px;
  background: var(--badge-default-bg);
  color: var(--accent-strong);
  border: 1px solid var(--badge-default-border);
}

.sync-badge[data-state='connected'] {
  background: var(--badge-connected-bg);
  color: var(--success);
  border-color: var(--badge-connected-border);
}

.sync-badge[data-state='reconnecting'],
.sync-badge[data-state='connecting'] {
  background: var(--badge-warning-bg);
  color: var(--warning);
  border-color: var(--badge-warning-border);
}

.sync-badge[data-state='disconnected'] {
  background: var(--badge-danger-bg);
  color: var(--danger);
  border-color: var(--badge-danger-border);
}

small {
  font-weight: 500;
  color: inherit;
}
</style>
