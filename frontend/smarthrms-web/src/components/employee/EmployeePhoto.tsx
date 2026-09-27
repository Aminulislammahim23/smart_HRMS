import { useState } from 'react'
import { assetUrl, initials } from '../../utils/formatters'

const SIZES = {
  sm: 'size-9 text-xs',
  md: 'size-12 text-sm',
  lg: 'size-20 text-xl',
  xl: 'size-28 text-3xl',
} as const

interface EmployeePhotoProps {
  photoUrl: string | null
  firstName: string
  lastName: string
  size?: keyof typeof SIZES
}

/** The employee's photo, or their initials when there is none or it fails to load. */
export function EmployeePhoto({ photoUrl, firstName, lastName, size = 'md' }: EmployeePhotoProps) {
  const src = assetUrl(photoUrl)
  const [failedSrc, setFailedSrc] = useState<string | null>(null)
  const showImage = src !== null && failedSrc !== src

  return (
    <div className={`avatar ${showImage ? '' : 'avatar-placeholder'}`}>
      <div className={`rounded-full ${SIZES[size]} ${showImage ? 'bg-base-200' : 'bg-primary/15 text-primary'}`}>
        {showImage ? (
          <img src={src} alt={`${firstName} ${lastName}`} loading="lazy" onError={() => setFailedSrc(src)} />
        ) : (
          <span className="font-semibold">{initials(firstName, lastName)}</span>
        )}
      </div>
    </div>
  )
}
