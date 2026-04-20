import { useState } from 'react'

interface Props {
  logSeq:       number
  initialScore: number   // 1 = 👎 진입, 5 = 👍 진입 (직접 제출)
  onClose:      () => void
  onSubmit:     (score: number, text: string) => Promise<void>
}

const STAR_LABELS: Record<number, string> = {
  1: '매우 나빠요',
  2: '나빠요',
  3: '보통이에요',
  4: '좋아요',
  5: '매우 좋아요',
}

export function FeedbackModal({ logSeq, initialScore, onClose, onSubmit }: Props) {
  const [score,      setScore]      = useState(initialScore)
  const [text,       setText]       = useState('')
  const [submitting, setSubmitting] = useState(false)
  const [hovered,    setHovered]    = useState<number | null>(null)

  const displayScore = hovered ?? score

  const handleSubmit = async () => {
    setSubmitting(true)
    try {
      await onSubmit(score, text)
      onClose()
    } catch {
      // 오류 시 모달 유지 (버튼 재활성화)
    } finally {
      setSubmitting(false)
    }
  }

  return (
    /* 백드롭 */
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4"
      onClick={e => e.target === e.currentTarget && onClose()}
    >
      <div className="w-full max-w-sm rounded-2xl bg-white shadow-2xl">
        {/* 헤더 */}
        <div className="flex items-center justify-between border-b border-gray-100 px-5 py-4">
          <div>
            <h3 className="text-sm font-semibold text-gray-900">AI 답변 피드백</h3>
            <p className="text-[11px] text-gray-400 mt-0.5">#{logSeq}</p>
          </div>
          <button
            onClick={onClose}
            className="rounded-lg p-1 text-gray-400 hover:bg-gray-100 transition-colors"
          >
            <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>

        <div className="px-5 py-4 space-y-4">
          {/* 별점 선택 */}
          <div>
            <p className="mb-2 text-xs font-medium text-gray-600">답변 품질 평가</p>
            <div className="flex items-center gap-1">
              {[1, 2, 3, 4, 5].map(s => (
                <button
                  key={s}
                  onClick={() => setScore(s)}
                  onMouseEnter={() => setHovered(s)}
                  onMouseLeave={() => setHovered(null)}
                  className="transition-transform hover:scale-110 active:scale-95"
                  aria-label={`${s}점`}
                >
                  <svg
                    className={`h-7 w-7 transition-colors ${
                      s <= displayScore
                        ? 'text-amber-400'
                        : 'text-gray-200'
                    }`}
                    fill="currentColor"
                    viewBox="0 0 24 24"
                  >
                    <path d="M10.788 3.21c.448-1.077 1.976-1.077 2.424 0l2.082 5.007 5.404.433c1.164.093 1.636 1.545.749 2.305l-4.117 3.527 1.257 5.273c.271 1.136-.964 2.033-1.96 1.425L12 18.354 7.373 21.18c-.996.608-2.231-.29-1.96-1.425l1.257-5.273-4.117-3.527c-.887-.76-.415-2.212.749-2.305l5.404-.433 2.082-5.006z" />
                  </svg>
                </button>
              ))}
              <span className="ml-2 text-xs text-gray-500 min-w-[80px]">
                {STAR_LABELS[displayScore]}
              </span>
            </div>
          </div>

          {/* 피드백 텍스트 */}
          <div>
            <label className="mb-1.5 block text-xs font-medium text-gray-600">
              어떤 점이 {score <= 2 ? '부족했나요?' : '좋으셨나요?'}
              <span className="ml-1 font-normal text-gray-400">(선택)</span>
            </label>
            <textarea
              value={text}
              onChange={e => setText(e.target.value)}
              placeholder={
                score <= 2
                  ? 'SQL이 잘못되었거나, 데이터가 틀린 경우 알려주세요.'
                  : '잘 된 점을 알려주시면 개선에 반영합니다.'
              }
              rows={3}
              maxLength={500}
              className="w-full resize-none rounded-xl border border-gray-200 px-3 py-2.5 text-sm
                placeholder-gray-300 text-gray-700 outline-none
                focus:border-blue-400 focus:ring-2 focus:ring-blue-100 transition"
            />
            <p className="mt-1 text-right text-[10px] text-gray-300">{text.length}/500</p>
          </div>

          {/* 점수 ≤ 2: 패턴 자동 등록 안내 */}
          {score <= 2 && (
            <div className="rounded-xl bg-amber-50 border border-amber-100 px-3 py-2.5">
              <p className="text-[11px] text-amber-700">
                💡 낮은 점수 피드백은 AI 학습 패턴으로 자동 등록되어
                동일 유형 오류를 방지합니다.
              </p>
            </div>
          )}
        </div>

        {/* 버튼 */}
        <div className="flex gap-2 border-t border-gray-100 px-5 py-3">
          <button
            onClick={onClose}
            className="flex-1 rounded-xl border border-gray-200 py-2 text-sm text-gray-500
              hover:bg-gray-50 transition-colors"
          >
            취소
          </button>
          <button
            onClick={handleSubmit}
            disabled={submitting}
            className="flex-1 rounded-xl bg-blue-600 py-2 text-sm font-medium text-white
              hover:bg-blue-700 disabled:opacity-50 transition-colors"
          >
            {submitting ? '전송 중…' : '피드백 제출'}
          </button>
        </div>
      </div>
    </div>
  )
}
