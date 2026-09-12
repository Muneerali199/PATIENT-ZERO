import { Game } from './game'
import { toggleMute, isMuted, unlockAudio } from './sfx'
import { ui } from './ui'

const canvas = document.getElementById('c') as HTMLCanvasElement
const ctx = canvas.getContext('2d')!

function resize() {
  const dpr = Math.min(2, window.devicePixelRatio || 1)
  canvas.width = Math.floor(innerWidth * dpr)
  canvas.height = Math.floor(innerHeight * dpr)
  canvas.style.width = `${innerWidth}px`
  canvas.style.height = `${innerHeight}px`
}
window.addEventListener('resize', resize)
resize()

const game = new Game(canvas, ctx)
game.bindInput()
void game.boot()
;(window as unknown as { __pz: Game }).__pz = game

ui.muteBtn.textContent = isMuted() ? '✕' : '♪'
ui.muteBtn.addEventListener('click', () => {
  ui.muteBtn.textContent = toggleMute() ? '✕' : '♪'
})

// First user gesture unlocks audio
window.addEventListener('pointerdown', unlockAudio, { once: true })

let last = performance.now()
function frame(now: number) {
  const dt = Math.min(0.05, (now - last) / 1000)
  last = now
  game.update(dt)
  game.render()
  requestAnimationFrame(frame)
}
requestAnimationFrame(frame)
