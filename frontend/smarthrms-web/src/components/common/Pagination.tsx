import { ChevronLeft, ChevronRight } from 'lucide-react'
import { PAGE_SIZE_OPTIONS } from '../../utils/constants'

interface PaginationProps {
  page: number
  pageCount: number
  pageSize: number
  total: number
  onPageChange: (page: number) => void
  onPageSizeChange: (size: number) => void
}

export function Pagination({ page, pageCount, pageSize, total, onPageChange, onPageSizeChange }: PaginationProps) {
  const from = total === 0 ? 0 : (page - 1) * pageSize + 1
  const to = Math.min(page * pageSize, total)

  return (
    <div className="flex flex-col items-center justify-between gap-3 text-sm sm:flex-row">
      <div className="flex items-center gap-2 text-base-content/70">
        <span>
          Showing {from}–{to} of {total}
        </span>
        <select
          className="select select-sm w-auto"
          value={pageSize}
          onChange={(event) => onPageSizeChange(Number(event.target.value))}
          aria-label="Rows per page"
        >
          {PAGE_SIZE_OPTIONS.map((size) => (
            <option key={size} value={size}>
              {size} / page
            </option>
          ))}
        </select>
      </div>
      <div className="join">
        <button type="button" className="btn btn-sm join-item" disabled={page <= 1} onClick={() => onPageChange(page - 1)} aria-label="Previous page">
          <ChevronLeft className="size-4" />
        </button>
        <span className="btn btn-sm join-item pointer-events-none">
          Page {page} of {pageCount}
        </span>
        <button type="button" className="btn btn-sm join-item" disabled={page >= pageCount} onClick={() => onPageChange(page + 1)} aria-label="Next page">
          <ChevronRight className="size-4" />
        </button>
      </div>
    </div>
  )
}
