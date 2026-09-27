import { z } from 'zod'
import { todayInput } from './formatters'

/*
 * Client-side rules that mirror the backend's DTO annotations ([Required], [MaxLength], [EmailAddress], [Phone],
 * regular expressions). They give instant feedback; the backend stays the final authority and its messages are
 * shown when it rejects something these rules allow (duplicates, inactive departments, ...).
 */

export function requiredText(label: string, max: number) {
  return z
    .string()
    .trim()
    .min(1, `${label} is required.`)
    .max(max, `${label} must be ${max} characters or fewer.`)
}

export function optionalText(label: string, max: number) {
  return z.string().trim().max(max, `${label} must be ${max} characters or fewer.`)
}

/** Like .NET's [EmailAddress]: one "@" with text on both sides. */
const EMAIL_PATTERN = /^[^@\s]+@[^@\s]+$/

export function requiredEmail(max = 200) {
  return requiredText('Email', max).regex(EMAIL_PATTERN, 'Enter a valid email address.')
}

export function optionalEmail(max = 200) {
  return optionalText('Email', max).refine((value) => value === '' || EMAIL_PATTERN.test(value), 'Enter a valid email address.')
}

/** Close to .NET's [Phone]: digits with optional +, spaces, dashes, dots and parentheses. */
const PHONE_PATTERN = /^\+?[\d\s\-().]*\d[\d\s\-().]*$/

export function requiredPhone(max = 30) {
  return requiredText('Phone', max).regex(PHONE_PATTERN, 'Enter a valid phone number.')
}

export function optionalPhone(max = 30) {
  return optionalText('Phone', max).refine((value) => value === '' || PHONE_PATTERN.test(value), 'Enter a valid phone number.')
}

export function requiredDate(label: string) {
  return z.string().min(1, `${label} is required.`)
}

export function pastDate(label: string) {
  return requiredDate(label).refine((value) => value < todayInput(), `${label} must be in the past.`)
}

/** Selects hold "" until something is chosen. */
export function requiredSelect(label: string) {
  return z.string().min(1, `Select a ${label.toLowerCase()}.`)
}

/** Optional enum select: "" means "not set". */
export function optionalEnum<const T extends readonly [string, ...string[]]>(values: T) {
  return z.union([z.literal(''), z.enum(values)])
}

/** A non-negative decimal with up to 2 decimals (BasicSalary), or blank. */
export const optionalAmount = z
  .string()
  .trim()
  .refine((value) => value === '' || /^\d{1,16}(\.\d{1,2})?$/.test(value), 'Enter a positive amount with up to 2 decimals.')

export interface FileRules {
  maxSizeBytes: number
  extensions: readonly string[]
}

/** Returns an error message, or null when the file satisfies the rules. */
export function validateFile(file: File, rules: FileRules): string | null {
  const dot = file.name.lastIndexOf('.')
  const extension = dot >= 0 ? file.name.slice(dot).toLowerCase() : ''
  if (!rules.extensions.includes(extension)) {
    return `Only ${rules.extensions.join(', ')} files are allowed.`
  }
  if (file.size === 0) {
    return 'The selected file is empty.'
  }
  if (file.size > rules.maxSizeBytes) {
    return `The file must be ${Math.round(rules.maxSizeBytes / (1024 * 1024))} MB or smaller.`
  }
  return null
}
