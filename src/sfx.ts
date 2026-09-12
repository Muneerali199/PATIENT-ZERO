// Tiny synthesized SFX — zero assets, WebAudio only.
let ctx: AudioContext | null = null
let muted = false

try { muted = localStorage.getItem('pz_muted') === '1' } catch { /* ignore */ }

function ac(): AudioContext | null {
  if (muted) return null
  if (!ctx) {
    try {
      ctx = new (window.AudioContext || (window as unknown as { webkitAudioContext: typeof AudioContext }).webkitAudioContext)()
    } catch { return null }
  }
  if (ctx.state === 'suspended') void ctx.resume()
  return ctx
}

export function unlockAudio() { ac() }

export function isMuted() { return muted }
export function toggleMute(): boolean {
  muted = !muted
  try { localStorage.setItem('pz_muted', muted ? '1' : '0') } catch { /* ignore */ }
  return muted
}

function tone(freq: number, dur: number, type: OscillatorType, vol: number, slideTo?: number) {
  const a = ac()
  if (!a) return
  const o = a.createOscillator()
  const g = a.createGain()
  o.type = type
  o.frequency.setValueAtTime(freq, a.currentTime)
  if (slideTo) o.frequency.exponentialRampToValueAtTime(Math.max(30, slideTo), a.currentTime + dur)
  g.gain.setValueAtTime(vol, a.currentTime)
  g.gain.exponentialRampToValueAtTime(0.0001, a.currentTime + dur)
  o.connect(g); g.connect(a.destination)
  o.start(); o.stop(a.currentTime + dur + 0.02)
}

function noise(dur: number, vol: number, freq: number) {
  const a = ac()
  if (!a) return
  const len = Math.floor(a.sampleRate * dur)
  const buf = a.createBuffer(1, len, a.sampleRate)
  const d = buf.getChannelData(0)
  for (let i = 0; i < len; i++) d[i] = (Math.random() * 2 - 1) * (1 - i / len)
  const src = a.createBufferSource()
  src.buffer = buf
  const f = a.createBiquadFilter()
  f.type = 'lowpass'
  f.frequency.value = freq
  const g = a.createGain()
  g.gain.value = vol
  src.connect(f); f.connect(g); g.connect(a.destination)
  src.start()
}

export function sfx(name: string) {
  switch (name) {
    case 'shoot': tone(880, 0.07, 'square', 0.045, 220); break
    case 'hit': noise(0.05, 0.09, 2400); break
    case 'melee': tone(180, 0.09, 'sawtooth', 0.07, 60); break
    case 'die': noise(0.22, 0.13, 900); tone(120, 0.22, 'triangle', 0.08, 40); break
    case 'hurt': tone(140, 0.16, 'sawtooth', 0.1, 55); break
    case 'purge': tone(70, 0.5, 'sine', 0.16, 320); noise(0.4, 0.1, 600); break
    case 'wave': tone(160, 0.3, 'square', 0.05, 420); break
    case 'taunt': tone(520, 0.05, 'square', 0.03); setTimeout(() => tone(430, 0.05, 'square', 0.03), 70); break
    case 'over': tone(220, 0.9, 'sawtooth', 0.09, 45); break
    case 'clear': tone(300, 0.12, 'square', 0.04, 600); break
  }
}
