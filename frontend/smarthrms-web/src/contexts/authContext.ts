import { createContext } from 'react'
import type { AuthState } from '../types/auth'

export const AuthContext = createContext<AuthState | null>(null)
