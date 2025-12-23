export interface InstrumentOption {
  id: string;
  label: string;
  description: string;
  icon: string;
  color: string;
  textColor?: string;
}

export const INSTRUMENTS: InstrumentOption[] = [
  { id: 'bass', label: 'Bass Guitar', description: 'Low-end groove', icon: 'mdi:music-clef-bass', color: '#3b2c1d', textColor: '#f7e7c2' },
  { id: 'acoustic', label: 'Acoustic Guitar', description: 'Strummed warmth', icon: 'mdi:guitar-acoustic', color: '#8c4f26', textColor: '#fff1d0' },
  { id: 'electric', label: 'Electric Guitar', description: 'Lead or rhythm', icon: 'mdi:guitar-electric', color: '#b65f32', textColor: '#ffe8d2' },
  { id: 'drums', label: 'Drums', description: 'Percussion pulse', icon: 'fa-solid:drum', color: '#5c4634', textColor: '#f8e3bb' },
  { id: 'piano', label: 'Piano', description: 'Keys & chords', icon: 'mdi:piano', color: '#2d1b10', textColor: '#f8e7c1' },
  { id: 'synth', label: 'Synthesizer', description: 'Pads & leads', icon: 'mdi:waveform', color: '#4a5c5c', textColor: '#dff1ea' },
  { id: 'vocal', label: 'Vocal', description: 'Lead or backing voice', icon: 'mdi:microphone-variant', color: '#8a2c2b', textColor: '#ffd7c8' },
  { id: 'other', label: 'Other', description: 'Anything else', icon: 'mdi:music', color: '#3a4a3f', textColor: '#e3f5d7' }
];
