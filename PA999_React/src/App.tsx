import { useEffect, useRef, useState } from 'react'
import { useChat } from './hooks/useChat'
import { ChatBubble } from './components/ChatBubble'
import { ExampleButtons } from './components/ExampleButtons'
import { ChatInput } from './components/ChatInput'
import { HistorySidebar } from './components/HistorySidebar'

const DEMO_MODE = import.meta.env.VITE_DEMO_MODE === 'true'

export default function App() {
  const { messages, loading, ask, reset } = useChat()
  const bottomRef                         = useRef<HTMLDivElement>(null)
  const [sidebarOpen, setSidebarOpen]     = useState(false)

  // Auto-scroll to bottom on new messages
  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: 'smooth' })
  }, [messages, loading])

  /** 히스토리 사이드바 클릭 시 해당 메시지로 스크롤 */
  const scrollToMessage = (id: string) => {
    document.getElementById(`msg-${id}`)?.scrollIntoView({ behavior: 'smooth', block: 'start' })
    setSidebarOpen(false)
  }

  return (
    <div className="flex h-screen flex-col bg-gray-50">

      {/* ── 히스토리 사이드바 ── */}
      <HistorySidebar
        messages={messages}
        open={sidebarOpen}
        onClose={() => setSidebarOpen(false)}
        onScrollTo={scrollToMessage}
      />

      {/* ── Header ── */}
      <header className="flex items-center justify-between border-b border-gray-200 bg-white px-5 py-3 shadow-sm">
        <div className="flex items-center gap-3">
          {/* 히스토리 토글 버튼 */}
          <button
            onClick={() => setSidebarOpen(v => !v)}
            title="대화 히스토리"
            className="flex h-8 w-8 items-center justify-center rounded-lg text-gray-400
              hover:bg-gray-100 hover:text-gray-600 transition-colors"
            aria-label="대화 히스토리 열기"
          >
            <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M3.75 6.75h16.5M3.75 12h16.5m-16.5 5.25H12" />
            </svg>
          </button>

          {/* Logo mark */}
          <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-gradient-to-br from-blue-600 to-violet-600 shadow">
            <svg className="h-4 w-4 text-white" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round"
                d="M20.25 6.375c0 2.278-3.694 4.125-8.25 4.125S3.75 8.653 3.75 6.375m16.5 0c0-2.278-3.694-4.125-8.25-4.125S3.75 4.097 3.75 6.375m16.5 0v11.25c0 2.278-3.694 4.125-8.25 4.125s-8.25-1.847-8.25-4.125V6.375m16.5 2.625c0 2.278-3.694 4.125-8.25 4.125s-8.25-1.847-8.25-4.125m16.5 5.625c0 2.278-3.694 4.125-8.25 4.125s-8.25-1.847-8.25-4.125" />
            </svg>
          </div>
          <div>
            <h1 className="text-sm font-bold text-gray-900 leading-none">UNIERP AI 챗봇</h1>
            <p className="text-[10px] text-gray-400 mt-0.5">생산관리 데이터 조회</p>
          </div>
          {DEMO_MODE && (
            <span className="ml-1 rounded-full bg-amber-100 px-2 py-0.5 text-[10px] font-semibold text-amber-700 ring-1 ring-amber-200">
              DEMO
            </span>
          )}
        </div>

        <button
          onClick={reset}
          className="flex items-center gap-1.5 rounded-lg border border-gray-200 px-3 py-1.5 text-xs text-gray-500
            hover:border-gray-300 hover:bg-gray-50 hover:text-gray-700 transition-colors"
        >
          <svg className="h-3.5 w-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
            <path strokeLinecap="round" strokeLinejoin="round" d="M16.023 9.348h4.992v-.001M2.985 19.644v-4.992m0 0h4.992m-4.993 0l3.181 3.183a8.25 8.25 0 0013.803-3.7M4.031 9.865a8.25 8.25 0 0113.803-3.7l3.181 3.182m0-4.991v4.99" />
          </svg>
          새 대화
        </button>
      </header>

      {/* ── Messages ── */}
      <main className="flex-1 overflow-y-auto">
        {messages.length === 0 ? (
          /* Empty state */
          <div className="flex h-full flex-col items-center justify-center gap-3 text-center px-6">
            <div className="flex h-16 w-16 items-center justify-center rounded-2xl bg-gradient-to-br from-blue-100 to-violet-100 shadow-inner">
              <svg className="h-8 w-8 text-blue-500" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.5}>
                <path strokeLinecap="round" strokeLinejoin="round"
                  d="M7.5 8.25h9m-9 3H12m-9.75 1.51c0 1.6 1.123 2.994 2.707 3.227 1.129.166 2.27.293 3.423.379.35.026.67.21.865.501L12 21l2.755-4.133a1.14 1.14 0 01.865-.501 48.172 48.172 0 003.423-.379c1.584-.233 2.707-1.626 2.707-3.228V6.741c0-1.602-1.123-2.995-2.707-3.228A48.394 48.394 0 0012 3c-2.392 0-4.744.175-7.043.513C3.373 3.746 2.25 5.14 2.25 6.741v6.018z" />
              </svg>
            </div>
            <div>
              <p className="font-semibold text-gray-700">무엇이 궁금하신가요?</p>
              <p className="mt-1 text-sm text-gray-400">생산량, 공장 현황 등 ERP 데이터를 자연어로 질문하세요.</p>
            </div>
          </div>
        ) : (
          <div className="mx-auto max-w-3xl space-y-5 px-4 py-6">
            {messages.map(msg => (
              /* 히스토리 앵커 id — HistorySidebar onScrollTo에서 참조 */
              <div key={msg.id} id={`msg-${msg.id}`}>
                <ChatBubble message={msg} />
              </div>
            ))}

            {/* Loading indicator */}
            {loading && (
              <div className="flex items-start gap-2.5">
                <div className="flex-shrink-0 w-8 h-8 rounded-full bg-gradient-to-br from-blue-500 to-violet-600 flex items-center justify-center shadow-sm">
                  <svg className="w-4 h-4 text-white animate-pulse" fill="currentColor" viewBox="0 0 24 24">
                    <circle cx="12" cy="12" r="3" />
                  </svg>
                </div>
                <div className="rounded-2xl rounded-tl-sm border border-gray-200 bg-white px-4 py-3 shadow-sm">
                  <div className="flex items-center gap-1.5">
                    <span className="h-2 w-2 rounded-full bg-blue-400 animate-bounce [animation-delay:-0.3s]" />
                    <span className="h-2 w-2 rounded-full bg-blue-400 animate-bounce [animation-delay:-0.15s]" />
                    <span className="h-2 w-2 rounded-full bg-blue-400 animate-bounce" />
                  </div>
                </div>
              </div>
            )}

            <div ref={bottomRef} />
          </div>
        )}
      </main>

      {/* ── Bottom input area ── */}
      <div className="border-t border-gray-200 bg-white">
        <div className="mx-auto max-w-3xl">
          <ExampleButtons onSelect={ask} disabled={loading} />
          <div className="px-4 pb-4">
            <ChatInput onSend={ask} disabled={loading} />
            <p className="mt-2 text-center text-[10px] text-gray-300">
              AI가 생성한 답변은 참고용입니다. 중요한 의사결정 전 원본 데이터를 확인하세요.
            </p>
          </div>
        </div>
      </div>
    </div>
  )
}
