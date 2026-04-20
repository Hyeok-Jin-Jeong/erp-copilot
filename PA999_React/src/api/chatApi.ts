import type { ChatRequest, ChatResponse, GridRow } from '../types/api'

// VITE_API_BASE_URL 미설정 시 Railway 배포 URL을 기본값으로 사용
const BASE_URL =
  (import.meta.env.VITE_API_BASE_URL as string | undefined)?.replace(/\/$/, '') ??
  'https://erp-copilot-api-production.up.railway.app'

const DEMO_MODE = import.meta.env.VITE_DEMO_MODE === 'true'

// ── Mock 응답 (VITE_DEMO_MODE=true 또는 서버 없을 때) ──────────
const MOCK_RESPONSES: Record<string, Partial<ChatResponse>> = {
  default: {
    answer: '## 조회 결과\n단양공장(P001) **2026년 3월** 생산 현황입니다.',
    processMode: 'SQL',
    generatedSql:
      "SELECT PLANT_CD, PROD_DT, SUM(PROD_QTY) AS PROD_QTY\nFROM P_PROD_DAILY_HDR (NOLOCK)\nWHERE PLANT_CD = 'P001'\n  AND PROD_DT BETWEEN '20260301' AND '20260331'\nGROUP BY PLANT_CD, PROD_DT\nORDER BY PROD_DT",
    gridData: [
      { PLANT_CD: 'P001', PROD_DT: '20260301', PROD_QTY: 4_980.7 },
      { PLANT_CD: 'P001', PROD_DT: '20260302', PROD_QTY: 5_219.8 },
      { PLANT_CD: 'P001', PROD_DT: '20260303', PROD_QTY: 4_812.3 },
    ] as GridRow[],
  },
  remicon: {
    answer:
      '## 레미콘 공장 전체 목록\n현재 UNIERP에 등록된 레미콘 공장은 총 **10개**입니다.',
    processMode: 'SQL',
    generatedSql:
      "SELECT PLANT_CD, PLANT_NM, REGION_NM\nFROM B_PLANT (NOLOCK)\nWHERE PLANT_TYPE = 'REMICON' AND USE_YN = 'Y'\nORDER BY PLANT_CD",
    gridData: [
      { PLANT_CD: 'P003', PLANT_NM: '대구공장',   REGION_NM: '대구' },
      { PLANT_CD: 'P004', PLANT_NM: '부산공장',   REGION_NM: '부산' },
      { PLANT_CD: 'P006', PLANT_NM: '청주공장',   REGION_NM: '충북' },
      { PLANT_CD: 'P007', PLANT_NM: '성남공장',   REGION_NM: '경기' },
      { PLANT_CD: 'P008', PLANT_NM: '대전공장',   REGION_NM: '대전' },
      { PLANT_CD: 'P011', PLANT_NM: '김해공장',   REGION_NM: '경남' },
      { PLANT_CD: 'P014', PLANT_NM: '서대구공장', REGION_NM: '대구' },
      { PLANT_CD: 'P020', PLANT_NM: '서인천공장', REGION_NM: '인천' },
      { PLANT_CD: 'P022', PLANT_NM: '화성공장',   REGION_NM: '경기' },
      { PLANT_CD: 'P025', PLANT_NM: '부천공장',   REGION_NM: '경기' },
    ] as GridRow[],
  },
  top3: {
    answer:
      '## 생산량 TOP 3 공장\n2026년 3월 기준 전체 공장 생산량 순위입니다.',
    processMode: 'SQL',
    generatedSql:
      "SELECT TOP 3\n  p.PLANT_NM,\n  SUM(h.PROD_QTY) AS TOTAL_PROD_QTY\nFROM P_PROD_DAILY_HDR h (NOLOCK)\nJOIN B_PLANT p (NOLOCK) ON p.PLANT_CD = h.PLANT_CD\nWHERE h.PROD_DT BETWEEN '20260301' AND '20260331'\nGROUP BY p.PLANT_NM\nORDER BY TOTAL_PROD_QTY DESC",
    gridData: [
      { RANK: 1, PLANT_NM: '단양공장',   TOTAL_PROD_QTY: 148_320.5 },
      { RANK: 2, PLANT_NM: '영월공장',   TOTAL_PROD_QTY: 132_870.2 },
      { RANK: 3, PLANT_NM: '삼곡공장',   TOTAL_PROD_QTY: 119_540.8 },
    ] as GridRow[],
  },
}

function pickMock(question: string): ChatResponse {
  const q = question
  let key = 'default'
  if (q.includes('레미콘') && q.includes('리스트')) key = 'remicon'
  if (q.includes('TOP') || q.includes('top') || q.includes('순위')) key = 'top3'
  const m = MOCK_RESPONSES[key] ?? MOCK_RESPONSES['default']
  return {
    answer:      m.answer ?? '(Mock 응답)',
    sessionId:   'mock-session',
    isError:     false,
    processMode: m.processMode ?? 'SQL',
    generatedSql: m.generatedSql ?? null,
    gridData:    m.gridData ?? null,
  }
}

// ── 실제 API 호출 ───────────────────────────────────────────────
export async function sendMessage(req: ChatRequest): Promise<ChatResponse> {
  if (DEMO_MODE) {
    await new Promise(r => setTimeout(r, 900))   // 로딩 느낌
    return pickMock(req.question)
  }

  const res = await fetch(`${BASE_URL}/api/PA999/ask`, {
    method:  'POST',
    headers: { 'Content-Type': 'application/json' },
    body:    JSON.stringify(req),
  })

  if (!res.ok) {
    const msg = await res.text().catch(() => res.statusText)
    throw new Error(`서버 오류 ${res.status}: ${msg}`)
  }
  return (await res.json()) as ChatResponse
}

export async function resetSession(sessionId: string): Promise<void> {
  if (DEMO_MODE) return
  await fetch(`${BASE_URL}/api/PA999/session/${sessionId}`, {
    method: 'DELETE',
  })
}
