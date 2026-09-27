import { zodResolver } from '@hookform/resolvers/zod'
import { useState, type ChangeEvent } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { DOCUMENT_TYPES, type DocumentType, type EmployeeDocument } from '../../types/document'
import { DOCUMENT_RULES } from '../../utils/constants'
import { blankToNull, enumLabel, formatFileSize } from '../../utils/formatters'
import { optionalText, validateFile } from '../../utils/validators'
import { ApiErrorAlert } from '../common/ApiErrorAlert'
import { FormActions } from '../common/FormActions'
import { FormField } from '../common/FormField'

const schema = z.object({
  documentType: z.enum(DOCUMENT_TYPES, { message: 'Select a document type.' }),
  description: optionalText('Description', 500),
})

type FormValues = z.infer<typeof schema>

export interface DocumentFormResult {
  documentType: DocumentType
  description: string | null
  /** Only when uploading; the backend can't replace the file of an existing document. */
  file: File | null
}

interface DocumentFormProps {
  /** Absent when uploading a new document. */
  initial?: EmployeeDocument
  onSubmit: (data: DocumentFormResult) => Promise<void>
  onCancel: () => void
}

export function DocumentForm({ initial, onSubmit, onCancel }: DocumentFormProps) {
  const [file, setFile] = useState<File | null>(null)
  const [fileError, setFileError] = useState<string | null>(null)
  const [submitError, setSubmitError] = useState<unknown>(null)
  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { documentType: initial?.documentType ?? 'Nid', description: initial?.description ?? '' },
  })

  const selectFile = (event: ChangeEvent<HTMLInputElement>) => {
    const selected = event.target.files?.[0] ?? null
    const problem = selected ? validateFile(selected, DOCUMENT_RULES) : null
    setFileError(problem)
    setFile(problem ? null : selected)
    if (problem) event.target.value = ''
  }

  const submit = handleSubmit(async (values) => {
    if (!initial && !file) {
      setFileError('Choose a file to upload.')
      return
    }
    setSubmitError(null)
    try {
      await onSubmit({ documentType: values.documentType, description: blankToNull(values.description), file })
    } catch (error) {
      setSubmitError(error)
    }
  })

  return (
    <form onSubmit={submit} noValidate className="grid gap-4">
      <ApiErrorAlert error={submitError} />
      {!initial && (
        <FormField
          label="File"
          htmlFor="file"
          error={fileError ?? undefined}
          hint={file ? `${file.name} · ${formatFileSize(file.size)}` : `${DOCUMENT_RULES.extensions.join(', ')} · up to 10 MB`}
          required
        >
          <input id="file" type="file" className="file-input w-full" accept={DOCUMENT_RULES.extensions.join(',')} onChange={selectFile} />
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
      <FormField label="Description" htmlFor="description" error={errors.description?.message} hint="Optional. Up to 500 characters.">
        <textarea id="description" rows={3} className="textarea w-full" {...register('description')} />
      </FormField>
      <FormActions submitting={isSubmitting} onCancel={onCancel} submitLabel={initial ? 'Save' : 'Upload'} />
    </form>
  )
}
