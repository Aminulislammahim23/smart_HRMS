import { createContext, useContext } from 'react'

/**
 * Whether the profile being shown may be edited. Employees see their own profile read-only (only HR/Admin may
 * change profile records, and the API enforces that); this only hides the buttons they can't use.
 */
export const ProfileEditingContext = createContext(true)

export function useCanEditProfile(): boolean {
  return useContext(ProfileEditingContext)
}
