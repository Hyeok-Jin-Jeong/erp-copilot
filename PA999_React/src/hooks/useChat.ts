import { useState, useCallback, useRef, useEffect } from 'react'
import { v4 as uuidv4 } from 'uuid'
import { sendMessage, resetSession } from '../api/chatApi'
import type { Message } from '../types/api'

// ── localStorage 설정 ───────────────────────────────────────────
const STORAGE_KEY   = 'erp-copilot-history'
const SESSION_KEY   = 'erp-copilot-session'
const MAX_MESSAGES  = 50

/** localStorage에서 메시지 목록 복원 (timestamp string → Date 변환) */
function loadMessages(): Message[] {
  try {
    const raw = localStorage.getItem(STORAGE_KEY)
    if (!raw) return []
    const parsed = JSON.parse(raw) as Message[]
    // JSON.parse 후 timestamp는 string → Date 복원
    return parsed.map(m => ({ ...m, timestamp: new Date(m.timestamp) }))
  } catch {
    return []
  }
}

/** 메시지 목록을 localStorage에 저장 (최대 MAX_MESSAGES개) */
function saveMessages(msgs: Message[]): void {
  try {
    const trimmed = msgs.slice(-MAX_MESSAGES)
    localStorage.setItem(STORAGE_KEY, JSON.stringify(trimmed))
  } catch {
    // QuotaExceededError 등 무시 — 저장 실패해도 UI 동작은 유지
  }
}

/** localStorage에서 세션 ID 복원 (없으면 새 UUID 생성 후 저장) */
function loadSessionId(): string {
  try {
    const stored = localStorage.getItem(SESSION_KEY)
    if (stored) return stored
    const id = uuidv4()
    localStorage.setItem(SESSION_KEY, id)
    return id
  } catch {
    return uuidv4()
  }
}

// ── 훅 ──────────────────────────────────────────────────────────

export function useChat() {
  const [messages, setMessages] = useState<Message[]>(() => loadMessages())
  const [loading,  setLoading]  = useState(false)
  const [error,    setError]    = useState<string | null>(null)

  // 세션 ID: localStorage에서 복원 → 새로고침해도 같은 세션 유지
  const sessionId = useRef<string>(loadSessionId())

  // messages 변경 시마다 localStorage 동기화
  useEffect(() => {
    saveMessages(messages)
  }, [messages])

  const ask = useCallback(async (question: string) => {
    if (!question.trim() || loading) return

    const userMsg: Message = {
      id:        uuidv4(),
      role:      'user',
      text:      question,
      timestamp: new Date(),
    }
    setMessages(prev => [...prev, userMsg])
    setLoading(true)
    setError(null)

    try {
      const res = await sendMessage({
        question,
        sessionId: sessionId.current,
        userId:    'DEMO',
      })

      const aiMsg: Message = {
        id:           uuidv4(),
        role:         'assistant',
        text:         res.answer,
        timestamp:    new Date(),
        gridData:     res.gridData,
        generatedSql: res.generatedSql,
        processMode:  res.processMode,
        isError:      res.isError,
        logSeq:       res.logSeq,
      }
      setMessages(prev => [...prev, aiMsg])
    } catch (e) {
      const msg = e instanceof Error ? e.message : '알 수 없는 오류가 발생했습니다.'
      setError(msg)
      setMessages(prev => [
        ...prev,
        {
          id:        uuidv4(),
          role:      'assistant',
          text:      `⚠️ ${msg}`,
          timestamp: new Date(),
          isError:   true,
        },
      ])
    } finally {
      setLoading(false)
    }
  }, [loading])

  const reset = useCallback(async () => {
    await resetSession(sessionId.current)
    // 새 세션 ID 발급 후 localStorage 갱신
    const newId = uuidv4()
    sessionId.current = newId
    try {
      localStorage.setItem(SESSION_KEY, newId)
      localStorage.removeItem(STORAGE_KEY)
    } catch { /* 무시 */ }
    setMessages([])
    setError(null)
  }, [])

  return { messages, loading, error, ask, reset }
}
