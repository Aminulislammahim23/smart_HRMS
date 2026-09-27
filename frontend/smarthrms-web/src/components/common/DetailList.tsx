import type { ReactNode } from 'react'

export interface DetailItem {
  label: string
  value: ReactNode
}

/** Label/value pairs in a responsive grid. Empty values show a dash. */
export function DetailList({ items, columns = 2 }: { items: DetailItem[]; columns?: 1 | 2 | 3 }) {
  const grid = columns === 1 ? '' : columns === 2 ? 'sm:grid-cols-2' : 'sm:grid-cols-2 lg:grid-cols-3'

  return (
    <dl className={`grid gap-x-6 gap-y-4 ${grid}`}>
      {items.map(({ label, value }) => (
        <div key={label} className="min-w-0">
          <dt className="text-xs font-medium uppercase tracking-wide text-base-content/50">{label}</dt>
          <dd className="mt-0.5 break-words text-sm">{value === null || value === undefined || value === '' ? '—' : value}</dd>
        </div>
      ))}
    </dl>
  )
}
