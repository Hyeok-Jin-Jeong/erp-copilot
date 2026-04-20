import { useState } from 'react'
import ReactMarkdown from 'react-markdown'
import remarkGfm from 'remark-gfm'
import type { Message } from '../types/api'
import { ModeBadge } from './ModeBadge'
import { GridTable } from './GridTable'
import { SqlCollapsible } from './SqlCollapsible'
import { FeedbackModal } from './FeedbackModal'
import { submitUserFeedback } from '../api/feedbackApi'

interface Props { message: Message }

export function ChatBubble({ message }: Props) {
  const isUser = message.role === 'user'

  // ── 피드백 상태 (AI 버블 전용) ─────────────────────────────────
  const [feedbackDone,  setFeedbackDone]  = useState(false)
  const [feedbackScore, setFeedbackScore] = useState<number | null>(null)
  const [modalOpen,     setModalOpen]     = useState(false)
  const [feedbackError, setFeedbackError] = useState(false)

  const hasLogSeq = !isUser && !message.isError && !!message.logSeq

  const handleGood = async () => {
    if (!message.logSeq) return
    try {
      await submitUserFeedback(message.logSeq, 5, '')
      setFeedbackScore(5)
      setFeedbackDone(true)
    } catch {
      setFeedbackError(true)
      setTimeout(() => setFeedbackError(false), 2000)
    }
  }

  const handleBad = () => {
    if (!message.logSeq) return
    setModalOpen(true)
  }

  const handleModalSubmit = async (score: number, text: string) => {
    if (!message.logSeq) return
    await submitUserFeedback(message.logSeq, score, text)
    setFeedbackScore(score)
    setFeedbackDone(true)
  }

  // ── 사용자 버블 ─────────────────────────────────────────────────
  if (isUser) {
    return (
      <div className="flex justify-end">
        <div className="max-w-[75%] rounded-2xl rounded-tr-sm bg-blue-600 px-4 py-2.5 text-sm text-white shadow-sm">
          {message.text}
        </div>
      </div>
    )
  }

  // ── AI 버블 ──────────────────────────────────────────────────────
  return (
    <>
      <div className="flex items-start gap-2.5">
        {/* 아바타 */}
        <div className="flex-shrink-0 w-8 h-8 rounded-full bg-gradient-to-br from-blue-500 to-violet-600 flex items-center justify-center shadow-sm">
          <svg className="w-4 h-4 text-white" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
            <path strokeLinecap="round" strokeLinejoin="round"
              d="M9.75 3.104v5.714a2.25 2.25 0 01-.659 1.591L5 14.5M9.75 3.104c-.251.023-.501.05-.75.082m.75-.082a24.301 24.301 0 014.5 0m0 0v5.714c0 .597.237 1.17.659 1.591L19.8 15.3M14.25 3.104c.251.023.501.05.75.082M19.8 15.3l-1.57.393A9.065 9.065 0 0112 15a9.065 9.065 0 00-6.23-.693L5 14.5m14.8.8l1.402 1.402c1 1 .03 2.703-1.352 2.703H4.15c-1.38 0-2.352-1.703-1.351-2.703L4.2 15.3" />
          </svg>
        </div>

        {/* 버블 본문 */}
        <div className="max-w-[80%] min-w-0">
          {/* 모드 배지 */}
          {message.processMode && (
            <div className="mb-1.5">
              <ModeBadge mode={message.processMode} />
            </div>
          )}

          {/* 텍스트 (마크다운) */}
          <div className={`rounded-2xl rounded-tl-sm px-4 py-3 text-sm shadow-sm ${
            message.isError
              ? 'bg-red-50 border border-red-200 text-red-800'
              : 'bg-white border border-gray-200 text-gray-800'
          }`}>
            <div className="prose prose-sm prose-gray max-w-none
              prose-headings:font-semibold prose-headings:text-gray-800
              prose-h2:text-base prose-h3:text-sm
              prose-strong:text-gray-900
              prose-code:text-blue-700 prose-code:bg-blue-50 prose-code:px-1 prose-code:rounded
              prose-p:leading-relaxed prose-p:my-1
              prose-ul:my-1 prose-li:my-0.5
            ">
              <ReactMarkdown remarkPlugins={[remarkGfm]}>
                {message.text}
              </ReactMarkdown>
            </div>

            {/* 그리드 테이블 */}
            {message.gridData && message.gridData.length > 0 && (
              <GridTable rows={message.gridData} />
            )}

            {/* SQL 접기/펼치기 */}
            {message.generatedSql && (
              <SqlCollapsible sql={message.generatedSql} />
            )}
          </div>

          {/* 하단: 타임스탬프 + 피드백 버튼 */}
          <div className="mt-1 flex items-center gap-3 px-1">
            <span className="text-[10px] text-gray-400">
              {message.timestamp.toLocaleTimeString('ko-KR', { hour: '2-digit', minute: '2-digit' })}
              {message.logSeq && (
                <span className="ml-1.5 opacity-60">#{message.logSeq}</span>
              )}
            </span>

            {/* 피드백 버튼 영역 */}
            {hasLogSeq && !feedbackDone && (
              <div className="flex items-center gap-1">
                <button
                  onClick={handleGood}
                  title="도움이 됐어요"
                  className="rounded-lg px-2 py-0.5 text-xs text-gray-400 transition-colors
                    hover:bg-green-50 hover:text-green-600 active:scale-95"
                >
                  👍
                </button>
                <button
                  onClick={handleBad}
                  title="개선이 필요해요"
                  className="rounded-lg px-2 py-0.5 text-xs text-gray-400 transition-colors
                    hover:bg-red-50 hover:text-red-500 active:scale-95"
                >
                  👎
                </button>
                {feedbackError && (
                  <span className="text-[10px] text-red-400">전송 실패</span>
                )}
              </div>
            )}

            {/* 피드백 완료 표시 */}
            {hasLogSeq && feedbackDone && (
              <span className="text-[10px] text-gray-400">
                {feedbackScore !== null && feedbackScore >= 4
                  ? '😊 피드백 감사합니다'
                  : '🙏 소중한 의견 감사합니다'}
              </span>
            )}
          </div>
        </div>
      </div>

      {/* 피드백 모달 */}
      {modalOpen && message.logSeq && (
        <FeedbackModal
          logSeq={message.logSeq}
          initialScore={2}
          onClose={() => setModalOpen(false)}
          onSubmit={handleModalSubmit}
        />
      )}
    </>
  )
}
