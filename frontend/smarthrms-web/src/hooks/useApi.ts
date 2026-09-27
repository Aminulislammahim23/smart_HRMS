import { useCallback, useEffect, useState } from 'react'
import { isCancelled } from '../services/api'

type Fetcher<T> = (signal: AbortSignal) => Promise<T>

interface Result<T> {
  fetcher: Fetcher<T>
  version: number
  data?: T
  error?: unknown
}

export interface ApiState<T> {
  data: T | undefined
  error: unknown
  loading: boolean
  /** Fetches again, keeping the current data on screen until the new data arrives. */
  reload: () => void
}

/**
 * Loads data with a GET service call. Pass a stable fetcher (useCallback) so it only reruns when its inputs change;
 * the previous request is aborted when they do or when the component unmounts.
 */
export function useApi<T>(fetcher: Fetcher<T>): ApiState<T> {
  const [version, setVersion] = useState(0)
  const [result, setResult] = useState<Result<T> | null>(null)

  useEffect(() => {
    const controller = new AbortController()
    fetcher(controller.signal).then(
      (data) => setResult({ fetcher, version, data }),
      (error: unknown) => {
        if (!isCancelled(error)) setResult({ fetcher, version, error })
      },
    )
    return () => controller.abort()
  }, [fetcher, version])

  const reload = useCallback(() => setVersion((current) => current + 1), [])
  const isSameSource = result?.fetcher === fetcher
  const isCurrent = isSameSource && result.version === version

  return {
    data: isSameSource ? result.data : undefined,
    error: isCurrent ? result.error : undefined,
    loading: !isCurrent,
    reload,
  }
}
