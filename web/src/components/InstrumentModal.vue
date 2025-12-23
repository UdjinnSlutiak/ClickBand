<template>
  <div class="modal-backdrop">
    <div class="modal">
      <header>
        <p class="eyebrow">{{ t('instrumentModal.eyebrow') }}</p>
        <h2>{{ t('instrumentModal.title') }}</h2>
        <p class="hint">{{ t('instrumentModal.hint') }}</p>
      </header>

      <div class="tiles">
        <button
          v-for="instrument in instruments"
          :key="instrument.id"
          type="button"
          class="tile"
          @click="$emit('select', instrument)"
        >
          <div class="icon-chip" :style="{ background: instrument.color, color: instrument.textColor || '#f7e7c2' }">
            <Icon :icon="instrument.icon" height="28" />
          </div>
          <strong>{{ instrument.label }}</strong>
        </button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { Icon } from '@iconify/vue';
import type { InstrumentOption } from '@/constants/instruments';
import { useLocalization } from '@/localization';

defineProps<{
  instruments: InstrumentOption[];
}>();

defineEmits<{
  select: [InstrumentOption];
}>();

const { t } = useLocalization();
</script>

<style scoped>
.modal-backdrop {
  position: fixed;
  inset: 0;
  background: var(--modal-backdrop);
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 1rem;
  z-index: 1000;
}

.modal {
  background: linear-gradient(150deg, var(--panel-bg), var(--panel-highlight));
  border-radius: 1.5rem;
  max-width: 560px;
  width: 100%;
  padding: 1.75rem;
  display: flex;
  flex-direction: column;
  gap: 1.25rem;
  box-shadow: var(--shadow-elevation);
  border: 1px solid var(--panel-border-strong);
}

header .eyebrow {
  text-transform: uppercase;
  letter-spacing: 0.08em;
  font-size: 0.75rem;
  color: var(--text-muted);
  margin: 0;
}

header h2 {
  margin: 0.25rem 0;
}

.hint {
  margin: 0;
  color: var(--text-muted);
}

.tiles {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(140px, 1fr));
  gap: 0.85rem;
}

.tile {
  border: 1px solid var(--tile-border);
  border-radius: 1rem;
  padding: 0.85rem;
  background: var(--tile-bg);
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: 0.35rem;
  text-align: left;
  cursor: pointer;
  transition: border-color 0.15s ease, background 0.15s ease, transform 0.15s ease;
  text-transform: none;
  letter-spacing: normal;
  color: var(--text-primary);
  box-shadow: var(--tile-shadow);
}

.tile:hover {
  border-color: var(--accent-strong);
  background: var(--tile-hover);
  transform: translateY(-2px);
}

.tile strong {
  font-size: 1rem;
  color: var(--text-primary);
}

.icon-chip {
  width: 48px;
  height: 48px;
  border-radius: 0.75rem;
  display: flex;
  align-items: center;
  justify-content: center;
  box-shadow: var(--chip-shadow);
  border: 1px solid var(--chip-border);
}
</style>
