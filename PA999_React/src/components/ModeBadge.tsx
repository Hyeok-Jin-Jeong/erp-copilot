// SP / SQL / SOP 모드 뱃지
const palette: Record<string, string> = {
  SP:  'bg-violet-100 text-violet-700 ring-violet-200',
  SQL: 'bg-blue-100   text-blue-700   ring-blue-200',
  SOP: 'bg-amber-100  text-amber-700  ring-amber-200',
}

export function ModeBadge({ mode }: { mode?: string | null }) {
  if (!mode) return null
  const cls = palette[mode] ?? 'bg-gray-100 text-gray-600 ring-gray-200'
  return (
    <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-semibold ring-1 ring-inset ${cls}`}>
      {mode}
    </span>
  )
}
