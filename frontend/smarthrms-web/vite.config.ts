import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { defineConfig, loadEnv } from 'vite'

// https://vite.dev/config/
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '')
  // By default the app calls the API directly (VITE_API_BASE_URL + the API's CORS policy). This proxy is the
  // fallback for an empty VITE_API_BASE_URL: Vite then forwards /api and /uploads to the API itself.
  const apiTarget = env.VITE_API_PROXY_TARGET || 'http://localhost:5099'

  return {
    plugins: [react(), tailwindcss()],
    server: {
      proxy: {
        '/api': { target: apiTarget, changeOrigin: true },
        '/uploads': { target: apiTarget, changeOrigin: true },
      },
    },
  }
})
