import { ImagePlus, Trash2, Undo2 } from 'lucide-react'
import { useId, useRef, useState, type ChangeEvent } from 'react'
import { PHOTO_RULES } from '../../utils/constants'
import { validateFile } from '../../utils/validators'
import { ConfirmDialog } from '../common/ConfirmDialog'
import { EmployeePhoto } from './EmployeePhoto'

interface EmployeePhotoFieldProps {
  firstName: string
  lastName: string
  /** The saved photo (edit mode). */
  currentPhotoUrl: string | null
  file: File | null
  onFileChange: (file: File | null) => void
  /** Removes the saved photo right away (DELETE /api/employees/{id}/photo). Edit mode only. */
  onRemoveCurrent?: () => Promise<void>
}

/**
 * Picks, validates and previews a photo. The file is uploaded by the page after the employee is saved,
 * because the backend takes photos on their own endpoint (PUT /api/employees/{id}/photo, multipart "photo").
 */
export function EmployeePhotoField({ firstName, lastName, currentPhotoUrl, file, onFileChange, onRemoveCurrent }: EmployeePhotoFieldProps) {
  const inputId = useId()
  const inputRef = useRef<HTMLInputElement>(null)
  const [preview, setPreview] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [savedPhotoUrl, setSavedPhotoUrl] = useState(currentPhotoUrl)
  const [confirmRemove, setConfirmRemove] = useState(false)

  const select = (event: ChangeEvent<HTMLInputElement>) => {
    const selected = event.target.files?.[0]
    event.target.value = ''
    if (!selected) return

    const problem = validateFile(selected, PHOTO_RULES)
    setError(problem)
    if (problem) return

    onFileChange(selected)
    const reader = new FileReader()
    reader.onload = () => setPreview(typeof reader.result === 'string' ? reader.result : null)
    reader.readAsDataURL(selected)
  }

  const clearSelection = () => {
    onFileChange(null)
    setPreview(null)
    setError(null)
  }

  return (
    <div className="flex flex-col items-center gap-3 text-center">
      {file && preview ? (
        <div className="avatar">
          <div className="size-28 rounded-full ring-2 ring-primary ring-offset-2 ring-offset-base-100">
            <img src={preview} alt="Selected photo preview" />
          </div>
        </div>
      ) : (
        <EmployeePhoto photoUrl={savedPhotoUrl} firstName={firstName || '?'} lastName={lastName} size="xl" />
      )}

      <input ref={inputRef} id={inputId} type="file" accept={PHOTO_RULES.accept} className="hidden" onChange={select} />

      <div className="flex flex-wrap justify-center gap-2">
        <button type="button" className="btn btn-sm" onClick={() => inputRef.current?.click()}>
          <ImagePlus className="size-4" />
          {savedPhotoUrl || file ? 'Change photo' : 'Choose photo'}
        </button>
        {file ? (
          <button type="button" className="btn btn-sm btn-ghost" onClick={clearSelection}>
            <Undo2 className="size-4" /> Undo
          </button>
        ) : (
          savedPhotoUrl &&
          onRemoveCurrent && (
            <button type="button" className="btn btn-sm btn-ghost text-error" onClick={() => setConfirmRemove(true)}>
              <Trash2 className="size-4" /> Remove
            </button>
          )
        )}
      </div>

      {error ? (
        <p className="text-sm text-error" role="alert">
          {error}
        </p>
      ) : (
        <p className="text-xs text-base-content/60">
          {file ? `${file.name} will be uploaded when you save.` : 'JPG, PNG or WEBP, up to 5 MB.'}
        </p>
      )}

      {onRemoveCurrent && (
        <ConfirmDialog
          open={confirmRemove}
          title="Remove photo?"
          message="The current photo will be deleted now. The rest of the employee record is not affected."
          confirmLabel="Remove photo"
          onCancel={() => setConfirmRemove(false)}
          onConfirm={async () => {
            await onRemoveCurrent()
            setSavedPhotoUrl(null)
            setConfirmRemove(false)
          }}
        />
      )}
    </div>
  )
}
