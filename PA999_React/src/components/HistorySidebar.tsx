import type { Message } from '../types/api'

interface Props {
  messages:    Message[]
  open:        boolean
  onClose:     () => void
  onScrollTo:  (id: string) => void
}

export function HistorySidebar({ messages, open, onClose, onScrollTo }: Props) {
  const userMsgs = messages.filter(m => m.role === 'user')

  return (
    <>
      {/* ── 모바일 백드롭 ── */}
      {open && (
        <div
          className="fixed inset-0 z-20 bg-black/30 lg:hidden"
          onClick={onClose}
        />
      )}

      {/* ── 사이드바 패널 ── */}
      <aside
        className={`
          fixed left-0 top-0 z-30 flex h-full w-64 flex-col
          border-r border-gray-200 bg-white shadow-xl
          transition-transform duration-200
          ${open ? 'translate-x-0' : '-translate-x-full'}
        `}
      >
        {/* 헤더 */}
        <div className="flex items-center justify-between border-b border-gray-100 px-4 py-3">
          <div className="flex items-center gap-2">
            <svg className="h-4 w-4 text-blue-500" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round"
                d="M12 6v6h4.5m4.5 0a9 9 0 11-18 0 9 9 0 0118 0z" />
            </svg>
            <h2 className="text-sm font-semibold text-gray-700">대화 히스토리</h2>
          </div>
          <button
            onClick={onClose}
            className="rounded-lg p-1 text-gray-400 hover:bg-gray-100 hover:text-gray-600 transition-colors"
            aria-label="사이드바 닫기"
          >
            <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>

        {/* 질문 목록 */}
        <div className="flex-1 overflow-y-auto p-2">
          {userMsgs.length === 0 ? (
            <div className="mt-10 flex flex-col items-center gap-2 text-center px-4">
              <svg className="h-8 w-8 text-gray-300" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5}
                  d="M8.625 9.75a.375.375 0 11-.75 0 .375.375 0 01.75 0zm0 0H8.25m4.125 0a.375.375 0 11-.75 0 .375.375 0 01.75 0zm0 0H12m4.125 0a.375.375 0 11-.75 0 .375.375 0 01.75 0zm0 0h-.375m-13.5 3.01c0 1.6 1.123 2.994 2.707 3.227 1.087.16 2.185.283 3.293.369V21l4.184-4.183a1.14 1.14 0 01.778-.332 48.294 48.294 0 005.83-.498c1.585-.233 2.708-1.626 2.708-3.228V6.741c0-1.602-1.123-2.995-2.707-3.228A48.394 48.394 0 0012 3c-2.392 0-4.744.175-7.043.513C3.373 3.746 2.25 5.14 2.25 6.741v6.018z" />
              </svg>
              <p className="text-xs text-gray-400">아직 질문이 없어요</p>
            </div>
          ) : (
            <ul className="space-y-0.5">
              {userMsgs.map((msg, i) => (
                <li key={msg.id}>
                  <button
                    onClick={() => onScrollTo(msg.id)}
                    className="w-full rounded-lg px-3 py-2.5 text-left transition-colors hover:bg-blue-50 active:bg-blue-100 group"
                  >
                    <div className="flex items-start gap-2">
                      <span className="mt-0.5 flex-shrink-0 text-[10px] font-semibold text-blue-400 group-hover:text-blue-600">
                        #{i + 1}
                      </span>
                      <div className="min-w-0">
                        <p className="line-clamp-2 text-xs text-gray-700 leading-relaxed group-hover:text-gray-900">
                          {msg.text}
                        </p>
                        <p className="mt-0.5 text-[10px] text-gray-400">
                          {msg.timestamp.toLocaleTimeString('ko-KR', {
                            hour:   '2-digit',
                            minute: '2-digit',
                          })}
                        </p>
                      </div>
                    </div>
                  </button>
                </li>
              ))}
            </ul>
          )}
        </div>

        {/* 하단 메시지 수 표시 */}
        {userMsgs.length > 0 && (
          <div className="border-t border-gray-100 px-4 py-2.5">
            <p className="text-[10px] text-gray-400 text-center">
              총 {userMsgs.length}개의 질문
            </p>
          </div>
        )}
      </aside>
    </>
  )
}
