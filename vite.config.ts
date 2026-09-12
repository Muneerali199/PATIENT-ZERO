import { defineConfig } from 'vite'

export default defineConfig({
  base: '/PATIENT-ZERO/',
  build: { target: 'es2020' },
  server: { host: true }
})
