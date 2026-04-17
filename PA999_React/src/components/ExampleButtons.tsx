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
  return (
    <div className="px-4 pb-2">
      <p className="mb-2 text-xs font-medium text-gray-400 tracking-wide">예시 질문</p>
      <div className="flex gap-2 overflow-x-auto pb-1 scrollbar-hide">
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
