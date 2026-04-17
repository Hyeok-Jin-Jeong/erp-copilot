import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      // 개발 시 CORS 우회: /api/* → PA999S1 서버로 프록시
      '/api': {
        target: process.env.VITE_API_BASE_URL ?? 'http://localhost:5000',
        changeOrigin: true,
      },
    },
  },
})
