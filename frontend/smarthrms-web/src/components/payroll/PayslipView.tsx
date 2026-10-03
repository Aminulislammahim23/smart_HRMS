import type { Payslip } from '../../types/payroll'
import { enumLabel, formatAmount, formatDate, formatDateTime } from '../../utils/formatters'
import { AmountBreakdown } from './AmountBreakdown'

/** A printable payslip. Styled to read well on screen and on A4 paper (see the print rules in index.css). */
export function PayslipView({ payslip }: { payslip: Payslip }) {
  const days = [
    { label: 'Working days', value: payslip.workingDays },
    { label: 'Present', value: payslip.presentDays },
    { label: 'Paid leave', value: payslip.paidLeaveDays },
    { label: 'Unpaid leave', value: payslip.unpaidLeaveDays },
    { label: 'Absent', value: payslip.absentDays },
  ]

  return (
    <article className="payslip card bg-base-100 shadow-sm print:shadow-none" aria-label={`Payslip ${payslip.periodName}`}>
      <div className="card-body gap-6">
        <header className="flex flex-col gap-4 border-b border-base-300 pb-4 sm:flex-row sm:items-start sm:justify-between">
          <div className="flex items-center gap-3">
            <div className="grid size-12 place-items-center rounded-xl bg-primary text-xl font-bold text-primary-content">{payslip.companyName.charAt(0)}</div>
            <div>
              <h1 className="text-xl font-semibold">{payslip.companyName}</h1>
              {payslip.companyAddress && <p className="text-sm text-base-content/60">{payslip.companyAddress}</p>}
            </div>
          </div>
          <div className="sm:text-right">
            <p className="text-xs font-semibold uppercase tracking-wider text-base-content/50">Payslip</p>
            <p className="text-lg font-semibold">{payslip.periodName}</p>
            <p className="text-sm text-base-content/60">
              {formatDate(payslip.periodStartDate)} – {formatDate(payslip.periodEndDate)}
            </p>
          </div>
        </header>

        {!payslip.isFinal && (
          <div role="note" className="alert alert-warning alert-soft print:border print:border-base-300">
            <span className="text-sm font-medium">Preview — this payroll is {enumLabel(payslip.paymentStatus).toLowerCase()} and not yet approved.</span>
          </div>
        )}

        <section className="grid gap-x-6 gap-y-3 text-sm sm:grid-cols-2">
          {[
            ['Employee', payslip.employeeName],
            ['Employee ID', payslip.employeeCode],
            ['Department', payslip.departmentName ?? '—'],
            ['Designation', payslip.designationName ?? '—'],
          ].map(([label, value]) => (
            <div key={label} className="flex justify-between gap-4 border-b border-base-200 pb-1">
              <span className="text-base-content/60">{label}</span>
              <span className="text-right font-medium">{value}</span>
            </div>
          ))}
        </section>

        <section className="grid gap-4 md:grid-cols-2 print:grid-cols-2">
          <AmountBreakdown title="Earnings" lines={payslip.earnings} totalLabel="Gross salary" total={payslip.grossSalary} />
          <AmountBreakdown title="Deductions" lines={payslip.deductions.length ? payslip.deductions : [{ label: 'No deductions', amount: 0 }]} totalLabel="Total deduction" total={payslip.totalDeduction} tone="deduction" />
        </section>

        <section className="flex flex-wrap items-center justify-between gap-4 rounded-box bg-primary/10 px-5 py-4 print:border print:border-base-300">
          <div>
            <p className="text-sm font-semibold">Net salary</p>
            <p className="text-xs text-base-content/60">Gross salary − total deduction</p>
          </div>
          <p className="ml-auto text-right text-2xl font-bold tabular-nums text-primary">
            {payslip.currency} {formatAmount(payslip.netSalary)}
          </p>
        </section>

        <section>
          <h2 className="mb-2 text-sm font-semibold">Attendance</h2>
          <div className="grid grid-cols-5 gap-2 text-center">
            {days.map(({ label, value }) => (
              <div key={label} className="rounded-box bg-base-200/60 px-2 py-2">
                <p className="font-semibold tabular-nums">{value}</p>
                <p className="text-xs text-base-content/60">{label}</p>
              </div>
            ))}
          </div>
        </section>

        <footer className="grid gap-2 border-t border-base-300 pt-4 text-sm sm:grid-cols-3">
          <p>
            <span className="text-base-content/60">Payment status: </span>
            <span className="font-medium">{enumLabel(payslip.paymentStatus)}</span>
          </p>
          <p>
            <span className="text-base-content/60">Approved by: </span>
            <span className="font-medium">{payslip.approvedBy ?? '—'}</span>
            {payslip.approvedAt && <span className="text-base-content/60"> · {formatDateTime(payslip.approvedAt)}</span>}
          </p>
          <p>
            <span className="text-base-content/60">Paid: </span>
            <span className="font-medium">{payslip.paidAt ? formatDateTime(payslip.paidAt) : 'Not yet'}</span>
          </p>
          {payslip.remarks && <p className="sm:col-span-3 text-base-content/70">Remarks: {payslip.remarks}</p>}
          <p className="sm:col-span-3 text-xs text-base-content/50">This payslip is generated by SmartHRMS and is valid without a signature.</p>
        </footer>
      </div>
    </article>
  )
}
