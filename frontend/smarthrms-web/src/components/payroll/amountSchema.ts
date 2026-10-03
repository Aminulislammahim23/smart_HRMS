import { z } from 'zod'

/** Upper limit for a monthly amount; the database allows more (decimal(18,2)), the backend stays the authority. */
export const MAX_AMOUNT = 1_000_000_000_000

/** A money amount from a number input: 0 or more, at most 2 decimals. */
export const amountSchema = z.coerce
  .number({ message: 'Enter an amount.' })
  .min(0, 'Cannot be negative.')
  .max(MAX_AMOUNT, 'Too large.')
  .refine((value) => Number.isFinite(value) && Math.abs(Math.round(value * 100) - value * 100) < 1e-6, 'Use at most 2 decimals.')
