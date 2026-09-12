import { PILLARS, STATIONARY_THRESHOLD, WORLD_H, WORLD_W, ZONE_CENTERS } from './config'
import type { BehaviorSummary, KillRecord, ThemeBucket, ZoneName } from './types'

/**
 * Behavior Logger — accumulates the player's behavioral fingerprint during a wave.
 * Compiled once at wave-end into the summary sent to the AI brain.
 */
export class BehaviorLogger {
  private totalTime = 0
  private stationaryTime = 0
  private movingTime = 0
  private totalCoverDist = 0
  private frameCount = 0
  private kills: KillRecord[] = []
  private waveStart = 0
  private zoneTime: Record<string, number> = {}
  private active = false

  reset(waveStartEpoch: number) {
    this.totalTime = 0
    this.stationaryTime = 0
    this.movingTime = 0
    this.totalCoverDist = 0
    this.frameCount = 0
    this.kills = []
    this.waveStart = waveStartEpoch
    this.zoneTime = {}
    this.active = true
  }

  stop() { this.active = false }

  trackFrame(dt: number, px: number, py: number, vx: number, vy: number) {
    if (!this.active) return
    this.totalTime += dt
    const speed = Math.hypot(vx, vy)
    if (speed < STATIONARY_THRESHOLD) this.stationaryTime += dt
    else this.movingTime += dt

    let nearest = Infinity
    for (const p of PILLARS) {
      const d = Math.hypot(px - p.x, py - p.y) - p.r
      if (d < nearest) nearest = d
    }
    this.totalCoverDist += Math.max(0, nearest)
    this.frameCount++

    // Favored zone tracking (time-weighted)
    const zone = this.classifyZone(px, py)
    if (zone) this.zoneTime[zone] = (this.zoneTime[zone] || 0) + dt
  }

  trackKill(rec: KillRecord) {
    if (!this.active) return
    this.kills.push(rec)
  }

  private classifyZone(x: number, y: number): ZoneName | null {
    if (Math.abs(x) > WORLD_W / 2 || Math.abs(y) > WORLD_H / 2) return null
    let best: ZoneName | null = null
    let bestD = Infinity
    for (const [name, c] of Object.entries(ZONE_CENTERS) as [ZoneName, { x: number; y: number }][]) {
      if (name === 'balanced') continue
      const d = Math.hypot(x - c.x, y - c.y)
      if (d < bestD) { bestD = d; best = name }
    }
    return best
  }

  favoredZone(): ZoneName {
    let best: ZoneName = 'center_open'
    let bestT = -1
    for (const [z, t] of Object.entries(this.zoneTime) as [ZoneName, number][]) {
      if (t > bestT) { bestT = t; best = z }
    }
    return best
  }

  compile(waveNumber: number, playerHpPct: number, locationSeed: ThemeBucket | null): BehaviorSummary {
    const totalKills = this.kills.length
    const ranged = this.kills.filter(k => k.kind === 'ranged').length
    const melee = totalKills - ranged
    const avgEng = totalKills > 0
      ? this.kills.reduce((s, k) => s + k.distance, 0) / totalKills
      : 0
    const avgCover = this.frameCount > 0
      ? this.totalCoverDist / this.frameCount
      : 999

    return {
      wave_number: waveNumber,
      time_stationary_pct: this.totalTime > 0 ? this.stationaryTime / this.totalTime : 0,
      time_moving_pct: this.totalTime > 0 ? this.movingTime / this.totalTime : 0,
      kills_ranged_pct: totalKills > 0 ? ranged / totalKills : 0.5,
      kills_melee_pct: totalKills > 0 ? melee / totalKills : 0.5,
      avg_engagement_distance: Math.round(avgEng * 10) / 10,
      avg_distance_to_cover: Math.round(Math.min(avgCover, 99) * 10) / 10,
      wave_clear_time_seconds: Math.round(this.totalTime * 10) / 10,
      player_hp_remaining_pct: playerHpPct,
      location_seed: waveNumber === 1 ? locationSeed : null,
      player_favored_zone: this.favoredZone(),
    }
  }
}
