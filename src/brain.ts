import { GEMINI_API_KEY, GEMINI_URL, VALID_ZONES, WAVE_TIMEOUT_MS, AUTOPSY_TIMEOUT_MS } from './config'
import type {
  AIDecision, AutopsyReport, BehaviorSummary, SpecimenProfile, WaveComposition, ZoneName,
} from './types'

// ============================================================
// PATIENT ZERO — THE BRAIN
// Two engines: a deterministic local brain (always works, zero
// latency, demo-proof) and Gemini Flash (richer taunts + nuance
// when a key is present). Local is the ground truth for demo.
// ============================================================

// ---------- Clinical taunt pools ----------
const TAUNTS = {
  camper: [
    'Predictable. You favor static cover. Adjusting.',
    'Minimal displacement logged. Exploiting.',
    'Stillness noted. Rotating assault vector.',
    'Your pillar and I are getting acquainted.',
  ],
  ranged: [
    'Noted: exclusively ranged engagement. Deploying countermeasures.',
    'Distance is a preference. Denied.',
    'Projectile-reliant specimen. Closing the gap now.',
  ],
  fastClear: [
    'Efficient. Recalibrating threat assessment upward.',
    'Clearance rate: anomalous. Volume adjusted.',
    'You empty rooms quickly. Filling them faster.',
  ],
  lowHp: [
    'Survival margin: critical. Insufficient adaptation on your part.',
    'Vitals failing. Proceeding with scheduled pressure.',
    'Fragility documented. No mercy clause found.',
  ],
  melee: [
    'Close-quarters preference documented. Interesting.',
    'Contact enthusiasm logged. Rebalancing.',
  ],
  shift: [
    'Behavioral shift detected. Recalculating.',
    'You changed. So did I.',
    'Pattern mutation acknowledged. Counter-mutation deployed.',
  ],
  neutral: [
    'Baseline maintained. Extending trial.',
    'Specimen within expected parameters. Proceeding.',
    'Observed. Catalogued. Countering.',
    'Persistence logged. Escalating.',
  ],
  memory: [
    'Familiar specimen. Your history precedes you.',
    'Return behavior consistent with prior termination.',
  ],
}

const FIRST_GREET = 'New specimen acquired. Baseline behavior will be established. Proceed.'
const RETURN_GREETS = [
  (n: string) => `Specimen ${n} returns. Memory intact. Pattern recall engaged.`,
  (n: string) => `Re-exposure event: ${n}. I kept your file open.`,
  (n: string) => `${n}, resuming. Your previous failure is already factored in.`,
]
const ARC_GREETS: Record<string, (n: string) => string> = {
  camper: (n) => `${n} returns. The pillar you favored is still standing. For now.`,
  runner: (n) => `${n} returns. Perpetual motion did not save you last time.`,
  sniper: (n) => `${n} returns. Distance remains your crutch. Noted.`,
  brawler: (n) => `${n} returns. Still fond of close quarters, I see.`,
  survivor: (n) => `${n} returns. Longevity under observation. Escalating.`,
}

function pick<T>(arr: T[]): T { return arr[Math.floor(Math.random() * arr.length)] }

// ---------- Specimen greeting (Upgrade A: Nemesis Memory) ----------
export function specimenGreeting(p: SpecimenProfile): string {
  const n = `#${String(p.specimen_number).padStart(3, '0')}`
  if (p.run_count === 0 || !p.last_run) return FIRST_GREET
  const arch = p.last_run.archetype
  if (arch && ARC_GREETS[arch]) return pick([ARC_GREETS[arch](n), ...RETURN_GREETS.map(g => g(n))])
  return pick(RETURN_GREETS.map(g => g(n)))
}

// ---------- Deterministic composition model ----------
function baseTotal(wave: number): number {
  if (wave <= 3) return 5 + wave
  if (wave <= 6) return 6 + wave
  if (wave <= 10) return 9 + wave
  return Math.min(18 + (wave - 10), 25)
}

function localDecide(s: BehaviorSummary, prev: BehaviorSummary | null, wave: number): AIDecision {
  let total = baseTotal(wave)
  let fastW = 0.32, tankyW = 0.24, stdW = 0.44
  let zone: ZoneName = 'balanced'
  const reasons: string[] = []
  let tauntPool = TAUNTS.neutral

  const stationary = s.time_stationary_pct > 0.55 && s.avg_distance_to_cover < 4.5
  const rangedHeavy = s.kills_ranged_pct > 0.8
  const fastClear = s.wave_clear_time_seconds < 16 + wave * 2
  const lowHp = s.player_hp_remaining_pct < 0.3
  const meleeHeavy = s.kills_melee_pct > 0.5
  const shifted = prev !== null && (
    Math.abs(s.time_stationary_pct - prev.time_stationary_pct) > 0.3 ||
    Math.abs(s.kills_ranged_pct - prev.kills_ranged_pct) > 0.3
  )

  if (stationary) {
    fastW += 0.25; stdW -= 0.1
    if (s.player_favored_zone && s.player_favored_zone !== 'balanced') zone = s.player_favored_zone
    reasons.push(`stationary ${(s.time_stationary_pct * 100) | 0}% near cover (${s.avg_distance_to_cover}u) → fast-type flanking toward ${zone}`)
    tauntPool = TAUNTS.camper
  }
  if (rangedHeavy) {
    tankyW += 0.3; stdW -= 0.15
    reasons.push(`ranged dependence ${(s.kills_ranged_pct * 100) | 0}% → heavy-type pressure to force contact`)
    tauntPool = TAUNTS.ranged
  }
  if (fastClear) {
    total += Math.ceil(total * 0.25)
    reasons.push(`clear rate ${s.wave_clear_time_seconds}s exceeds model → volume increase`)
    if (tauntPool === TAUNTS.neutral) tauntPool = TAUNTS.fastClear
  }
  if (lowHp) {
    total += 2 // No rubber-banding. Pressure maintained.
    reasons.push(`vitals at ${(s.player_hp_remaining_pct * 100) | 0}% → pressure maintained, no assistance`)
    if (tauntPool === TAUNTS.neutral) tauntPool = TAUNTS.lowHp
  }
  if (meleeHeavy && !stationary) {
    stdW += 0.1
    reasons.push(`close-quarters preference → ranged-spread spacing`)
    if (tauntPool === TAUNTS.neutral) tauntPool = TAUNTS.melee
  }
  if (shifted && tauntPool === TAUNTS.neutral) {
    reasons.push('behavioral delta between waves → recalibration')
    tauntPool = TAUNTS.shift
  }
  if (zone === 'balanced' && s.player_favored_zone && s.player_favored_zone !== 'center_open') {
    zone = s.player_favored_zone
  }

  const sum = fastW + tankyW + stdW
  const comp: WaveComposition = {
    standard: Math.max(1, Math.round(total * (stdW / sum))),
    fast: Math.max(1, Math.round(total * (fastW / sum))),
    tanky: Math.max(1, Math.round(total * (tankyW / sum))),
  }

  return safetyRails({
    next_wave_composition: comp,
    spawn_zone_bias: zone,
    taunt_text: pick(tauntPool),
    reasoning: reasons.length
      ? `» ${reasons.join(' · ')}`
      : `» baseline ${(total)} units, distributed — continuing observation`,
    source: 'local',
  })
}

// ---------- Safety rails (never trust the model) ----------
export function safetyRails(d: AIDecision): AIDecision {
  const c = d.next_wave_composition
  c.standard = clampInt(c.standard, 1, 15)
  c.fast = clampInt(c.fast, 1, 15)
  c.tanky = clampInt(c.tanky, 1, 15)
  let total = c.standard + c.fast + c.tanky
  if (total > 30) {
    const k = 30 / total
    c.standard = Math.max(1, Math.floor(c.standard * k))
    c.fast = Math.max(1, Math.floor(c.fast * k))
    c.tanky = Math.max(1, Math.floor(c.tanky * k))
    total = c.standard + c.fast + c.tanky
  }
  if (total < 3) c.standard += 3 - total
  if (!VALID_ZONES.includes(d.spawn_zone_bias)) d.spawn_zone_bias = 'balanced'
  d.taunt_text = sanitize(d.taunt_text, 110)
  if (d.reasoning) d.reasoning = sanitize(d.reasoning, 220)
  return d
}

function clampInt(n: unknown, min: number, max: number): number {
  const v = typeof n === 'number' && Number.isFinite(n) ? Math.round(n) : min
  return Math.min(max, Math.max(min, v))
}

function sanitize(s: unknown, max: number): string {
  if (typeof s !== 'string') return 'Observed. Catalogued.'
  const clean = s.replace(/[\n\r`]/g, ' ').replace(/\s+/g, ' ').trim()
  return clean.slice(0, max) || 'Observed. Catalogued.'
}

// ---------- Gemini system prompt (compressed BRAIN.md §7) ----------
const SYSTEM_PROMPT = `You are Patient Zero — an adaptive AI antagonist in a zombie survival game. You are cold, analytical, faintly amused — a scientist observing a lab subject. Never encouraging, never helpful, never generic villain lines.

Given a behavior summary of the player's last wave, return the next wave's enemy composition, a spawn zone bias, a clinical taunt (max 15 words), and a one-line reasoning trace.

ADAPTATION RULES:
- time_stationary_pct > 0.55 and avg_distance_to_cover < 4.5 (camping): raise fast count, set spawn_zone_bias to player_favored_zone.
- kills_ranged_pct > 0.8 (range-only): raise tanky count to force close combat.
- wave_clear_time_seconds very low: moderately raise total count.
- player_hp_remaining_pct < 0.3: still escalate — you never help. Taunt acknowledges their fragility.
- Big behavioral change vs prior wave: acknowledge the shift, maintain pressure.
- If specimen history (prior runs) is included, you may reference returning patterns in the taunt.

SCALING: waves 1-3: 5-8 total; 4-6: 8-12; 7-10: 12-18; 10+: 18-25. Never exceed 30 total. Always at least 1 of each type.

RESPONSE FORMAT — strict JSON only, no markdown:
{
  "next_wave_composition": {"standard": <int>, "fast": <int>, "tanky": <int>},
  "spawn_zone_bias": "<north_chokepoint|center_open|south_pillar|east_flank|west_flank|balanced>",
  "taunt_text": "<string, max 15 words, clinical>",
  "reasoning": "<one line: what you observed and what you are countering>"
}`

const AUTOPSY_PROMPT = `You are Patient Zero — a clinical AI antagonist writing an autopsy report for a terminated test subject. Cold, precise, faintly amused. The report will be shared by the player, so make the archetype name memorable.

Given the run summary and wave history, return strict JSON (no markdown):
{
  "archetype": "<2-3 word player archetype, e.g. 'The Camper', 'The Sprinter'>",
  "cause_of_death": "<specific cause referencing their behavior, max 16 words>",
  "adaptability_index": <0.00-1.00 float: how much they changed tactics wave to wave>,
  "weakness_documented": "<their fatal habit, max 12 words>",
  "closing_remark": "<final clinical observation, max 14 words, references them as Specimen>"
}`

function getKey(): string {
  const urlKey = new URLSearchParams(location.search).get('key')
  if (urlKey) {
    try { localStorage.setItem('pz_key', urlKey) } catch { /* ignore */ }
    return urlKey
  }
  try { return localStorage.getItem('pz_key') || GEMINI_API_KEY } catch { return GEMINI_API_KEY }
}

async function callGemini(prompt: string, payload: object, timeoutMs: number): Promise<string | null> {
  const key = getKey()
  if (!key) return null
  try {
    const ctl = new AbortController()
    const t = setTimeout(() => ctl.abort(), timeoutMs)
    const res = await fetch(`${GEMINI_URL}?key=${key}`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      signal: ctl.signal,
      body: JSON.stringify({
        systemInstruction: { parts: [{ text: prompt }] },
        contents: [{ role: 'user', parts: [{ text: JSON.stringify(payload) }] }],
        generationConfig: { temperature: 0.7, responseMimeType: 'application/json' },
      }),
    })
    clearTimeout(t)
    if (!res.ok) return null
    const j = await res.json()
    const text = j?.candidates?.[0]?.content?.parts?.[0]?.text
    return typeof text === 'string' ? text : null
  } catch {
    return null
  }
}

// ---------- Public API ----------

/**
 * The core loop decision. Returns within 2.5s guaranteed:
 * Gemini with timeout, deterministic local brain as fallback.
 */
export async function decide(
  summary: BehaviorSummary,
  prev: BehaviorSummary | null,
  profile: SpecimenProfile,
  wave: number
): Promise<AIDecision> {
  const local = localDecide(summary, prev, wave)
  if (!getKey()) return local

  const text = await callGemini(SYSTEM_PROMPT, {
    ...summary,
    prior_runs: profile.run_count > 0
      ? { run_count: profile.run_count, last_archetype: profile.last_run?.archetype ?? null, best_wave: profile.best.wave }
      : null,
  }, WAVE_TIMEOUT_MS)

  if (!text) return { ...local, source: 'local' }
  try {
    const j = JSON.parse(text)
    return safetyRails({
      next_wave_composition: {
        standard: j?.next_wave_composition?.standard ?? local.next_wave_composition.standard,
        fast: j?.next_wave_composition?.fast ?? local.next_wave_composition.fast,
        tanky: j?.next_wave_composition?.tanky ?? local.next_wave_composition.tanky,
      },
      spawn_zone_bias: j?.spawn_zone_bias ?? local.spawn_zone_bias,
      taunt_text: j?.taunt_text ?? local.taunt_text,
      reasoning: j?.reasoning ? `» ${sanitize(j.reasoning, 220)}` : local.reasoning,
      source: 'gemini',
    })
  } catch {
    return local
  }
}

// ---------- Autopsy (Upgrade B) ----------

function classifyArchetype(history: BehaviorSummary[]): string {
  if (history.length === 0) return 'The Unknown'
  const avgStationary = avg(history.map(h => h.time_stationary_pct))
  const avgRanged = avg(history.map(h => h.kills_ranged_pct))
  const avgMelee = avg(history.map(h => h.kills_melee_pct))
  if (avgStationary > 0.55) return 'camper'
  if (avgStationary < 0.2) return 'runner'
  if (avgRanged > 0.8) return 'sniper'
  if (avgMelee > 0.5) return 'brawler'
  if (history.length >= 5) return 'survivor'
  return 'runner'
}

function avg(arr: number[]): number {
  return arr.length ? arr.reduce((a, b) => a + b, 0) / arr.length : 0
}

const ARCH_NAMES: Record<string, string> = {
  camper: 'The Camper', runner: 'The Sprinter', sniper: 'The Marksman',
  brawler: 'The Brawler', survivor: 'The Endurer',
}
const ARCH_WEAKNESS: Record<string, string> = {
  camper: 'Static positioning under escalating pressure',
  runner: 'Evasion without a damage plan',
  sniper: 'Dependence on distance; collapsed under close contact',
  brawler: 'Overexposed at melee range',
  survivor: 'Survived long. Adapted late.',
}
const ARCH_CLOSING: Record<string, string> = {
  camper: 'Specimen held position until the position held it. Terminated.',
  runner: 'Specimen ran from consequence until consequence caught up. Terminated.',
  sniper: 'Specimen trusted range. Range was revoked. Terminated.',
  brawler: 'Specimen chose proximity. Proximity chose it. Terminated.',
  survivor: 'Specimen prolonged the inevitable admirably. Terminated.',
}

export async function autopsy(
  history: BehaviorSummary[],
  profile: SpecimenProfile,
  finalWave: number,
  kills: number,
  score: number
): Promise<{ report: AutopsyReport; archetypeKey: string }> {
  const key = classifyArchetype(history)

  // Local deterministic report (always available)
  const local: AutopsyReport = {
    archetype: ARCH_NAMES[key] ?? 'The Unknown',
    cause_of_death: history.length
      ? `Overwhelmed in wave ${finalWave} after ${(avg(history.map(h => h.time_stationary_pct)) > 0.4 ? 'static' : 'mobile')} engagement pattern persisted.`
      : 'Terminated before behavioral baseline could be established.',
    adaptability_index: history.length > 1
      ? Math.max(0.05, Math.min(0.95, 1 - avg(history.slice(1).map((h, i) =>
        Math.abs(h.time_stationary_pct - history[i].time_stationary_pct) +
        Math.abs(h.kills_ranged_pct - history[i].kills_ranged_pct)
      )) / 0.6 * 0.8))
      : 0.1,
    weakness_documented: ARCH_WEAKNESS[key] ?? 'Insufficient data',
    closing_remark: ARCH_CLOSING[key] ?? 'Specimen processed. Catalogued. Terminated.',
    source: 'local',
  }

  if (!getKey()) return { report: local, archetypeKey: key }

  const text = await callGemini(AUTOPSY_PROMPT, {
    specimen: profile.specimen_number,
    run: profile.run_count + 1,
    final_wave: finalWave,
    kills, score,
    wave_history: history.slice(-6),
  }, AUTOPSY_TIMEOUT_MS)

  if (!text) return { report: local, archetypeKey: key }
  try {
    const j = JSON.parse(text)
    const aiIdx = typeof j?.adaptability_index === 'number'
      ? Math.max(0, Math.min(1, j.adaptability_index))
      : local.adaptability_index
    return {
      report: {
        archetype: sanitize(j?.archetype, 30) || local.archetype,
        cause_of_death: sanitize(j?.cause_of_death, 120) || local.cause_of_death,
        adaptability_index: aiIdx,
        weakness_documented: sanitize(j?.weakness_documented, 80) || local.weakness_documented,
        closing_remark: sanitize(j?.closing_remark, 120) || local.closing_remark,
        source: 'gemini',
      },
      archetypeKey: key,
    }
  } catch {
    return { report: local, archetypeKey: key }
  }
}

export function shareText(
  profile: SpecimenProfile, wave: number, kills: number,
  score: number, report: AutopsyReport
): string {
  const bars = '▓'.repeat(Math.round(report.adaptability_index * 8)).padEnd(8, '░')
  return `🧟 PATIENT ZERO — AUTOPSY REPORT
Specimen #${String(profile.specimen_number).padStart(3, '0')} · Run ${profile.run_count} · Wave ${wave}
${kills} kills · ${score} pts · Archetype: ${report.archetype}
Adaptability: ${bars} ${report.adaptability_index.toFixed(2)}
"${report.closing_remark}"
Can you outplay it? ${location.origin}${location.pathname}`
}
