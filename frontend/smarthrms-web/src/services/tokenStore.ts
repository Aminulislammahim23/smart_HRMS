/**
 * Holds the access token for the API client. It lives in sessionStorage: it survives a page reload but not closing
 * the tab, which limits how long a forgotten session stays usable on a shared computer. Storage can be unavailable
 * (private mode, blocked site data), so every access is guarded and the token is also kept in memory.
 */
const KEY = 'smarthrms.session'

interface StoredSession {
  token: string
  expiresAt: string
}

let memory: StoredSession | null = null
const listeners = new Set<() => void>()

function read(): StoredSession | null {
  if (memory) return memory
  try {
    const raw = sessionStorage.getItem(KEY)
    memory = raw ? (JSON.parse(raw) as StoredSession) : null
  } catch {
    memory = null
  }
  return memory
}

export const tokenStore = {
  /** The token, or null when there is none or it has expired. */
  get(): string | null {
    const session = read()
    if (!session) return null
    if (Date.parse(session.expiresAt) <= Date.now()) {
      tokenStore.clear()
      return null
    }
    return session.token
  },

  set(token: string, expiresAt: string) {
    memory = { token, expiresAt }
    try {
      sessionStorage.setItem(KEY, JSON.stringify(memory))
    } catch {
      // Memory only: the session ends on reload.
    }
  },

  clear() {
    memory = null
    try {
      sessionStorage.removeItem(KEY)
    } catch {
      // Nothing stored.
    }
  },

  /** Called when the API rejects the token (401), so the app can return to the sign-in page. */
  onUnauthorized(listener: () => void): () => void {
    listeners.add(listener)
    return () => listeners.delete(listener)
  },

  notifyUnauthorized() {
    listeners.forEach((listener) => listener())
  },
}
