import { useNavigate } from 'react-router-dom'
import { PageHeader } from '../../components/common/PageHeader'
import { PayrollPeriodForm } from '../../components/payroll/PayrollPeriodForm'
import { useToast } from '../../hooks/useToast'
import { createPayrollPeriod } from '../../services/payrollService'

export default function PayrollCreatePage() {
  const navigate = useNavigate()
  const { notify } = useToast()

  return (
    <>
      <PageHeader title="New payroll period" description="Usually one calendar month. Periods can't overlap." />
      <div className="card bg-base-100 shadow-sm">
        <div className="card-body">
          <PayrollPeriodForm
            submitLabel="Create period"
            onCancel={() => navigate('/payroll/periods')}
            onSubmit={async (request) => {
              const period = await createPayrollPeriod(request)
              notify('success', `${period.name} created. Calculate it when salaries and attendance are ready.`)
              navigate(`/payroll/${period.id}`)
            }}
          />
        </div>
      </div>
    </>
  )
}
