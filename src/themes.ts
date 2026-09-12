import type { ThemeBucket } from './types'

export interface Theme {
  bucket: ThemeBucket
  label: string
  bg: string
  floor: string
  grid: string
  pillar: string
  pillarEdge: string
  accent: string
  enemyTint: Record<string, string>
  fog: string
}

export const THEMES: Record<ThemeBucket, Theme> = {
  temperate: {
    bucket: 'temperate', label: 'TEMPERATE / ROTTED STRAIN',
    bg: '#070b07', floor: '#111a12', grid: '#1a281c',
    pillar: '#26332a', pillarEdge: '#3a4f3e', accent: '#7da05a',
    enemyTint: { standard: '#6f9b4a', fast: '#9bc46f', tanky: '#42582f' },
    fog: 'rgba(20, 34, 22, 0.16)'
  },
  coastal: {
    bucket: 'coastal', label: 'COASTAL / DROWNED STRAIN',
    bg: '#050d14', floor: '#0e1c26', grid: '#152836',
    pillar: '#1d3441', pillarEdge: '#2d5163', accent: '#6ec6e9',
    enemyTint: { standard: '#4a8dab', fast: '#7fb8cf', tanky: '#2d566b' },
    fog: 'rgba(16, 40, 58, 0.2)'
  },
  desert: {
    bucket: 'desert', label: 'DESERT / SCORCHED STRAIN',
    bg: '#140c04', floor: '#221708', grid: '#33230d',
    pillar: '#4a3218', pillarEdge: '#6d4b22', accent: '#e8a33d',
    enemyTint: { standard: '#d97b29', fast: '#eba253', tanky: '#8f4c14' },
    fog: 'rgba(66, 42, 12, 0.18)'
  },
  mountain: {
    bucket: 'mountain', label: 'MOUNTAIN / FROZEN STRAIN',
    bg: '#0a0e15', floor: '#161d29', grid: '#212b3b',
    pillar: '#2a3140', pillarEdge: '#46536b', accent: '#bcd4e6',
    enemyTint: { standard: '#a8cfe0', fast: '#d3e8f2', tanky: '#6b8299' },
    fog: 'rgba(180, 205, 225, 0.1)'
  },
  urban: {
    bucket: 'urban', label: 'URBAN / INFECTED STRAIN',
    bg: '#0a0a10', floor: '#15151f', grid: '#20202e',
    pillar: '#26263a', pillarEdge: '#3d3d5c', accent: '#d63cf0',
    enemyTint: { standard: '#c04ae0', fast: '#e079f0', tanky: '#7c2b96' },
    fog: 'rgba(50, 20, 66, 0.2)'
  },
}

export const FALLBACK_THEME = THEMES.temperate
