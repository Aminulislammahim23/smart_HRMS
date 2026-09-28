import { zodResolver } from '@hookform/resolvers/zod'
import { useState, type ChangeEvent } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import {
  DOCUMENT_TYPES,
  type DocumentMetadata,
  type EmployeeDocument,
  type UploadProgressHandler,
} from '../../types/document'
import { DOCUMENT_RULES } from '../../utils/constants'
import { blankToNull, enumLabel, formatFileSize, toDateInput } from '../../utils/formatters'
import { optionalText, requiredText, validateFile } from '../../utils/validators'
import { ApiErrorAlert } from '../common/ApiErrorAlert'
import { FormActions } from '../common/FormActions'
import { FormField } from '../common/FormField'

// Mirrors the upload/update rules in EmployeeDocumentService.
const schema = z
  .object({
    documentType: z.enum(DOCUMENT_TYPES, { message: 'Select a document type.' }),
    documentName: requiredText('Document name', 200),
    issueDate: z.string(),
    expiryDate: z.string(),
    description: optionalText('Description', 500),
  })
  .refine((values) => !values.issueDate || !values.expiryDate || values.expiryDate >= values.issueDate, {
    path: ['expiryDate'],
    message: 'The expiry date cannot be earlier than the issue date.',
  })

type FormValues = z.infer<typeof schema>

export interface DocumentFormResult extends DocumentMetadata {
  /** Only when uploading; the backend can't replace the file of an existing document. */
  file: File | null
}

interface DocumentFormProps {
  /** Absent when uploading a new document. */
  initial?: EmployeeDocument
  onSubmit: (data: DocumentFormResult, onProgress: UploadProgressHandler) => Promise<void>
  onCancel: () => void
}

/** "national-id_scan.pdf" → "national id scan": a sensible default title. */
function titleFromFileName(name: string): string {
  return name.replace(/\.[^.]+$/, '').replace(/[_-]+/g, ' ').trim().slice(0, 200)
}

export function DocumentForm({ initial, onSubmit, onCancel }: DocumentFormProps) {
  const [file, setFile] = useState<File | null>(null)
  const [fileError, setFileError] = useState<string | null>(null)
  const [submitError, setSubmitError] = useState<unknown>(null)
  const [progress, setProgress] = useState<number | null>(null)
  const {
    register,
    handleSubmit,
    getValues,
    setValue,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      documentType: initial?.documentType ?? 'Nid',
      documentName: initial?.documentName ?? '',
      issueDate: toDateInput(initial?.issueDate),
      expiryDate: toDateInput(initial?.expiryDate),
      description: initial?.description ?? '',
    },
  })

  const selectFile = (event: ChangeEvent<HTMLInputElement>) => {
    const selected = event.target.files?.[0] ?? null
    const problem = selected ? validateFile(selected, DOCUMENT_RULES) : null
    setFileError(problem)
    setFile(problem ? null : selected)
    if (problem) {
      event.target.value = ''
      return
    }
    if (selected && !getValues('documentName').trim()) {
      setValue('documentName', titleFromFileName(selected.name), { shouldValidate: true })
    }
  }

  // The file isn't a form-library field, so check it on every submit, also when other fields are invalid.
  const requireFile = () => {
    if (initial || file) return true
    setFileError('Choose a file to upload.')
    return false
  }

  const submit = handleSubmit(async (values) => {
    if (!requireFile()) return
    setSubmitError(null)
    setProgress(file ? 0 : null)
    try {
      await onSubmit(
        {
          documentType: values.documentType,
          documentName: values.documentName,
          issueDate: values.issueDate || null,
          expiryDate: values.expiryDate || null,
          description: blankToNull(values.description),
          file,
        },
        setProgress,
      )
    } catch (error) {
      setSubmitError(error)
      setProgress(null)
    }
  }, requireFile)

  return (
    <form onSubmit={submit} noValidate className="grid gap-4 sm:grid-cols-2">
      <div className="sm:col-span-2">
        <ApiErrorAlert error={submitError} />
      </div>

      {!initial && (
        <FormField
          label="File"
          htmlFor="file"
          error={fileError ?? undefined}
          hint={file ? `Selected: ${file.name} · ${formatFileSize(file.size)}` : `${DOCUMENT_RULES.extensions.join(', ')} · up to 10 MB`}
          className="sm:col-span-2"
          required
        >
          <input
            id="file"
            type="file"
            className="file-input w-full"
            accept={DOCUMENT_RULES.extensions.join(',')}
            onChange={selectFile}
            disabled={isSubmitting}
          />
        </FormField>
      )}

      <FormField label="Document type" htmlFor="documentType" error={errors.documentType?.message} required>
        <select id="documentType" className="select w-full" {...register('documentType')}>
          {DOCUMENT_TYPES.map((type) => (
            <option key={type} value={type}>
              {enumLabel(type)}
            </option>
          ))}
        </select>
      </FormField>
      <FormField label="Document name" htmlFor="documentName" error={errors.documentName?.message} required>
        <input id="documentName" className="input w-full" placeholder="e.g. National ID card" {...register('documentName')} />
      </FormField>
      <FormField label="Issue date" htmlFor="issueDate" error={errors.issueDate?.message}>
        <input id="issueDate" type="date" className="input w-full" {...register('issueDate')} />
      </FormField>
      <FormField label="Expiry date" htmlFor="expiryDate" error={errors.expiryDate?.message} hint="Leave empty if it doesn't expire.">
        <input id="expiryDate" type="date" className="input w-full" {...register('expiryDate')} />
      </FormField>
      <FormField label="Description" htmlFor="description" error={errors.description?.message} hint="Optional. Up to 500 characters." className="sm:col-span-2">
        <textarea id="description" rows={3} className="textarea w-full" {...register('description')} />
      </FormField>

      {initial && <p className="text-xs text-base-content/60 sm:col-span-2">The file itself ({initial.fileName}) is not changed. Upload a new document to replace it.</p>}

      {isSubmitting && progress !== null && (
        <div className="sm:col-span-2">
          <div className="mb-1 flex justify-between text-xs text-base-content/70">
            <span>{progress < 100 ? 'Uploading…' : 'Processing…'}</span>
            <span>{progress}%</span>
          </div>
          <progress className="progress progress-primary w-full" value={progress} max={100} aria-label="Upload progress" />
        </div>
      )}

      <div className="sm:col-span-2">
        <FormActions submitting={isSubmitting} onCancel={onCancel} submitLabel={initial ? 'Save' : 'Upload'} />
      </div>
    </form>
  )
}
