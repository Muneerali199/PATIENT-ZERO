import type { BehaviorSummary, RunRecord, SpecimenProfile } from './types'

const KEY = 'pz_specimen'

const DEFAULT: SpecimenProfile = {
  specimen_number: 1,
  run_count: 0,
  last_run: null,
  last_3_wave_summaries: [],
  best: { wave: 0, score: 0 },
}

export function loadProfile(): SpecimenProfile {
  try {
    const raw = localStorage.getItem(KEY)
    if (!raw) {
      // First ever run on this device — assign specimen number
      const first: SpecimenProfile = { ...DEFAULT }
      saveProfile(first)
      return first
    }
    const p = JSON.parse(raw) as SpecimenProfile
    return {
      specimen_number: p.specimen_number ?? 1,
      run_count: p.run_count ?? 0,
      last_run: p.last_run ?? null,
      last_3_wave_summaries: Array.isArray(p.last_3_wave_summaries) ? p.last_3_wave_summaries : [],
      best: p.best ?? { wave: 0, score: 0 },
    }
  } catch {
    return { ...DEFAULT }
  }
}

export function saveProfile(p: SpecimenProfile) {
  try { localStorage.setItem(KEY, JSON.stringify(p)) } catch { /* ignore */ }
}

export function recordRun(
  p: SpecimenProfile,
  run: RunRecord,
  waveSummaries: BehaviorSummary[]
): SpecimenProfile {
  p.run_count += 1
  p.last_run = run
  // Keep only the last 3 summaries across sessions for AI context
  const merged = [...(p.last_3_wave_summaries || []), ...waveSummaries]
  p.last_3_wave_summaries = merged.slice(-3)
  if (run.waves_survived > p.best.wave) {
    p.best.wave = run.waves_survived
    p.best.score = run.score
  } else if (run.score > p.best.score) {
    p.best.score = run.score
  }
  saveProfile(p)
  return p
}

export function resetProfile(): SpecimenProfile {
  try { localStorage.removeItem(KEY) } catch { /* ignore */ }
  return loadProfile()
}

export function pad3(n: number): string {
  return String(n).padStart(3, '0')
}
