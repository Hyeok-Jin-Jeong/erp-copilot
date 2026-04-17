// ── API 요청/응답 타입 (PA999Models.cs 와 1:1 대응) ─────────────

export interface ChatRequest {
  question:   string
  sessionId:  string
  userId?:    string
  plantCd?:   string
  deptCd?:    string
  userRole?:  string
}

export interface ChatResponse {
  answer:          string
  sessionId:       string
  generatedSql?:   string | null
  relevantTables?: string[] | null
  isError:         boolean
  processMode?:    'SP' | 'SQL' | 'SOP' | string | null
  sopGuide?:       string | null
  menuPath?:       string | null
  logSeq?:         number | null
  gridData?:       GridRow[] | null
}

export type GridRow = Record<string, unknown>

// ── UI 전용 메시지 타입 ─────────────────────────────────────────

export type Role = 'user' | 'assistant'

export interface Message {
  id:          string
  role:        Role
  text:        string          // 사용자 질문 또는 AI answer
  timestamp:   Date
  // AI 응답 전용
  gridData?:   GridRow[] | null
  generatedSql?: string | null
  processMode?:  string | null
  isError?:    boolean
  logSeq?:     number | null
}
