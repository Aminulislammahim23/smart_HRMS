import { useCallback, useEffect, useMemo } from 'react'
import { useApi } from '../../hooks/useApi'
import { fetchDocumentFile } from '../../services/documentService'
import type { EmployeeDocument } from '../../types/document'
import { ErrorState } from '../common/ErrorState'
import { Loading } from '../common/Loading'

/**
 * Renders a PDF (browser viewer) or image inline. The file is fetched through the API download endpoint; there is no
 * direct URL to the private storage. Only use for documents where canPreview() is true.
 */
export function DocumentPreview({ document, height = 'h-[70vh]' }: { document: EmployeeDocument; height?: string }) {
  const load = useCallback(() => fetchDocumentFile(document), [document])
  const { data: blob, error, loading, reload } = useApi(load)

  // Object URLs pin the blob in memory, so release each one when it is replaced or the view closes.
  const url = useMemo(() => (blob ? URL.createObjectURL(blob) : null), [blob])
  useEffect(() => () => {
    if (url) URL.revokeObjectURL(url)
  }, [url])

  if (error) return <ErrorState error={error} onRetry={reload} />
  if (loading || !url) return <Loading label="Loading document…" />

  return document.contentType === 'application/pdf' ? (
    <iframe src={url} title={document.documentName} className={`${height} w-full rounded-box border border-base-300 bg-white`} />
  ) : (
    <div className={`flex max-h-[70vh] justify-center overflow-auto rounded-box border border-base-300 bg-base-200`}>
      <img src={url} alt={document.documentName} className="max-w-full object-contain" />
    </div>
  )
}
