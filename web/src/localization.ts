import { computed, ref } from 'vue';

export type Locale = 'en' | 'uk';

const LOCALE_STORAGE_KEY = 'clickband-locale';

const EN_MESSAGES = {
  'locale.label': 'Language',
  'locale.enLabel': 'English',
  'locale.ukLabel': 'Українська',
  'createRoom.eyebrow': 'Create a room',
  'createRoom.title': 'Start a new session',
  'createRoom.tempoLabel': 'Tempo (BPM)',
  'createRoom.timeSignatureLabel': 'Time signature',
  'createRoom.submit': 'Create room',
  'joinRoom.eyebrow': 'Join room',
  'joinRoom.title': 'Connect to an invite',
  'joinRoom.roomIdLabel': 'Room ID',
  'joinRoom.roomIdPlaceholder': 'e.g. 12ab34cd',
  'joinRoom.submit': 'Join room',
  'metronome.eyebrow': 'Room',
  'metronome.tempoLabel': 'Tempo',
  'metronome.bpmLabel': 'BPM',
  'metronome.decreaseTempo': 'Decrease tempo',
  'metronome.increaseTempo': 'Increase tempo',
  'metronome.applyTempo': 'Apply tempo',
  'metronome.timeSignatureLabel': 'Time signature',
  'metronome.beatLabel': 'Beat {count}',
  'metronome.start': 'Start',
  'metronome.stop': 'Stop',
  'metronome.soundEnabled': 'Sound enabled',
  'metronome.enableSound': 'Enable sound',
  'metronome.shareRoom': 'Share room',
  'metronome.changeInstrument': 'Change instrument',
  'metronome.leaveRoom': 'Leave room',
  'metronome.emptyState': 'Join or create a room to control the metronome.',
  'metronome.noRoom': 'No room',
  'theme.light': 'Light theme',
  'theme.dark': 'Dark theme',
  'participants.eyebrow': 'Participants',
  'participants.joined': 'Joined {time}',
  'participants.empty': 'No one else is here yet.',
  'participants.fallbackInstrument': 'Musician',
  'instrumentModal.eyebrow': 'Who is playing?',
  'instrumentModal.title': 'Select your instrument',
  'instrumentModal.hint': 'This helps other participants know your role.',
  'share.success': 'Invite link copied to clipboard.',
  'share.error': 'Unable to copy link. Copy manually from the address bar.',
  'errors.createRoom': 'Unable to create room.',
  'errors.joinRoom': 'Failed to join room. Check the ID and try again.',
  'audio.enablePrompt': 'Sound requires a user gesture. Tap "Enable sound" to allow audio playback.',
  'syncStatus.idle': 'Idle',
  'syncStatus.connecting': 'Connecting…',
  'syncStatus.connected': 'Synced',
  'syncStatus.reconnecting': 'Reconnecting…',
  'syncStatus.disconnected': 'Disconnected',
  'syncStatus.drift': 'drift {value} ms'
} as const;

export type TranslationKey = keyof typeof EN_MESSAGES;

type TranslationMap = Record<Locale, Record<TranslationKey, string>>;

const TRANSLATIONS: TranslationMap = {
  en: EN_MESSAGES,
  uk: {
    'locale.label': 'Мова',
    'locale.enLabel': 'Англійська',
    'locale.ukLabel': 'Українська',
    'createRoom.eyebrow': 'Створити кімнату',
    'createRoom.title': 'Почати нову сесію',
    'createRoom.tempoLabel': 'Темп (BPM)',
    'createRoom.timeSignatureLabel': 'Розмір',
    'createRoom.submit': 'Створити',
    'joinRoom.eyebrow': 'Приєднатися',
    'joinRoom.title': 'Підключитися за запрошенням',
    'joinRoom.roomIdLabel': 'ID кімнати',
    'joinRoom.roomIdPlaceholder': 'наприклад, 12ab34cd',
    'joinRoom.submit': 'Увійти',
    'metronome.eyebrow': 'Кімната',
    'metronome.tempoLabel': 'Темп',
    'metronome.bpmLabel': 'BPM',
    'metronome.decreaseTempo': 'Зменшити темп',
    'metronome.increaseTempo': 'Збільшити темп',
    'metronome.applyTempo': 'Застосувати темп',
    'metronome.timeSignatureLabel': 'Розмір',
    'metronome.beatLabel': 'Доля {count}',
    'metronome.start': 'Старт',
    'metronome.stop': 'Стоп',
    'metronome.soundEnabled': 'Звук увімкнено',
    'metronome.enableSound': 'Увімкнути звук',
    'metronome.shareRoom': 'Поділитися кімнатою',
    'metronome.changeInstrument': 'Змінити інструмент',
    'metronome.leaveRoom': 'Вийти',
    'metronome.emptyState': 'Приєднайтеся або створіть кімнату, щоб керувати метрономом.',
    'metronome.noRoom': 'Немає кімнати',
    'theme.light': 'Світла тема',
    'theme.dark': 'Темна тема',
    'participants.eyebrow': 'Учасники',
    'participants.joined': 'Приєднався {time}',
    'participants.empty': 'Поки що ви тут самі.',
    'participants.fallbackInstrument': 'Музикант',
    'instrumentModal.eyebrow': 'Хто грає?',
    'instrumentModal.title': 'Оберіть свій інструмент',
    'instrumentModal.hint': 'Це допоможе іншим учасникам знати вашу роль.',
    'share.success': 'Посилання на запрошення скопійовано.',
    'share.error': 'Не вдалося скопіювати. Скопіюйте вручну з адресного рядка.',
    'errors.createRoom': 'Не вдалося створити кімнату.',
    'errors.joinRoom': 'Не вдалося приєднатися. Перевірте ID і спробуйте ще раз.',
    'audio.enablePrompt': 'Звук вимагає взаємодії. Натисніть «Увімкнути звук», щоб дозволити відтворення.',
    'syncStatus.idle': 'Очікування',
    'syncStatus.connecting': 'Підключення…',
    'syncStatus.connected': 'З`єднано',
    'syncStatus.reconnecting': 'Повторне підключення…',
    'syncStatus.disconnected': 'Відключено',
    'syncStatus.drift': 'дрифт {value} мс'
  }
};

export const localeOptions = [
  { value: 'en' as Locale, label: EN_MESSAGES['locale.enLabel'] },
  { value: 'uk' as Locale, label: EN_MESSAGES['locale.ukLabel'] }
];

const currentLocale = ref<Locale>(resolveStoredLocale());

export function useLocalization() {
  const locale = computed<Locale>({
    get: () => currentLocale.value,
    set: (value) => setLocale(value)
  });

  function setLocale(value: Locale) {
    if (currentLocale.value === value) return;
    currentLocale.value = value;
    if (typeof window !== 'undefined') {
      window.localStorage.setItem(LOCALE_STORAGE_KEY, value);
    }
  }

  function t(key: TranslationKey, vars?: Record<string, string | number>) {
    const message = TRANSLATIONS[currentLocale.value]?.[key] ?? EN_MESSAGES[key] ?? key;
    if (!vars) {
      return message;
    }
    return message.replace(/\{(\w+)\}/g, (_, token) =>
      Object.prototype.hasOwnProperty.call(vars, token) ? String(vars[token]) : `{${token}}`
    );
  }

  return {
    locale,
    localeOptions,
    setLocale,
    t
  };
}

function resolveStoredLocale(): Locale {
  if (typeof window === 'undefined') {
    return 'en';
  }
  const stored = window.localStorage.getItem(LOCALE_STORAGE_KEY);
  return stored === 'uk' ? 'uk' : 'en';
}
