import type { MetronomeSyncPayload } from '@/types';

type BeatListener = (beatIndex: number) => void;

const getAudioContextCtor = () => {
  if (typeof window === 'undefined') {
    return null;
  }
  return window.AudioContext || (window as any).webkitAudioContext || null;
};

export class MetronomeEngine {
  private timerId: number | null = null;
  private absoluteBeatIndex = 0;
  private listeners: BeatListener[] = [];
  private payload: MetronomeSyncPayload | null = null;
  private clockOffsetMs = 0;
  private audioContext: AudioContext | null = null;
  private beatsPerMeasure = 4;
  private audioEnabled = false;

  async enableAudio(): Promise<boolean> {
    if (this.audioEnabled) {
      return true;
    }
    const Ctor = getAudioContextCtor();
    if (!Ctor) {
      return false;
    }
    if (!this.audioContext) {
      this.audioContext = new Ctor();
    }
    try {
      await this.audioContext.resume();
      this.primeAudio();
      this.audioEnabled = true;
      return true;
    } catch {
      return false;
    }
  }

  isAudioEnabled(): boolean {
    return this.audioEnabled;
  }

  disableAudio(): void {
    this.audioEnabled = false;
  }

  start(syncPayload: MetronomeSyncPayload, clockOffsetMs: number): void {
    this.stop();
    this.payload = syncPayload;
    this.clockOffsetMs = clockOffsetMs;
    this.absoluteBeatIndex = 0;
    this.beatsPerMeasure = this.parseBeatsPerMeasure(syncPayload.timeSignature);
    this.scheduleNextBeat();
  }

  stop(): void {
    if (this.timerId !== null) {
      window.clearTimeout(this.timerId);
      this.timerId = null;
    }
    this.absoluteBeatIndex = 0;
    this.payload = null;
  }

  onBeat(listener: BeatListener): () => void {
    this.listeners.push(listener);
    return () => {
      this.listeners = this.listeners.filter((l) => l !== listener);
    };
  }

  applyDriftCorrection(offsetMs: number): void {
    this.clockOffsetMs = offsetMs;
    if (!this.payload || this.timerId === null) {
      return;
    }
    this.scheduleNextBeat();
  }

  private scheduleNextBeat(): void {
    if (!this.payload) {
      return;
    }
    if (this.timerId !== null) {
      window.clearTimeout(this.timerId);
    }
    const serverStart = new Date(this.payload.startAtUtc).getTime();
    const clientNow = Date.now();
    const elapsedBeats = Math.max(
      0,
      Math.floor(((clientNow + this.clockOffsetMs) - serverStart) / this.payload.beatIntervalMs)
    );
    if (this.absoluteBeatIndex < elapsedBeats) {
      this.absoluteBeatIndex = elapsedBeats;
    }
    const nextServerTime = serverStart + this.absoluteBeatIndex * this.payload.beatIntervalMs;
    const clientTarget = nextServerTime - this.clockOffsetMs;
    const delay = Math.max(0, clientTarget - clientNow);
    this.timerId = window.setTimeout(() => {
      this.emitBeat();
      this.scheduleNextBeat();
    }, delay);
  }

  private emitBeat(): void {
    this.absoluteBeatIndex += 1;
    const beatWithinMeasure = ((this.absoluteBeatIndex - 1) % this.beatsPerMeasure) + 1;
    if (this.audioEnabled) {
      this.playClick(beatWithinMeasure);
    }
    for (const listener of this.listeners) {
      listener(beatWithinMeasure);
    }
  }

  private playClick(beat: number): void {
    const ctx = this.audioContext;
    if (!ctx) {
      return;
    }

    const accent = beat === 1;
    const now = ctx.currentTime + 0.001;

    const oscillator = ctx.createOscillator();
    const gainNode = ctx.createGain();

    oscillator.type = 'square';
    oscillator.frequency.value = accent ? 1200 : 900;

    gainNode.gain.setValueAtTime(0.0001, now);
    gainNode.gain.exponentialRampToValueAtTime(accent ? 0.6 : 0.3, now + 0.005);
    gainNode.gain.exponentialRampToValueAtTime(0.0001, now + 0.08);

    oscillator.connect(gainNode);
    gainNode.connect(ctx.destination);

    oscillator.start(now);
    oscillator.stop(now + 0.1);
  }

  private parseBeatsPerMeasure(signature?: string): number {
    if (!signature) return 4;
    const [numerator] = signature.split('/').map((part) => parseInt(part, 10));
    return Number.isFinite(numerator) && numerator > 0 ? numerator : 4;
  }

  private primeAudio(): void {
    const ctx = this.audioContext;
    if (!ctx) {
      return;
    }
    const buffer = ctx.createBuffer(1, 1, ctx.sampleRate);
    const source = ctx.createBufferSource();
    source.buffer = buffer;
    source.connect(ctx.destination);
    try {
      source.start();
    } catch {
      // ignore failures during priming
    }
  }
}
