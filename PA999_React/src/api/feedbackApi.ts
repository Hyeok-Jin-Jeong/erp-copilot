// VITE_API_BASE_URL 미설정 시 Railway 배포 URL을 기본값으로 사용
const BASE_URL =
  (import.meta.env.VITE_API_BASE_URL as string | undefined)?.replace(/\/$/, '') ??
  'https://erp-copilot-api-production.up.railway.app'

const DEMO_MODE = import.meta.env.VITE_DEMO_MODE === 'true'

export interface UserFeedbackRequest {
  /** 응답 품질 점수 (1=매우 나쁨 ~ 5=매우 좋음) */
  perfScore:           number
  /** 사용자 피드백 내용 */
  devFeedback:         string
  /** 피드백 작성자 ID */
  feedbackBy?:         string
  /** 피드백 유형: "U"=사용자 */
  feedbackType:        'U'
  /** 점수 ≤ 2 시 PA999_FEEDBACK_PATTERN 자동 등록 */
  autoRegisterPattern: boolean
}

/**
 * AI 답변에 대한 사용자 피드백 전송
 * PATCH /api/PA999/log/{logSeq}/feedback
 */
export async function submitUserFeedback(
  logSeq: number,
  score:  number,
  text:   string,
): Promise<void> {
  if (DEMO_MODE) {
    // 데모 모드: API 호출 없이 성공 시뮬레이션
    await new Promise(r => setTimeout(r, 300))
    return
  }

  const body: UserFeedbackRequest = {
    perfScore:           score,
    devFeedback:         text,
    feedbackBy:          'USER',
    feedbackType:        'U',
    autoRegisterPattern: score <= 2,
  }

  const res = await fetch(`${BASE_URL}/api/PA999/log/${logSeq}/feedback`, {
    method:  'PATCH',
    headers: { 'Content-Type': 'application/json' },
    body:    JSON.stringify(body),
  })

  if (!res.ok) {
    throw new Error(`피드백 전송 실패: ${res.status}`)
  }
}
