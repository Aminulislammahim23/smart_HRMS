import { z } from 'zod'

/** Mirrors PasswordPolicy in the backend (which stays the final authority). */
export const passwordSchema = z
  .string()
  .min(8, 'At least 8 characters.')
  .max(128, 'At most 128 characters.')
  .refine((value) => /\p{L}/u.test(value) && /\d/.test(value), 'Use at least one letter and one digit.')
