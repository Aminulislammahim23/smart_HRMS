/// <reference types="vite/client" />

interface ImportMetaEnv {
  /** Origin of the SmartHRMS API, e.g. https://hrms.example.com. Empty means the app's own origin. */
  readonly VITE_API_BASE_URL?: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}
