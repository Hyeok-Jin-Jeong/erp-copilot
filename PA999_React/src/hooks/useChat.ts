import { useState, useCallback, useRef } from 'react'
import { v4 as uuidv4 } from 'uuid'
import { sendMessage, resetSession } from '../api/chatApi'
import type { Message } from '../types/api'

export function useChat() {
  const [messages,  setMessages]  = useState<Message[]>([])
  const [loading,   setLoading]   = useState(false)
  const [error,     setError]     = useState<string | null>(null)
  const sessionId = useRef(uuidv4())

  const ask = useCallback(async (question: string) => {
    if (!question.trim() || loading) return

    // 1) 사용자 메시지 즉시 추가
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
    sessionId.current = uuidv4()
    setMessages([])
    setError(null)
  }, [])

  return { messages, loading, error, ask, reset }
}
