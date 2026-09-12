export type EnemyType = 'standard' | 'fast' | 'tanky'
export type ZoneName =
  | 'north_chokepoint' | 'south_pillar' | 'east_flank' | 'west_flank'
  | 'center_open' | 'balanced'
export type ThemeBucket = 'coastal' | 'desert' | 'temperate' | 'mountain' | 'urban'

export interface Vec { x: number; y: number }
export interface Pillar { x: number; y: number; r: number; name: string }

export interface Enemy {
  id: number
  type: EnemyType
  x: number; y: number
  vx: number; vy: number
  hp: number; maxHp: number
  speed: number; damage: number; radius: number
  attackCd: number
  flash: number
  spawnT: number
  dead: boolean
}

export interface Projectile {
  x: number; y: number
  vx: number; vy: number
  dmg: number
  life: number
  dead: boolean
}

export interface Particle {
  x: number; y: number
  vx: number; vy: number
  life: number; maxLife: number
  color: string
  size: number
}

export interface KillRecord {
  kind: 'ranged' | 'melee'
  distance: number
  enemyType: EnemyType
}

export interface BehaviorSummary {
  wave_number: number
  time_stationary_pct: number
  time_moving_pct: number
  kills_ranged_pct: number
  kills_melee_pct: number
  avg_engagement_distance: number
  avg_distance_to_cover: number
  wave_clear_time_seconds: number
  player_hp_remaining_pct: number
  location_seed?: ThemeBucket | null
  player_favored_zone?: ZoneName
}

export interface WaveComposition { standard: number; fast: number; tanky: number }

export interface AIDecision {
  next_wave_composition: WaveComposition
  spawn_zone_bias: ZoneName
  taunt_text: string
  reasoning?: string
  source: 'local' | 'gemini'
}

export interface AutopsyReport {
  archetype: string
  cause_of_death: string
  adaptability_index: number
  weakness_documented: string
  closing_remark: string
  source: 'local' | 'gemini'
}

export interface RunRecord {
  waves_survived: number
  score: number
  kills: number
  archetype: string
  cause_of_death: string
}

export interface SpecimenProfile {
  specimen_number: number
  run_count: number
  last_run: RunRecord | null
  last_3_wave_summaries: BehaviorSummary[]
  best: { wave: number; score: number }
}
