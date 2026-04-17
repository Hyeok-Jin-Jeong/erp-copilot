import { useState } from 'react'

interface Props { sql: string }

export function SqlCollapsible({ sql }: Props) {
  const [open, setOpen] = useState(false)

  return (
    <div className="mt-3">
      <button
        onClick={() => setOpen(p => !p)}
        className="flex items-center gap-1.5 text-xs font-medium text-gray-500 hover:text-blue-600 transition-colors"
      >
        <svg
          className={`h-3.5 w-3.5 transition-transform ${open ? 'rotate-90' : ''}`}
          fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2.5}
        >
          <path strokeLinecap="round" strokeLinejoin="round" d="M9 5l7 7-7 7" />
        </svg>
        {open ? 'SQL 접기' : '생성된 SQL 보기'}
      </button>

      {open && (
        <div className="relative mt-2 rounded-lg bg-gray-900 text-gray-100 overflow-x-auto">
          {/* 복사 버튼 */}
          <CopyButton text={sql} />
          <pre className="p-4 pr-12 text-xs leading-relaxed font-mono whitespace-pre">
            {sql}
          </pre>
        </div>
      )}
    </div>
  )
}

function CopyButton({ text }: { text: string }) {
  const [copied, setCopied] = useState(false)

  const copy = async () => {
    await navigator.clipboard.writeText(text)
    setCopied(true)
    setTimeout(() => setCopied(false), 1500)
  }

  return (
    <button
      onClick={copy}
      className="absolute right-2 top-2 rounded px-2 py-1 text-xs text-gray-400 hover:text-white hover:bg-gray-700 transition-colors"
    >
      {copied ? '✓ 복사됨' : '복사'}
    </button>
  )
}
