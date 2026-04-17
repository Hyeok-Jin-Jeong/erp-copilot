import ReactMarkdown from 'react-markdown'
import remarkGfm from 'remark-gfm'
import type { Message } from '../types/api'
import { ModeBadge } from './ModeBadge'
import { GridTable } from './GridTable'
import { SqlCollapsible } from './SqlCollapsible'

interface Props { message: Message }

export function ChatBubble({ message }: Props) {
  const isUser = message.role === 'user'

  if (isUser) {
    return (
      <div className="flex justify-end">
        <div className="max-w-[75%] rounded-2xl rounded-tr-sm bg-blue-600 px-4 py-2.5 text-sm text-white shadow-sm">
          {message.text}
        </div>
      </div>
    )
  }

  // AI bubble
  return (
    <div className="flex items-start gap-2.5">
      {/* Avatar */}
      <div className="flex-shrink-0 w-8 h-8 rounded-full bg-gradient-to-br from-blue-500 to-violet-600 flex items-center justify-center shadow-sm">
        <svg className="w-4 h-4 text-white" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
          <path strokeLinecap="round" strokeLinejoin="round"
            d="M9.75 3.104v5.714a2.25 2.25 0 01-.659 1.591L5 14.5M9.75 3.104c-.251.023-.501.05-.75.082m.75-.082a24.301 24.301 0 014.5 0m0 0v5.714c0 .597.237 1.17.659 1.591L19.8 15.3M14.25 3.104c.251.023.501.05.75.082M19.8 15.3l-1.57.393A9.065 9.065 0 0112 15a9.065 9.065 0 00-6.23-.693L5 14.5m14.8.8l1.402 1.402c1 1 .03 2.703-1.352 2.703H4.15c-1.38 0-2.352-1.703-1.351-2.703L4.2 15.3" />
        </svg>
      </div>

      {/* Bubble */}
      <div className="max-w-[80%] min-w-0">
        {/* Mode badge */}
        {message.processMode && (
          <div className="mb-1.5">
            <ModeBadge mode={message.processMode} />
          </div>
        )}

        {/* Text (markdown) */}
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

          {/* Grid table */}
          {message.gridData && message.gridData.length > 0 && (
            <GridTable rows={message.gridData} />
          )}

          {/* SQL collapsible */}
          {message.generatedSql && (
            <SqlCollapsible sql={message.generatedSql} />
          )}
        </div>

        {/* Timestamp */}
        <div className="mt-1 px-1 text-[10px] text-gray-400">
          {message.timestamp.toLocaleTimeString('ko-KR', { hour: '2-digit', minute: '2-digit' })}
          {message.logSeq && (
            <span className="ml-1.5 opacity-60">#{message.logSeq}</span>
          )}
        </div>
      </div>
    </div>
  )
}
