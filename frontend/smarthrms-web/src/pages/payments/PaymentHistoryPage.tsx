import { PageHeader } from '../../components/common/PageHeader'
import { PaymentList } from '../../components/payments/PaymentList'

/** Every salary payment across batches (HR/Admin), filtered and paged on the server. */
export default function PaymentHistoryPage() {
  return (
    <>
      <PageHeader title="Payment history" description="Salary payments by employee, payroll period, date, status and method." />
      <PaymentList />
    </>
  )
}
