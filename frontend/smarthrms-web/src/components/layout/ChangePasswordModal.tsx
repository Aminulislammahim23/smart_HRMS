import { zodResolver } from '@hookform/resolvers/zod'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { useAuth } from '../../hooks/useAuth'
import { useToast } from '../../hooks/useToast'
import { changePassword } from '../../services/authService'
import { ApiErrorAlert } from '../common/ApiErrorAlert'
import { FormField } from '../common/FormField'
import { Modal } from '../common/Modal'
import { passwordSchema } from './passwordSchema'

const schema = z
  .object({
    currentPassword: z.string().min(1, 'Enter your current password.'),
    newPassword: passwordSchema,
    confirmPassword: z.string(),
  })
  .refine((v) => v.newPassword === v.confirmPassword, { message: 'The passwords do not match.', path: ['confirmPassword'] })
  .refine((v) => v.newPassword !== v.currentPassword, { message: 'Choose a password different from the current one.', path: ['newPassword'] })
type Form = z.infer<typeof schema>

/** Changes the signed-in user's password. The server ends every session, so the user signs in again. */
export function ChangePasswordModal({ open, onClose }: { open: boolean; onClose: () => void }) {
  const { logout } = useAuth()
  const { notify } = useToast()
  const [error, setError] = useState<unknown>(null)
  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<Form>({ resolver: zodResolver(schema), defaultValues: { currentPassword: '', newPassword: '', confirmPassword: '' } })

  const submit = handleSubmit(async (values) => {
    setError(null)
    try {
      await changePassword({ currentPassword: values.currentPassword, newPassword: values.newPassword })
      notify('success', 'Password changed. Please sign in with your new password.')
      logout('Your password was changed. Sign in with the new password.')
    } catch (caught) {
      setError(caught)
    }
  })

  return (
    <Modal open={open} title="Change password" onClose={onClose} busy={isSubmitting}>
      <form className="flex flex-col gap-3" onSubmit={submit} noValidate>
        {error !== null && <ApiErrorAlert error={error} />}
        <FormField label="Current password" htmlFor="cp-current" error={errors.currentPassword?.message} required>
          <input id="cp-current" type="password" className="input w-full" autoComplete="current-password" {...register('currentPassword')} />
        </FormField>
        <FormField label="New password" htmlFor="cp-new" error={errors.newPassword?.message} hint="At least 8 characters, with a letter and a digit." required>
          <input id="cp-new" type="password" className="input w-full" autoComplete="new-password" {...register('newPassword')} />
        </FormField>
        <FormField label="Confirm new password" htmlFor="cp-confirm" error={errors.confirmPassword?.message} required>
          <input id="cp-confirm" type="password" className="input w-full" autoComplete="new-password" {...register('confirmPassword')} />
        </FormField>
        <div className="modal-action">
          <button type="button" className="btn btn-ghost" onClick={onClose} disabled={isSubmitting}>
            Cancel
          </button>
          <button type="submit" className="btn btn-primary" disabled={isSubmitting}>
            {isSubmitting && <span className="loading loading-spinner loading-sm" />} Change password
          </button>
        </div>
      </form>
    </Modal>
  )
}
