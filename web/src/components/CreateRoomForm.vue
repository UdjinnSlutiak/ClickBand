<template>
  <section class="panel">
    <header class="panel__header">
      <div>
        <p class="eyebrow">{{ t('createRoom.eyebrow') }}</p>
        <h2>{{ t('createRoom.title') }}</h2>
      </div>
      <label class="locale-picker">
        <span>{{ t('locale.label') }}</span>
        <select v-model="localeModel">
          <option v-for="option in localeOptions" :key="option.value" :value="option.value">
            {{ option.label }}
          </option>
        </select>
      </label>
    </header>

    <form class="form-grid" @submit.prevent="emitSubmit">
      <label>
        {{ t('createRoom.tempoLabel') }}
        <input v-model.number="tempo" type="number" min="40" max="240" required />
      </label>

      <label>
        {{ t('createRoom.timeSignatureLabel') }}
        <select v-model="timeSignature">
          <option v-for="signature in timeSignatures" :key="signature" :value="signature">
            {{ signature }}
          </option>
        </select>
      </label>

      <button type="submit" :disabled="loading">{{ t('createRoom.submit') }}</button>
    </form>

  </section>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue';
import { useLocalization, type Locale } from '@/localization';

const props = defineProps<{
  loading: boolean;
}>();

const emit = defineEmits<{
  submit: [{ tempoBpm: number; timeSignature: string }];
}>();

import { TIME_SIGNATURES } from '@/constants/timeSignatures';

const tempo = ref(120);
const timeSignature = ref(TIME_SIGNATURES[0]);
const timeSignatures = TIME_SIGNATURES;
const { t, locale, setLocale, localeOptions } = useLocalization();
const localeModel = computed<Locale>({
  get: () => locale.value,
  set: (value) => setLocale(value)
});

function emitSubmit() {
  emit('submit', {
    tempoBpm: tempo.value,
    timeSignature: timeSignature.value
  });
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

.panel__header > div {
  flex: 1 1 auto;
}

.eyebrow {
  text-transform: uppercase;
  letter-spacing: 0.08em;
  font-size: 0.75rem;
  color: var(--text-muted);
}

.locale-picker {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
  font-size: 0.75rem;
  font-weight: 600;
  color: var(--text-muted);
}

.locale-picker select {
  background: var(--surface-button);
  border-radius: 0.75rem;
  border: 1px solid var(--surface-button-border);
  color: var(--text-primary);
  padding: 0.35rem 0.75rem;
  font-weight: 600;
}

.form-grid {
  display: grid;
  gap: 1rem;
}

label {
  display: flex;
  flex-direction: column;
  gap: 0.35rem;
  font-weight: 600;
  color: var(--text-primary);
}
</style>
