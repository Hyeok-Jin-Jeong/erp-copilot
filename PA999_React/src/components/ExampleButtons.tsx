import { useRef } from 'react'

const EXAMPLES = [
  '단양공장 2026년 3월 생산량 알려줘',
  '레미콘 공장 전체 리스트 알려줘',
  '이번달 반복제조 공장별 생산량 비교해줘',
  '영월공장 작업장별 생산실적 보여줘',
  '생산량 TOP 3 공장은?',
]

interface Props {
  onSelect: (q: string) => void
  disabled?: boolean
}

export function ExampleButtons({ onSelect, disabled }: Props) {
  const scrollRef   = useRef<HTMLDivElement>(null)
  const isDragging  = useRef(false)
  const startX      = useRef(0)
  const scrollLeft  = useRef(0)

  const onMouseDown = (e: React.MouseEvent) => {
    isDragging.current = true
    startX.current     = e.pageX - (scrollRef.current?.offsetLeft ?? 0)
    scrollLeft.current = scrollRef.current?.scrollLeft ?? 0
    if (scrollRef.current) scrollRef.current.style.cursor = 'grabbing'
  }

  const onMouseMove = (e: React.MouseEvent) => {
    if (!isDragging.current || !scrollRef.current) return
    e.preventDefault()
    const x    = e.pageX - (scrollRef.current.offsetLeft ?? 0)
    const walk = (x - startX.current) * 1.2
    scrollRef.current.scrollLeft = scrollLeft.current - walk
  }

  const stopDrag = () => {
    isDragging.current = false
    if (scrollRef.current) scrollRef.current.style.cursor = 'grab'
  }

  return (
    <div className="px-4 pb-2">
      <p className="mb-2 text-xs font-medium text-gray-400 tracking-wide">예시 질문</p>
      <div
        ref={scrollRef}
        className="flex flex-nowrap gap-2 overflow-x-auto pb-1 scrollbar-hide select-none"
        style={{ cursor: 'grab' }}
        onMouseDown={onMouseDown}
        onMouseMove={onMouseMove}
        onMouseUp={stopDrag}
        onMouseLeave={stopDrag}
      >
        {EXAMPLES.map((q) => (
          <button
            key={q}
            onClick={() => onSelect(q)}
            disabled={disabled}
            className="flex-shrink-0 rounded-full border border-gray-200 bg-white px-3.5 py-1.5 text-xs text-gray-600
              hover:border-blue-400 hover:text-blue-600 hover:bg-blue-50
              disabled:opacity-40 disabled:cursor-not-allowed
              transition-colors shadow-sm whitespace-nowrap"
          >
            {q}
          </button>
        ))}
      </div>
    </div>
  )
}
