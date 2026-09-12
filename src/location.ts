import { CITY_MAP } from './config'
import type { ThemeBucket } from './types'
import { FALLBACK_THEME, THEMES } from './themes'

export interface SeedResult {
  bucket: ThemeBucket
  label: string
}

function bucketFromCity(city: string): ThemeBucket | null {
  const c = city.toLowerCase().trim()
  if (!c) return null
  for (const key of Object.keys(CITY_MAP)) {
    if (c.includes(key)) return CITY_MAP[key]
  }
  return null
}

async function reverseGeocode(lat: number, lng: number): Promise<string | null> {
  try {
    const ctl = new AbortController()
    const t = setTimeout(() => ctl.abort(), 4000)
    const res = await fetch(
      `https://api.bigdatacloud.net/data/reverse-geocode-client?latitude=${lat}&longitude=${lng}&localityLanguage=en`,
      { signal: ctl.signal }
    )
    clearTimeout(t)
    if (!res.ok) return null
    const j = await res.json() as { city?: string; locality?: string; principalSubdivision?: string }
    return j.city || j.locality || j.principalSubdivision || null
  } catch {
    return null
  }
}

function getPosition(): Promise<GeolocationPosition | null> {
  return new Promise((resolve) => {
    if (!('geolocation' in navigator)) return resolve(null)
    navigator.geolocation.getCurrentPosition(
      (pos) => resolve(pos),
      () => resolve(null),
      { timeout: 5000, maximumAge: 600000 }
    )
  })
}

/**
 * One-time location read → theme bucket.
 * Demo override: ?theme=coastal|desert|temperate|mountain|urban
 * Fallbacks: city lookup → temperate.
 */
export async function resolveSeed(): Promise<SeedResult> {
  // 1. URL override (demo tool)
  const params = new URLSearchParams(location.search)
  const forced = params.get('theme')
  if (forced && forced in THEMES) {
    const th = THEMES[forced as ThemeBucket]
    return { bucket: th.bucket, label: `SEED OVERRIDE // ${th.label}` }
  }

  // 2. Real geolocation
  const pos = await getPosition()
  if (pos) {
    const city = await reverseGeocode(pos.coords.latitude, pos.coords.longitude)
    if (city) {
      const bucket = bucketFromCity(city)
      if (bucket) {
        return { bucket, label: `SEED: ${city.toUpperCase()} → ${THEMES[bucket].label}` }
      }
      return { bucket: 'temperate', label: `SEED: ${city.toUpperCase()} → ${FALLBACK_THEME.label}` }
    }
  }

  // 3. Fallback
  return { bucket: 'temperate', label: `SEED: UNKNOWN SECTOR → ${FALLBACK_THEME.label}` }
}
