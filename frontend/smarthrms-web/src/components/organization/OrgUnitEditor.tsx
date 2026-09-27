import { useCallback } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { useApi } from '../../hooks/useApi'
import { useToast } from '../../hooks/useToast'
import { ErrorState } from '../common/ErrorState'
import { Loading } from '../common/Loading'
import { PageHeader } from '../common/PageHeader'
import { OrgUnitForm } from './OrgUnitForm'
import type { OrgUnitModule } from './orgUnits'

export function OrgUnitCreate({ module }: { module: OrgUnitModule }) {
  const navigate = useNavigate()
  const { notify } = useToast()

  return (
    <div className="mx-auto max-w-2xl">
      <PageHeader title={`New ${module.singular.toLowerCase()}`} description={`Add a ${module.singular.toLowerCase()} to the organization.`} />
      <OrgUnitForm
        module={module}
        onSubmit={async ({ name, description }) => {
          const created = await module.create({ name, description })
          notify('success', `${module.singular} "${created.name}" created.`)
          navigate(module.basePath)
        }}
      />
    </div>
  )
}

export function OrgUnitEdit({ module }: { module: OrgUnitModule }) {
  const { id = '' } = useParams()
  const navigate = useNavigate()
  const { notify } = useToast()
  const load = useCallback((signal: AbortSignal) => module.getById(id, signal), [module, id])
  const { data, error, loading, reload } = useApi(load)

  return (
    <div className="mx-auto max-w-2xl">
      <PageHeader title={`Edit ${module.singular.toLowerCase()}`} description={data?.name} />
      {error ? (
        <div className="card bg-base-100 shadow-sm">
          <ErrorState
            error={error}
            onRetry={reload}
            action={
              <Link to={module.basePath} className="btn btn-sm btn-ghost">
                Back to {module.plural.toLowerCase()}
              </Link>
            }
          />
        </div>
      ) : loading || !data ? (
        <Loading />
      ) : (
        <OrgUnitForm
          key={data.id}
          module={module}
          initial={data}
          onSubmit={async (values) => {
            const updated = await module.update(id, values)
            notify('success', `${module.singular} "${updated.name}" updated.`)
            navigate(module.basePath)
          }}
        />
      )}
    </div>
  )
}
