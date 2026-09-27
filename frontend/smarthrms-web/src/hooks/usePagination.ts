import { useState } from 'react'
import { PAGE_SIZE_OPTIONS } from '../utils/constants'

/**
 * Client-side paging. Used only because the backend list endpoints return every row and have no paging
 * parameters; switch to server-side paging if the API adds it.
 */
export function usePagination<T>(items: readonly T[], initialPageSize: number = PAGE_SIZE_OPTIONS[0]) {
  const [requestedPage, setPage] = useState(1)
  const [pageSize, setPageSizeState] = useState(initialPageSize)

  const total = items.length
  const pageCount = Math.max(1, Math.ceil(total / pageSize))
  // Clamp instead of resetting in an effect, so shrinking the list (filters, deletes) never shows an empty page.
  const page = Math.min(Math.max(1, requestedPage), pageCount)
  const pageItems = items.slice((page - 1) * pageSize, page * pageSize)

  const setPageSize = (size: number) => {
    setPageSizeState(size)
    setPage(1)
  }

  return { page, setPage, pageSize, setPageSize, pageCount, pageItems, total }
}
