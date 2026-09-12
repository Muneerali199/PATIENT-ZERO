import { sfx } from './sfx'

function el<T extends HTMLElement>(id: string): T {
  const e = document.getElementById(id)
  if (!e) throw new Error(`missing element #${id}`)
  return e as T
}

export const ui = {
  hpFill: el<HTMLDivElement>('hpFill'),
  waveLabel: el<HTMLDivElement>('waveLabel'),
  themeTag: el<HTMLDivElement>('themeTag'),
  scoreLabel: el<HTMLDivElement>('scoreLabel'),
  brainBadge: el<HTMLSpanElement>('brainBadge'),
  taunt: el<HTMLDivElement>('taunt'),
  tauntText: el<HTMLDivElement>('tauntText'),
  reasoning: el<HTMLDivElement>('reasoning'),
  specialBtn: el<HTMLButtonElement>('specialBtn'),
  cdText: el<HTMLSpanElement>('cdText'),
  muteBtn: el<HTMLButtonElement>('muteBtn'),
  startScreen: el<HTMLDivElement>('startScreen'),
  startSpec: el<HTMLDivElement>('startSpec'),
  startGreet: el<HTMLDivElement>('startGreet'),
  startSeed: el<HTMLDivElement>('startSeed'),
  startBest: el<HTMLDivElement>('startBest'),
  startBtn: el<HTMLButtonElement>('startBtn'),
  overScreen: el<HTMLDivElement>('overScreen'),
  rRun: el<HTMLSpanElement>('rRun'),
  rSpec: el<HTMLSpanElement>('rSpec'),
  rWave: el<HTMLSpanElement>('rWave'),
  rKills: el<HTMLSpanElement>('rKills'),
  rArchetype: el<HTMLSpanElement>('rArchetype'),
  rCause: el<HTMLSpanElement>('rCause'),
  rWeak: el<HTMLSpanElement>('rWeak'),
  rAdaptPct: el<HTMLSpanElement>('rAdaptPct'),
  adaptFill: el<HTMLSpanElement>('adaptFill'),
  rRemark: el<HTMLDivElement>('rRemark'),
  rSource: el<HTMLDivElement>('rSource'),
  shareBtn: el<HTMLButtonElement>('shareBtn'),
  restartBtn: el<HTMLButtonElement>('restartBtn'),
}

// ---------- HUD ----------
export function setHp(pct: number) {
  ui.hpFill.style.width = `${Math.max(0, Math.min(1, pct)) * 100}%`
  ui.hpFill.classList.toggle('low', pct < 0.3)
}
export function setWave(n: number) { ui.waveLabel.textContent = `WAVE ${String(n).padStart(2, '0')}` }
export function setScore(n: number) { ui.scoreLabel.textContent = String(n) }
export function setThemeTag(s: string) { ui.themeTag.textContent = s }
export function setBrain(source: 'local' | 'gemini') {
  ui.brainBadge.textContent = source === 'gemini' ? 'GEMINI // ONLINE' : 'PZ-CORE // LOCAL'
  ui.brainBadge.style.color = source === 'gemini' ? '#9ef0b3' : ''
}

// ---------- Taunt typewriter ----------
let tauntTimer: ReturnType<typeof setInterval> | null = null
let tauntHideTimer: ReturnType<typeof setTimeout> | null = null

export function showTaunt(text: string, reasoning: string | undefined, holdSeconds: number) {
  if (tauntTimer) clearInterval(tauntTimer)
  if (tauntHideTimer) clearTimeout(tauntHideTimer)
  ui.taunt.classList.remove('done')
  ui.taunt.classList.add('show')
  ui.tauntText.textContent = ''
  ui.reasoning.textContent = ''
  sfx('taunt')

  let i = 0
  tauntTimer = setInterval(() => {
    if (i < text.length) {
      ui.tauntText.textContent += text[i++]
    } else {
      if (tauntTimer) clearInterval(tauntTimer)
      ui.taunt.classList.add('done')
      if (reasoning) ui.reasoning.textContent = reasoning
      tauntHideTimer = setTimeout(() => ui.taunt.classList.remove('show'), holdSeconds * 1000)
    }
  }, 34)
}

export function hideTaunt() {
  ui.taunt.classList.remove('show')
  if (tauntTimer) clearInterval(tauntTimer)
  if (tauntHideTimer) clearTimeout(tauntHideTimer)
}

// ---------- Screens ----------
export function showStart(spec: string, greet: string, seed: string, best: string) {
  ui.startScreen.classList.remove('hidden')
  ui.startSpec.textContent = spec
  ui.startGreet.textContent = greet
  ui.startSeed.textContent = seed
  ui.startBest.textContent = best
  ui.overScreen.classList.add('hidden')
}

export function hideScreens() {
  ui.startScreen.classList.add('hidden')
  ui.overScreen.classList.add('hidden')
}

export interface ReportData {
  run: number; spec: string; wave: number; kills: number; score: number
  archetype: string; cause: string; weak: string; adapt: number
  remark: string; source: 'local' | 'gemini'
}

export function showGameOver(r: ReportData) {
  ui.rRun.textContent = `RUN ${String(r.run).padStart(2, '0')}`
  ui.rSpec.textContent = r.spec
  ui.rWave.textContent = String(r.wave)
  ui.rKills.textContent = `${r.kills} / ${r.score}`
  ui.rArchetype.textContent = r.archetype.toUpperCase()
  ui.rCause.textContent = r.cause
  ui.rWeak.textContent = r.weak
  ui.rAdaptPct.textContent = r.adapt.toFixed(2)
  ui.adaptFill.style.width = '0%'
  ui.rRemark.textContent = `"${r.remark}"`
  ui.rSource.textContent = r.source === 'gemini'
    ? 'REPORT AUTHORED BY GEMINI FLASH'
    : 'REPORT AUTHORED BY PZ-CORE (LOCAL MODEL)'
  ui.overScreen.classList.remove('hidden')
  setTimeout(() => { ui.adaptFill.style.width = `${r.adapt * 100}%` }, 80)
}

export function setControlsVisible(v: boolean) {
  ui.specialBtn.style.display = v ? 'block' : 'none'
  ui.muteBtn.style.display = v ? 'block' : 'none'
}

export function setPurgeCooldown(remaining: number, total: number) {
  if (remaining <= 0) {
    ui.specialBtn.disabled = false
    ui.cdText.textContent = 'RDY'
  } else {
    ui.specialBtn.disabled = true
    ui.cdText.textContent = `${Math.ceil(remaining)}s`
  }
}

export async function copyText(t: string): Promise<boolean> {
  try {
    await navigator.clipboard.writeText(t)
    return true
  } catch {
    try {
      const ta = document.createElement('textarea')
      ta.value = t
      document.body.appendChild(ta)
      ta.select()
      document.execCommand('copy')
      ta.remove()
      return true
    } catch { return false }
  }
}
