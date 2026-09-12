import { decide, autopsy, shareText, specimenGreeting } from './brain'
import {
  ENEMY_ATTACK_CD, ENEMY_ATTACK_RANGE, ENEMY_CONFIG, INTERMISSION_MIN,
  PILLARS, PLAYER, SPAWN_STAGGER, TAUNT_HOLD, WAVE1_BIAS, WORLD_H, WORLD_W,
  ZONE_SPAWNS,
} from './config'
import { BehaviorLogger } from './logger'
import { loadProfile, pad3, recordRun, saveProfile } from './memory'
import { sfx, unlockAudio } from './sfx'
import { FALLBACK_THEME, type Theme } from './themes'
import type {
  AIDecision, BehaviorSummary, Enemy, EnemyType, Particle, Projectile,
  SpecimenProfile, WaveComposition, ZoneName,
} from './types'
import * as UI from './ui'
import type { ThemeBucket } from './types'

type Phase = 'boot' | 'menu' | 'playing' | 'intermission' | 'over'

const CTX_MENU = { x: 0, y: 0 }

export class Game {
  // state
  phase: Phase = 'boot'
  time = 0
  wave = 0
  score = 0
  kills = 0
  theme: Theme = FALLBACK_THEME
  profile: SpecimenProfile = loadProfile()
  logger = new BehaviorLogger()
  history: BehaviorSummary[] = []

  // entities
  player = { x: 0, y: 6, hp: PLAYER.maxHp, aimX: 0, aimY: -1, fireCd: 0, meleeCd: 0, purgeCd: 0, invuln: 0, hitFlash: 0 }
  enemies: Enemy[] = []
  projectiles: Projectile[] = []
  particles: Particle[] = []
  spawnQueue: { t: number; type: EnemyType; x: number; y: number }[] = []
  nextEnemyId = 1

  // input
  keys = new Set<string>()
  touchMove: { id: number; ox: number; oy: number; x: number; y: number } | null = null
  touchAim: { id: number; x: number; y: number; active: boolean } | null = null
  mouse = { x: 0, y: 0, down: false }

  // AI
  pendingDecision: AIDecision | null = null
  intermissionT = 0
  pendingWaveComp: WaveComposition | null = null
  pendingZone: ZoneName = 'balanced'
  lastBrainSource: 'local' | 'gemini' = 'local'

  constructor(private canvas: HTMLCanvasElement, private ctx: CanvasRenderingContext2D) {}

  // ================= BOOT / MENU =================
  async boot() {
    UI.setControlsVisible(false)
    UI.setScore(0)
    UI.setHp(1)
    UI.setWave(1)

    const { resolveSeed } = await import('./location')
    const seed = await resolveSeed()
    this.theme = (await import('./themes')).THEMES[seed.bucket as ThemeBucket]

    const spec = `SPECIMEN #${pad3(this.profile.specimen_number)}`
    const greet = specimenGreeting(this.profile)
    const best = this.profile.best.wave > 0
      ? `BEST: WAVE ${this.profile.best.wave} · ${this.profile.best.score} PTS`
      : 'NO PRIOR DATA ON FILE'

    UI.setThemeTag(this.theme.label)
    UI.showStart(spec, greet, seed.label, best)
    this.phase = 'menu'
  }

  start() {
    unlockAudio()
    UI.hideScreens()
    UI.setControlsVisible(true)
    this.resetRun()
    this.phase = 'playing'
    this.startWave(1, WAVE1_BIAS[this.theme.bucket], 'balanced')
  }

  private resetRun() {
    this.time = 0
    this.wave = 0
    this.score = 0
    this.kills = 0
    this.history = []
    this.enemies = []
    this.projectiles = []
    this.particles = []
    this.spawnQueue = []
    this.player = { x: 0, y: 6, hp: PLAYER.maxHp, aimX: 0, aimY: -1, fireCd: 0, meleeCd: 0, purgeCd: 0, invuln: 0, hitFlash: 0 }
    UI.setHp(1)
    UI.setScore(0)
  }

  // ================= WAVES =================
  private startWave(n: number, comp: WaveComposition, zone: ZoneName) {
    this.wave = n
    UI.setWave(n)
    UI.hideTaunt()
    sfx('wave')

    // Build spawn queue with zone bias (60% from biased zone)
    const types: EnemyType[] = []
    for (let i = 0; i < comp.standard; i++) types.push('standard')
    for (let i = 0; i < comp.fast; i++) types.push('fast')
    for (let i = 0; i < comp.tanky; i++) types.push('tanky')
    // shuffle
    for (let i = types.length - 1; i > 0; i--) {
      const j = Math.floor(Math.random() * (i + 1)); [types[i], types[j]] = [types[j], types[i]]
    }
    const zones = (Object.keys(ZONE_SPAWNS) as (Exclude<ZoneName, 'balanced'>)[])
    types.forEach((t, i) => {
      const useBias = zone !== 'balanced' && zone in ZONE_SPAWNS && Math.random() < 0.6
      const z = useBias ? zone as Exclude<ZoneName, 'balanced'> : zones[Math.floor(Math.random() * zones.length)]
      const base = ZONE_SPAWNS[z][Math.floor(Math.random() * ZONE_SPAWNS[z].length)]
      this.spawnQueue.push({
        t: this.time + i * SPAWN_STAGGER,
        type: t,
        x: base.x + (Math.random() - 0.5) * 1.6,
        y: base.y + (Math.random() - 0.5) * 1.6,
      })
    })
    this.logger.reset(this.time)
  }

  private async endWave() {
    this.logger.stop()
    const summary = this.logger.compile(this.wave, this.player.hp / PLAYER.maxHp, this.theme.bucket)
    this.history.push(summary)

    this.phase = 'intermission'
    this.intermissionT = 0
    this.pendingDecision = null

    const prev = this.history.length > 1 ? this.history[this.history.length - 2] : null
    const decideStart = performance.now()
    const nextWaveNum = this.wave + 1
    decide(summary, prev, this.profile, nextWaveNum).then(d => {
      // Guarantee a minimum intermission for the taunt to land
      const wait = Math.max(0, INTERMISSION_MIN * 1000 - (performance.now() - decideStart))
      setTimeout(() => {
        if (this.phase !== 'intermission') return
        this.pendingDecision = d
        this.lastBrainSource = d.source
        UI.setBrain(d.source)
        UI.showTaunt(d.taunt_text, d.reasoning, TAUNT_HOLD)
      }, wait)
    })
  }

  private launchNextWave() {
    const d = this.pendingDecision
    this.pendingDecision = null
    if (d) this.startWave(this.wave + 1, d.next_wave_composition, d.spawn_zone_bias)
    else this.startWave(this.wave + 1, { standard: 3, fast: 2, tanky: 1 }, 'balanced')
    this.phase = 'playing'
  }

  // ================= COMBAT =================
  private spawnEnemy(type: EnemyType, x: number, y: number) {
    const cfg = ENEMY_CONFIG[type]
    this.enemies.push({
      id: this.nextEnemyId++, type,
      x, y, vx: 0, vy: 0,
      hp: cfg.hp, maxHp: cfg.hp,
      speed: cfg.speed, damage: cfg.damage, radius: cfg.radius,
      attackCd: 0, flash: 0, spawnT: 0.55, dead: false,
    })
  }

  private fireBullet() {
    const p = this.player
    const d = Math.hypot(p.aimX, p.aimY) || 1
    const nx = p.aimX / d, ny = p.aimY / d
    this.projectiles.push({
      x: p.x + nx * 0.9, y: p.y + ny * 0.9,
      vx: nx * PLAYER.bulletSpeed, vy: ny * PLAYER.bulletSpeed,
      dmg: PLAYER.bulletDmg, life: PLAYER.bulletLife, dead: false,
    })
    sfx('shoot')
  }

  private tryMelee() {
    const p = this.player
    if (p.meleeCd > 0) return
    let hit = false
    for (const e of this.enemies) {
      if (e.dead || e.spawnT > 0) continue
      if (Math.hypot(e.x - p.x, e.y - p.y) <= PLAYER.meleeRange + e.radius) {
        e.hp -= PLAYER.meleeDmg
        e.flash = 1
        hit = true
        if (e.hp <= 0) this.killEnemy(e, 'melee')
      }
    }
    if (hit) { p.meleeCd = PLAYER.meleeCooldown; sfx('melee') }
  }

  private tryPurge() {
    const p = this.player
    if (p.purgeCd > 0) return
    p.purgeCd = PLAYER.purgeCooldown
    sfx('purge')
    for (let i = 0; i < 26; i++) {
      const a = (i / 26) * Math.PI * 2
      this.particles.push({
        x: p.x, y: p.y,
        vx: Math.cos(a) * 11, vy: Math.sin(a) * 11,
        life: 0.5, maxLife: 0.5, color: '#9ef0b3', size: 0.28,
      })
    }
    for (const e of this.enemies) {
      if (e.dead || e.spawnT > 0) continue
      if (Math.hypot(e.x - p.x, e.y - p.y) <= PLAYER.purgeRadius + e.radius) {
        e.hp -= PLAYER.purgeDmg
        e.flash = 1
        if (e.hp <= 0) this.killEnemy(e, 'ranged')
      }
    }
  }

  private killEnemy(e: Enemy, kind: 'ranged' | 'melee') {
    if (e.dead) return
    e.dead = true
    const dist = Math.hypot(e.x - this.player.x, e.y - this.player.y)
    this.logger.trackKill({ kind, distance: Math.round(dist * 10) / 10, enemyType: e.type })
    this.kills++
    this.score += ENEMY_CONFIG[e.type].score
    UI.setScore(this.score)
    sfx('die')
    const tint = this.theme.enemyTint[e.type]
    for (let i = 0; i < 10; i++) {
      const a = Math.random() * Math.PI * 2
      const sp = 2 + Math.random() * 5
      this.particles.push({
        x: e.x, y: e.y, vx: Math.cos(a) * sp, vy: Math.sin(a) * sp,
        life: 0.4 + Math.random() * 0.25, maxLife: 0.65, color: tint, size: 0.14 + Math.random() * 0.12,
      })
    }
  }

  private damagePlayer(dmg: number) {
    const p = this.player
    if (p.invuln > 0 || this.phase !== 'playing') return
    p.hp -= dmg
    p.invuln = PLAYER.invulnTime
    p.hitFlash = 1
    UI.setHp(Math.max(0, p.hp) / PLAYER.maxHp)
    sfx('hurt')
    if (p.hp <= 0) this.gameOver()
  }

  // ================= GAME OVER / AUTOPSY =================
  private async gameOver() {
    this.phase = 'over'
    UI.setControlsVisible(false)
    UI.hideTaunt()
    sfx('over')

    const { report, archetypeKey } = await autopsy(
      this.history, this.profile, this.wave, this.kills, this.score
    )

    recordRun(this.profile, {
      waves_survived: this.wave,
      score: this.score,
      kills: this.kills,
      archetype: archetypeKey,
      cause_of_death: report.cause_of_death,
    }, this.history)
    saveProfile(this.profile)

    UI.showGameOver({
      run: this.profile.run_count,
      spec: `#${pad3(this.profile.specimen_number)}`,
      wave: this.wave,
      kills: this.kills,
      score: this.score,
      archetype: report.archetype,
      cause: report.cause_of_death,
      weak: report.weakness_documented,
      adapt: report.adaptability_index,
      remark: report.closing_remark,
      source: report.source,
    })
    this.lastReport = report
  }

  private lastReport: import('./types').AutopsyReport | null = null

  shareReport() {
    if (!this.lastReport) return
    const text = shareText(this.profile, this.wave, this.kills, this.score, this.lastReport)
    UI.copyText(text).then(ok => {
      UI.ui.shareBtn.textContent = ok ? '✓ COPIED' : 'SHARE REPORT'
      setTimeout(() => { UI.ui.shareBtn.textContent = 'SHARE REPORT' }, 1600)
    })
  }

  // ================= INPUT =================
  bindInput() {
    window.addEventListener('keydown', e => {
      this.keys.add(e.code)
      if (e.code === 'Space') {
        e.preventDefault()
        if (this.phase === 'menu') this.start()
        else if (this.phase === 'playing') this.tryPurge()
      }
      if (e.code === 'Enter' && this.phase === 'menu') this.start()
    })
    window.addEventListener('keyup', e => this.keys.delete(e.code))

    const c = this.canvas
    const toWorld = (cx: number, cy: number) => this.screenToWorld(cx, cy)

    c.addEventListener('mousedown', e => { this.mouse.down = true; this.mouse.x = e.clientX; this.mouse.y = e.clientY })
    window.addEventListener('mousemove', e => { this.mouse.x = e.clientX; this.mouse.y = e.clientY })
    window.addEventListener('mouseup', () => { this.mouse.down = false })

    c.addEventListener('touchstart', e => {
      e.preventDefault()
      unlockAudio()
      for (const t of Array.from(e.changedTouches)) {
        if (t.clientX < innerWidth * 0.5 && !this.touchMove) {
          this.touchMove = { id: t.identifier, ox: t.clientX, oy: t.clientY, x: t.clientX, y: t.clientY }
        } else if (!this.touchAim) {
          this.touchAim = { id: t.identifier, x: t.clientX, y: t.clientY, active: true }
        }
      }
    }, { passive: false })
    c.addEventListener('touchmove', e => {
      e.preventDefault()
      for (const t of Array.from(e.changedTouches)) {
        if (this.touchMove?.id === t.identifier) { this.touchMove.x = t.clientX; this.touchMove.y = t.clientY }
        if (this.touchAim?.id === t.identifier) { this.touchAim.x = t.clientX; this.touchAim.y = t.clientY }
      }
    }, { passive: false })
    const endTouch = (e: TouchEvent) => {
      for (const t of Array.from(e.changedTouches)) {
        if (this.touchMove?.id === t.identifier) this.touchMove = null
        if (this.touchAim?.id === t.identifier) this.touchAim = null
      }
    }
    c.addEventListener('touchend', endTouch)
    c.addEventListener('touchcancel', endTouch)

    UI.ui.specialBtn.addEventListener('touchstart', e => { e.stopPropagation(); e.preventDefault(); if (this.phase === 'playing') this.tryPurge() }, { passive: false })
    UI.ui.specialBtn.addEventListener('mousedown', e => { e.stopPropagation(); if (this.phase === 'playing') this.tryPurge() })
    UI.ui.startBtn.addEventListener('click', () => { if (this.phase === 'menu') this.start() })
    UI.ui.restartBtn.addEventListener('click', () => this.start())
    UI.ui.shareBtn.addEventListener('click', () => this.shareReport())
  }

  private screenToWorld(cx: number, cy: number) {
    const r = this.canvas.getBoundingClientRect()
    const sx = (cx - r.left) / r.width
    const sy = (cy - r.top) / r.height
    return {
      x: (sx - 0.5) * WORLD_W,
      y: (sy - 0.5) * WORLD_W, // uniform scale from width
    }
  }

  private moveInput(): { x: number; y: number } {
    let x = 0, y = 0
    if (this.keys.has('KeyA') || this.keys.has('ArrowLeft')) x -= 1
    if (this.keys.has('KeyD') || this.keys.has('ArrowRight')) x += 1
    if (this.keys.has('KeyW') || this.keys.has('ArrowUp')) y -= 1
    if (this.keys.has('KeyS') || this.keys.has('ArrowDown')) y += 1
    if (this.touchMove) {
      const dx = (this.touchMove.x - this.touchMove.ox) / 60
      const dy = (this.touchMove.y - this.touchMove.oy) / 60
      const m = Math.hypot(dx, dy)
      const cap = Math.min(1, m)
      if (m > 0.15) { x += (dx / m) * cap; y += (dy / m) * cap }
    }
    const m = Math.hypot(x, y)
    return m > 1 ? { x: x / m, y: y / m } : { x, y }
  }

  private aimInput(): { x: number; y: number; firing: boolean } {
    const p = this.player
    if (this.touchAim) {
      const w = this.screenToWorld(this.touchAim.x, this.touchAim.y)
      const dx = w.x - p.x, dy = w.y - p.y
      const m = Math.hypot(dx, dy)
      if (m > 0.01) return { x: dx / m, y: dy / m, firing: true }
    }
    if (this.mouse.down) {
      const w = toWorldSafe(this, this.mouse.x, this.mouse.y)
      const dx = w.x - p.x, dy = w.y - p.y
      const m = Math.hypot(dx, dy)
      if (m > 0.01) return { x: dx / m, y: dy / m, firing: true }
    }
    // Auto-aim fallback (keyboard): nearest enemy
    let best: Enemy | null = null; let bd = Infinity
    for (const e of this.enemies) {
      if (e.dead || e.spawnT > 0) continue
      const d = Math.hypot(e.x - p.x, e.y - p.y)
      if (d < bd) { bd = d; best = e }
    }
    if (best) {
      const dx = best.x - p.x, dy = best.y - p.y
      const m = Math.hypot(dx, dy)
      return { x: dx / m, y: dy / m, firing: true } // keyboard mode: auto-fire at nearest enemy
    }
    return { x: p.aimX, y: p.aimY, firing: false }
  }

  // ================= UPDATE =================
  update(dt: number) {
    this.time += dt
    const p = this.player

    if (this.phase === 'intermission') {
      this.intermissionT += dt
      if (this.pendingDecision && this.intermissionT > TAUNT_HOLD + 0.4) this.launchNextWave()
    }

    if (this.phase === 'playing') {
      // spawn queue
      for (const s of this.spawnQueue) {
        if (this.time >= s.t && !('done' in s)) {
          (s as { done?: boolean }).done = true
          this.spawnEnemy(s.type, s.x, s.y)
        }
      }
      this.spawnQueue = this.spawnQueue.filter(s => !('done' in s))

      // player movement
      const mv = this.moveInput()
      const pvx = mv.x * PLAYER.speed, pvy = mv.y * PLAYER.speed
      p.x += pvx * dt
      p.y += pvy * dt
      p.x = Math.max(-WORLD_W / 2 + PLAYER.radius, Math.min(WORLD_W / 2 - PLAYER.radius, p.x))
      p.y = Math.max(-WORLD_H / 2 + PLAYER.radius, Math.min(WORLD_H / 2 - PLAYER.radius, p.y))
      for (const pil of PILLARS) {
        const dx = p.x - pil.x, dy = p.y - pil.y
        const d = Math.hypot(dx, dy)
        const minD = pil.r + PLAYER.radius
        if (d < minD && d > 0.001) {
          p.x = pil.x + (dx / d) * minD
          p.y = pil.y + (dy / d) * minD
        }
      }

      // aim + fire
      const aim = this.aimInput()
      if (aim.firing || Math.hypot(aim.x, aim.y) > 0.01) {
        p.aimX = aim.x; p.aimY = aim.y
      }
      if (aim.firing) {
        p.fireCd -= dt
        if (p.fireCd <= 0) { this.fireBullet(); p.fireCd = PLAYER.fireCooldown }
      } else {
        p.fireCd = Math.min(p.fireCd, PLAYER.fireCooldown)
      }

      // auto-melee
      p.meleeCd -= dt
      this.tryMelee()
      p.purgeCd = Math.max(0, p.purgeCd - dt)
      UI.setPurgeCooldown(p.purgeCd, PLAYER.purgeCooldown)
      p.invuln = Math.max(0, p.invuln - dt)
      p.hitFlash = Math.max(0, p.hitFlash - dt * 3)

      // behavior logging
      this.logger.trackFrame(dt, p.x, p.y, pvx, pvy)

      // enemies
      for (const e of this.enemies) {
        if (e.dead) continue
        if (e.spawnT > 0) { e.spawnT -= dt; continue }
        e.flash = Math.max(0, e.flash - dt * 4)
        e.attackCd -= dt

        // seek player with separation + pillar avoidance
        const dx = p.x - e.x, dy = p.y - e.y
        const dist = Math.hypot(dx, dy) || 0.001
        let dirX = dx / dist, dirY = dy / dist
        for (const o of this.enemies) {
          if (o === e || o.dead) continue
          const ox = e.x - o.x, oy = e.y - o.y
          const od = Math.hypot(ox, oy)
          if (od < (e.radius + o.radius) * 1.4 && od > 0.001) {
            dirX += (ox / od) * 0.7
            dirY += (oy / od) * 0.7
          }
        }
        for (const pil of PILLARS) {
          const px = e.x - pil.x, py = e.y - pil.y
          const pd = Math.hypot(px, py)
          if (pd < pil.r + e.radius + 0.6 && pd > 0.001) {
            dirX += (px / pd) * 1.5
            dirY += (py / pd) * 1.5
          }
        }
        const dm = Math.hypot(dirX, dirY) || 0.001
        e.vx = (dirX / dm) * e.speed
        e.vy = (dirY / dm) * e.speed
        e.x += e.vx * dt
        e.y += e.vy * dt
        e.x = Math.max(-WORLD_W / 2 + e.radius, Math.min(WORLD_W / 2 - e.radius, e.x))
        e.y = Math.max(-WORLD_H / 2 + e.radius, Math.min(WORLD_H / 2 - e.radius, e.y))

        if (dist <= ENEMY_ATTACK_RANGE + e.radius && e.attackCd <= 0) {
          e.attackCd = ENEMY_ATTACK_CD
          this.damagePlayer(e.damage)
        }
      }

      // projectiles
      for (const b of this.projectiles) {
        if (b.dead) continue
        b.x += b.vx * dt
        b.y += b.vy * dt
        b.life -= dt
        if (b.life <= 0 || Math.abs(b.x) > WORLD_W / 2 || Math.abs(b.y) > WORLD_H / 2) { b.dead = true; continue }
        let blocked = false
        for (const pil of PILLARS) {
          if (Math.hypot(b.x - pil.x, b.y - pil.y) < pil.r) { blocked = true; break }
        }
        if (blocked) { b.dead = true; continue }
        for (const e of this.enemies) {
          if (e.dead || e.spawnT > 0) continue
          if (Math.hypot(b.x - e.x, b.y - e.y) < e.radius + 0.18) {
            e.hp -= b.dmg
            e.flash = 1
            b.dead = true
            sfx('hit')
            if (e.hp <= 0) this.killEnemy(e, 'ranged')
            break
          }
        }
      }

      this.enemies = this.enemies.filter(e => !e.dead)
      this.projectiles = this.projectiles.filter(b => !b.dead)

      // wave complete?
      if (this.spawnQueue.length === 0 && this.enemies.length === 0) {
        sfx('clear')
        this.score += 50 * this.wave
        UI.setScore(this.score)
        this.endWave()
      }
    }

    // particles always tick
    for (const pt of this.particles) {
      pt.x += pt.vx * dt
      pt.y += pt.vy * dt
      pt.vx *= 0.92
      pt.vy *= 0.92
      pt.life -= dt
    }
    this.particles = this.particles.filter(pt => pt.life > 0)
  }

  // ================= RENDER =================
  render() {
    const c = this.canvas, ctx = this.ctx
    const w = c.width, h = c.height
    const scale = w / WORLD_W
    const toX = (x: number) => (x + WORLD_W / 2) * scale
    const toY = (y: number) => h / 2 + y * scale
    const t = this.theme

    // bg
    ctx.fillStyle = t.bg
    ctx.fillRect(0, 0, w, h)

    // floor
    ctx.fillStyle = t.floor
    ctx.fillRect(toX(-WORLD_W / 2), toY(-WORLD_H / 2), WORLD_W * scale, WORLD_H * scale)

    // grid
    ctx.strokeStyle = t.grid
    ctx.lineWidth = 1
    ctx.beginPath()
    for (let gx = -WORLD_W / 2; gx <= WORLD_W / 2; gx += 2) {
      ctx.moveTo(toX(gx), toY(-WORLD_H / 2)); ctx.lineTo(toX(gx), toY(WORLD_H / 2))
    }
    for (let gy = -WORLD_H / 2; gy <= WORLD_H / 2; gy += 2) {
      ctx.moveTo(toX(-WORLD_W / 2), toY(gy)); ctx.lineTo(toX(WORLD_W / 2), toY(gy))
    }
    ctx.stroke()

    // arena edge
    ctx.strokeStyle = t.accent
    ctx.globalAlpha = 0.35
    ctx.lineWidth = 2
    ctx.strokeRect(toX(-WORLD_W / 2), toY(-WORLD_H / 2), WORLD_W * scale, WORLD_H * scale)
    ctx.globalAlpha = 1

    // pillars
    for (const p of PILLARS) {
      ctx.fillStyle = t.pillar
      ctx.beginPath(); ctx.arc(toX(p.x), toY(p.y), p.r * scale, 0, Math.PI * 2); ctx.fill()
      ctx.strokeStyle = t.pillarEdge
      ctx.lineWidth = 2
      ctx.stroke()
    }

    // spawn telegraphs (intermission pending spawns)
    if (this.phase === 'playing') {
      for (const s of this.spawnQueue) {
        const pulse = 0.4 + 0.3 * Math.sin(this.time * 8)
        ctx.strokeStyle = t.accent
        ctx.globalAlpha = pulse
        ctx.beginPath(); ctx.arc(toX(s.x), toY(s.y), 0.7 * scale, 0, Math.PI * 2); ctx.stroke()
        ctx.globalAlpha = 1
      }
    }

    // enemies
    for (const e of this.enemies) {
      const spawnScale = e.spawnT > 0 ? Math.max(0.15, 1 - e.spawnT / 0.55) : 1
      const r = e.radius * scale * spawnScale
      const tint = this.theme.enemyTint[e.type]
      ctx.fillStyle = e.flash > 0.3 ? '#ffffff' : tint
      ctx.beginPath(); ctx.arc(toX(e.x), toY(e.y), r, 0, Math.PI * 2); ctx.fill()
      // direction nub toward player
      const p = this.player
      const dx = p.x - e.x, dy = p.y - e.y
      const dm = Math.hypot(dx, dy) || 1
      ctx.strokeStyle = 'rgba(0,0,0,0.5)'
      ctx.lineWidth = 2
      ctx.beginPath()
      ctx.moveTo(toX(e.x), toY(e.y))
      ctx.lineTo(toX(e.x + (dx / dm) * e.radius * 1.25), toY(e.y + (dy / dm) * e.radius * 1.25))
      ctx.stroke()
      // hp bar when damaged
      if (e.hp < e.maxHp) {
        const bw = r * 1.6
        ctx.fillStyle = 'rgba(0,0,0,0.55)'
        ctx.fillRect(toX(e.x) - bw / 2, toY(e.y) - r - 6, bw, 3)
        ctx.fillStyle = this.theme.accent
        ctx.fillRect(toX(e.x) - bw / 2, toY(e.y) - r - 6, bw * Math.max(0, e.hp / e.maxHp), 3)
      }
    }

    // projectiles
    for (const b of this.projectiles) {
      ctx.fillStyle = '#d9ffe6'
      ctx.beginPath(); ctx.arc(toX(b.x), toY(b.y), 0.16 * scale, 0, Math.PI * 2); ctx.fill()
      ctx.strokeStyle = 'rgba(158,240,179,0.5)'
      ctx.beginPath()
      ctx.moveTo(toX(b.x), toY(b.y))
      ctx.lineTo(toX(b.x - b.vx * 0.03), toY(b.y - b.vy * 0.03))
      ctx.stroke()
    }

    // particles
    for (const pt of this.particles) {
      ctx.globalAlpha = Math.max(0, pt.life / pt.maxLife)
      ctx.fillStyle = pt.color
      ctx.beginPath(); ctx.arc(toX(pt.x), toY(pt.y), pt.size * scale, 0, Math.PI * 2); ctx.fill()
    }
    ctx.globalAlpha = 1

    // player
    if (this.phase === 'playing' || this.phase === 'intermission') {
      const p = this.player
      const pr = PLAYER.radius * scale
      // purge radius hint
      if (p.purgeCd <= 0) {
        ctx.strokeStyle = this.theme.accent
        ctx.globalAlpha = 0.18 + 0.1 * Math.sin(this.time * 3)
        ctx.beginPath(); ctx.arc(toX(p.x), toY(p.y), PLAYER.purgeRadius * scale, 0, Math.PI * 2); ctx.stroke()
        ctx.globalAlpha = 1
      }
      // body
      ctx.fillStyle = p.hitFlash > 0.4 ? '#ff4d5e' : '#e8fff0'
      ctx.beginPath(); ctx.arc(toX(p.x), toY(p.y), pr, 0, Math.PI * 2); ctx.fill()
      ctx.strokeStyle = '#9ef0b3'
      ctx.lineWidth = 2
      ctx.stroke()
      // aim barrel
      const ad = Math.hypot(p.aimX, p.aimY) || 1
      ctx.strokeStyle = '#9ef0b3'
      ctx.lineWidth = 3
      ctx.beginPath()
      ctx.moveTo(toX(p.x), toY(p.y))
      ctx.lineTo(toX(p.x + (p.aimX / ad) * 1.4), toY(p.y + (p.aimY / ad) * 1.4))
      ctx.stroke()
    }

    // fog vignette
    const grad = ctx.createRadialGradient(w / 2, h / 2, Math.min(w, h) * 0.34, w / 2, h / 2, Math.max(w, h) * 0.75)
    grad.addColorStop(0, 'rgba(0,0,0,0)')
    grad.addColorStop(1, t.fog)
    ctx.fillStyle = grad
    ctx.fillRect(0, 0, w, h)

    // low hp vignette
    if ((this.phase === 'playing') && this.player.hp / PLAYER.maxHp < 0.3) {
      const pulse = 0.12 + 0.1 * Math.sin(this.time * 6)
      const g2 = ctx.createRadialGradient(w / 2, h / 2, Math.min(w, h) * 0.3, w / 2, h / 2, Math.max(w, h) * 0.7)
      g2.addColorStop(0, 'rgba(255,0,40,0)')
      g2.addColorStop(1, `rgba(255,0,40,${pulse})`)
      ctx.fillStyle = g2
      ctx.fillRect(0, 0, w, h)
    }

    // touch joystick visuals
    if (this.touchMove) {
      ctx.strokeStyle = 'rgba(158,240,179,0.4)'
      ctx.lineWidth = 2
      ctx.beginPath(); ctx.arc(this.touchMove.ox, this.touchMove.oy, 54, 0, Math.PI * 2); ctx.stroke()
      ctx.fillStyle = 'rgba(158,240,179,0.5)'
      const kx = this.touchMove.ox + Math.max(-50, Math.min(50, this.touchMove.x - this.touchMove.ox))
      const ky = this.touchMove.oy + Math.max(-50, Math.min(50, this.touchMove.y - this.touchMove.oy))
      ctx.beginPath(); ctx.arc(kx, ky, 20, 0, Math.PI * 2); ctx.fill()
    }
  }
}

function toWorldSafe(g: Game, cx: number, cy: number) {
  const rect = (g as unknown as { canvas: HTMLCanvasElement }).canvas.getBoundingClientRect()
  const sx = (cx - rect.left) / rect.width
  const sy = (cy - rect.top) / rect.height
  return { x: (sx - 0.5) * WORLD_W, y: (sy - 0.5) * WORLD_W }
}
