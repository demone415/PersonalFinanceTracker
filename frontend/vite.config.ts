import path from 'node:path'
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
    // Select @undecaf/zbar-wasm's "inlined" build so the WASM is embedded as
    // base64 rather than fetched as a separate asset — no extra served file and
    // identical behaviour in dev, build, and tests.
    conditions: ['zbar-inlined', 'module', 'browser', 'development|production'],
  },
  server: {
    port: 5173,
  },
})
