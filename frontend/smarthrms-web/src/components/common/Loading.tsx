export function Loading({ label = 'Loading…' }: { label?: string }) {
  return (
    <div className="flex flex-col items-center justify-center gap-3 py-16 text-base-content/60" role="status">
      <span className="loading loading-spinner loading-lg text-primary" />
      <span className="text-sm">{label}</span>
    </div>
  )
}
