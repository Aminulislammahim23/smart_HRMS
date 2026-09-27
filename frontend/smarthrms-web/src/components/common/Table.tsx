import { ArrowDown, ArrowUp, ArrowUpDown } from 'lucide-react'
import type { ReactNode } from 'react'

export interface Column<T> {
  key: string
  header: ReactNode
  render: (row: T) => ReactNode
  sortable?: boolean
  className?: string
}

export interface SortState {
  key: string
  direction: 'asc' | 'desc'
}

interface TableProps<T> {
  columns: Column<T>[]
  rows: readonly T[]
  rowKey: (row: T) => string
  sort?: SortState
  onSortChange?: (sort: SortState) => void
}

/** Responsive table: scrolls horizontally on narrow screens instead of breaking the layout. */
export function Table<T>({ columns, rows, rowKey, sort, onSortChange }: TableProps<T>) {
  const toggleSort = (key: string) => {
    if (!onSortChange) return
    onSortChange({ key, direction: sort?.key === key && sort.direction === 'asc' ? 'desc' : 'asc' })
  }

  return (
    <div className="overflow-x-auto">
      <table className="table">
        <thead>
          <tr>
            {columns.map((column) => {
              const active = sort?.key === column.key
              const SortIcon = !active ? ArrowUpDown : sort.direction === 'asc' ? ArrowUp : ArrowDown
              return (
                <th key={column.key} className={column.className} aria-sort={active ? (sort.direction === 'asc' ? 'ascending' : 'descending') : undefined}>
                  {column.sortable && onSortChange ? (
                    <button type="button" className="inline-flex items-center gap-1 hover:text-base-content" onClick={() => toggleSort(column.key)}>
                      {column.header}
                      <SortIcon className={`size-3.5 ${active ? '' : 'opacity-40'}`} />
                    </button>
                  ) : (
                    column.header
                  )}
                </th>
              )
            })}
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => (
            <tr key={rowKey(row)} className="hover:bg-base-200/50">
              {columns.map((column) => (
                <td key={column.key} className={column.className}>
                  {column.render(row)}
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}
