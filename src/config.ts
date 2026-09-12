import type { EnemyType, Pillar, ThemeBucket, WaveComposition, ZoneName } from './types'

// ---------- World ----------
export const WORLD_W = 40
export const WORLD_H = 26

export const PILLARS: Pillar[] = [
  { x: -9, y: -6, r: 1.9, name: 'north_west_pillar' },
  { x: 9, y: -6, r: 1.9, name: 'north_east_pillar' },
  { x: -9, y: 6, r: 1.9, name: 'south_west_pillar' },
  { x: 9, y: 6, r: 1.9, name: 'south_east_pillar' },
]

export const ZONE_CENTERS: Record<ZoneName, { x: number; y: number }> = {
  north_chokepoint: { x: 0, y: -10.5 },
  south_pillar: { x: 0, y: 10.5 },
  east_flank: { x: 16.5, y: 0 },
  west_flank: { x: -16.5, y: 0 },
  center_open: { x: 0, y: 0 },
  balanced: { x: 0, y: 0 },
}

export const ZONE_SPAWNS: Record<Exclude<ZoneName, 'balanced'>, Vec2[]> = {
  north_chokepoint: [{ x: -6, y: -12 }, { x: 0, y: -12.4 }, { x: 6, y: -12 }],
  south_pillar: [{ x: -6, y: 12 }, { x: 0, y: 12.4 }, { x: 6, y: 12 }],
  east_flank: [{ x: 18.5, y: -8 }, { x: 18.5, y: 0 }, { x: 18.5, y: 8 }],
  west_flank: [{ x: -18.5, y: -8 }, { x: -18.5, y: 0 }, { x: -18.5, y: 8 }],
  center_open: [{ x: 0, y: 0 }, { x: 5, y: 2 }, { x: -5, y: -2 }],
}

interface Vec2 { x: number; y: number }

// ---------- Player ----------
export const PLAYER = {
  maxHp: 100,
  speed: 5.6,
  radius: 0.75,
  fireCooldown: 0.24,
  bulletDmg: 35,
  bulletSpeed: 19,
  bulletLife: 0.85,
  meleeDmg: 55,
  meleeRange: 2.2,
  meleeCooldown: 0.7,
  purgeDmg: 130,
  purgeRadius: 5.6,
  purgeCooldown: 15,
  invulnTime: 0.4,
}

// ---------- Enemies ----------
export interface EnemyConfig {
  hp: number; speed: number; damage: number; radius: number; scale: number; score: number
}

export const ENEMY_CONFIG: Record<EnemyType, EnemyConfig> = {
  standard: { hp: 100, speed: 3.0, damage: 10, radius: 0.85, scale: 1.0, score: 10 },
  fast: { hp: 50, speed: 6.1, damage: 5, radius: 0.65, scale: 0.8, score: 15 },
  tanky: { hp: 250, speed: 1.6, damage: 20, radius: 1.15, scale: 1.3, score: 25 },
}

export const ENEMY_ATTACK_RANGE = 1.7
export const ENEMY_ATTACK_CD = 1.0
export const SPAWN_STAGGER = 0.32
export const INTERMISSION_MIN = 1.1
export const TAUNT_HOLD = 2.9

// Wave 1 composition bias per theme bucket (from PLAN.md 5.4)
export const WAVE1_BIAS: Record<ThemeBucket, WaveComposition> = {
  coastal: { standard: 2, fast: 3, tanky: 1 },
  desert: { standard: 2, fast: 1, tanky: 3 },
  temperate: { standard: 2, fast: 2, tanky: 2 },
  mountain: { standard: 3, fast: 1, tanky: 2 },
  urban: { standard: 1, fast: 4, tanky: 1 },
}

// ---------- Behavior logging ----------
export const STATIONARY_THRESHOLD = 0.6 // world units/sec

// ---------- AI ----------
export const GEMINI_MODEL = 'gemini-2.0-flash'
export const GEMINI_URL = `https://generativelanguage.googleapis.com/v1beta/models/${GEMINI_MODEL}:generateContent`
export const WAVE_TIMEOUT_MS = 2500
export const AUTOPSY_TIMEOUT_MS = 3200

// Paste your key here, or add ?key=YOUR_KEY to the URL once (stored locally).
export const GEMINI_API_KEY = ''

export const VALID_ZONES: ZoneName[] = [
  'north_chokepoint', 'south_pillar', 'east_flank', 'west_flank', 'center_open', 'balanced'
]

export const CITY_MAP: Record<string, ThemeBucket> = {
  mumbai: 'urban', delhi: 'desert', 'new delhi': 'desert', noida: 'desert', gurgaon: 'desert',
  jaipur: 'desert', jodhpur: 'desert', ahmedabad: 'desert', surat: 'desert', dubai: 'desert',
  chennai: 'coastal', goa: 'coastal', panaji: 'coastal', kochi: 'coastal',
  visakhapatnam: 'coastal', kolkata: 'coastal', singapore: 'coastal',
  bangalore: 'temperate', bengaluru: 'temperate', pune: 'temperate', hyderabad: 'temperate',
  london: 'temperate', paris: 'temperate', berlin: 'temperate',
  shimla: 'mountain', manali: 'mountain', darjeeling: 'mountain', srinagar: 'mountain',
  'los angeles': 'urban', 'new york': 'urban', tokyo: 'urban', seoul: 'urban', shanghai: 'urban',
}
