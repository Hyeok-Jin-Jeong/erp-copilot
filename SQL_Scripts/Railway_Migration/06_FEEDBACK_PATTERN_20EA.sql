-- ══════════════════════════════════════════════════════════════════
-- 06. PA999_FEEDBACK_PATTERN — 답변 품질 향상 패턴 20개 삽입
-- 주제: 날짜조건·DAY_MAGAM_YN·서브쿼리금지·테이블분기·집계·기간조건 등
-- 작성일: 2026-04-20
-- ══════════════════════════════════════════════════════════════════

USE PA999_DEMO;
GO

-- 기존 패턴 전체 초기화 후 재삽입
DELETE FROM PA999_FEEDBACK_PATTERN;
DBCC CHECKIDENT ('PA999_FEEDBACK_PATTERN', RESEED, 0);
PRINT 'PA999_FEEDBACK_PATTERN 초기화 완료';
GO

INSERT INTO PA999_FEEDBACK_PATTERN
    (QUERY_PATTERN, WRONG_APPROACH, CORRECT_SQL, LESSON, PRIORITY, APPLY_YN, PREFERRED_MODE)
VALUES

-- ── 1. 날짜 조건 없을 때 오늘/이번달 자동 적용 ────────────────────
(
N'날짜/기간 조건 없는 생산 실적 조회',
N'기간 조건 없이 전체 데이터 조회 → 수만 건 반환, 성능 저하',
N'-- 오늘 기준 (단일일)
SELECT H.PLANT_CD, B.PLANT_NM, SUM(H.PROD_QTY) AS 생산량
FROM P_PROD_DAILY_HDR_CKO087 H WITH(NOLOCK)
JOIN B_PLANT B WITH(NOLOCK) ON B.PLANT_CD = H.PLANT_CD
WHERE H.PROD_DT = CONVERT(CHAR(8), GETDATE(), 112)
  AND H.DAY_MAGAM_YN = ''Y''
GROUP BY H.PLANT_CD, B.PLANT_NM
ORDER BY 생산량 DESC

-- 이번달 기준
-- WHERE H.PROD_DT BETWEEN CONVERT(CHAR(6),GETDATE(),112)+''01''
--                     AND CONVERT(CHAR(8),GETDATE(),112)',
N'날짜 조건 미지정 시 자동 처리 규칙: 오늘=CONVERT(CHAR(8),GETDATE(),112), 이번달 시작=CONVERT(CHAR(6),GETDATE(),112)+''01'', 이번달 종료=CONVERT(CHAR(8),GETDATE(),112). 하드코딩 날짜 삽입 금지.',
3, 'Y', 'SQL'
),

-- ── 2. DAY_MAGAM_YN = 'Y' 조건 필수 ──────────────────────────────
(
N'생산실적 조회 시 일마감 조건(DAY_MAGAM_YN) 누락',
N'DAY_MAGAM_YN 조건 없이 조회 → 미마감(잠정) 데이터가 확정 실적에 섞여 오류값 반환 가능',
N'SELECT H.PLANT_CD, SUM(H.PROD_QTY) AS 생산량합계
FROM P_PROD_DAILY_HDR_CKO087 H WITH(NOLOCK)
WHERE H.PROD_DT BETWEEN ''20260301'' AND ''20260331''
  AND H.DAY_MAGAM_YN = ''Y''   -- 반드시 포함
GROUP BY H.PLANT_CD',
N'생산실적 테이블(DAILY_HDR/REMICON_HDR/REMITAL_HDR) 조회 시 DAY_MAGAM_YN = ''Y'' 조건 필수 포함. 이 조건 없으면 미확정 잠정 수치가 혼입되어 신뢰도 저하.',
2, 'Y', 'SQL'
),

-- ── 3. 공장명 서브쿼리 금지 → JOIN 사용 ──────────────────────────
(
N'공장명으로 필터 시 서브쿼리 사용',
N'WHERE H.PLANT_CD = (SELECT PLANT_CD FROM B_PLANT WHERE PLANT_NM LIKE ''%단양%'') — 단일값 보장 불가, 성능 저하',
N'SELECT H.PLANT_CD, B.PLANT_NM, SUM(H.PROD_QTY) AS 생산량
FROM P_PROD_DAILY_HDR_CKO087 H WITH(NOLOCK)
JOIN B_PLANT B WITH(NOLOCK) ON B.PLANT_CD = H.PLANT_CD
WHERE B.PLANT_NM LIKE ''%단양%''
  AND H.DAY_MAGAM_YN = ''Y''
GROUP BY H.PLANT_CD, B.PLANT_NM',
N'공장명(PLANT_NM) 조건 필터 시 서브쿼리 금지. 반드시 B_PLANT와 JOIN 사용. 서브쿼리는 복수행 오류 위험 있음.',
4, 'Y', 'SQL'
),

-- ── 4. 레미콘 공장 → P_PROD_REMICON_HDR_CKO087, 단위 M3 ──────────
(
N'레미콘 공장 생산량 조회 시 잘못된 테이블 사용',
N'P_PROD_DAILY_HDR_CKO087(반복제조 테이블) 사용 → 레미콘 데이터 없음',
N'SELECT H.PLANT_CD, B.PLANT_NM, SUM(H.PROD_QTY) AS 생산량_M3
FROM P_PROD_REMICON_HDR_CKO087 H WITH(NOLOCK)
JOIN B_PLANT B WITH(NOLOCK) ON B.PLANT_CD = H.PLANT_CD
WHERE H.PROD_DT BETWEEN ''20260301'' AND ''20260331''
  AND H.DAY_MAGAM_YN = ''Y''
  AND B.PLANT_GUBUN_CD = ''300''
GROUP BY H.PLANT_CD, B.PLANT_NM
ORDER BY 생산량_M3 DESC',
N'레미콘(PLANT_GUBUN_CD=300) 공장 생산실적은 P_PROD_REMICON_HDR_CKO087 사용. 단위는 M3(입방미터, TON 아님). P_PROD_DAILY_HDR_CKO087는 반복제조(시멘트) 전용.',
3, 'Y', 'SQL'
),

-- ── 5. 레미탈 공장 → P_PROD_REMITAL_HDR_CKO087 ───────────────────
(
N'레미탈 공장 생산량 조회',
N'레미탈 공장임에도 레미콘/반복제조 테이블 사용',
N'SELECT H.PLANT_CD, B.PLANT_NM, SUM(H.PROD_QTY) AS 생산량_TON
FROM P_PROD_REMITAL_HDR_CKO087 H WITH(NOLOCK)
JOIN B_PLANT B WITH(NOLOCK) ON B.PLANT_CD = H.PLANT_CD
WHERE H.PROD_DT BETWEEN CONVERT(CHAR(6),GETDATE(),112)+''01''
                    AND CONVERT(CHAR(8),GETDATE(),112)
  AND H.DAY_MAGAM_YN = ''Y''
  AND B.PLANT_GUBUN_CD = ''400''
GROUP BY H.PLANT_CD, B.PLANT_NM',
N'레미탈(PLANT_GUBUN_CD=400) 공장 생산실적은 P_PROD_REMITAL_HDR_CKO087 사용. 단위 TON. 대상 공장: P050(수원레미탈), P051(광주레미탈).',
4, 'Y', 'SQL'
),

-- ── 6. 반복제조(시멘트) → P_PROD_DAILY_HDR_CKO087 ────────────────
(
N'시멘트/반복제조 공장 생산량 조회',
N'반복제조 공장임에도 레미콘/레미탈 테이블 사용, 또는 PLANT_GUBUN_CD 미사용',
N'SELECT H.PLANT_CD, B.PLANT_NM,
       H.WC_CD AS 호기코드,
       SUM(H.PROD_QTY) AS 생산량_TON
FROM P_PROD_DAILY_HDR_CKO087 H WITH(NOLOCK)
JOIN B_PLANT B WITH(NOLOCK) ON B.PLANT_CD = H.PLANT_CD
                            AND B.PLANT_GUBUN_CD = ''100''
WHERE H.PROD_DT = CONVERT(CHAR(8), GETDATE(), 112)
  AND H.DAY_MAGAM_YN = ''Y''
GROUP BY H.PLANT_CD, B.PLANT_NM, H.WC_CD',
N'반복제조(시멘트, PLANT_GUBUN_CD=100) 공장(P001=단양, P031=영월) 생산실적은 P_PROD_DAILY_HDR_CKO087. 단위 TON. JOIN 조건에 B.PLANT_GUBUN_CD=''100'' 포함 권장.',
3, 'Y', 'SQL'
),

-- ── 7. TOP N 임의 삽입 금지 ───────────────────────────────────────
(
N'사용자 요청 없이 TOP N 임의 삽입',
N'SELECT TOP 10 ... — 사용자가 "상위 10개"를 요청하지 않았는데 임의 추가',
NULL,
N'TOP N은 사용자가 명시적으로 "TOP 5", "상위 3개" 등을 요청했을 때만 사용. 임의 삽입 금지. 전체 결과 반환이 기본.',
5, 'Y', 'SQL'
),

-- ── 8. 생산량 집계 SUM(PROD_QTY) + GROUP BY ───────────────────────
(
N'생산량 합계/집계 조회 시 SUM 누락',
N'SELECT PROD_QTY — SUM 없이 단일행 조회, 여러 행이 있으면 집계 불가',
N'SELECT H.PLANT_CD, B.PLANT_NM,
       SUM(H.PROD_QTY) AS 생산량합계
FROM P_PROD_DAILY_HDR_CKO087 H WITH(NOLOCK)
JOIN B_PLANT B WITH(NOLOCK) ON B.PLANT_CD = H.PLANT_CD
WHERE H.PROD_DT BETWEEN ''20260301'' AND ''20260331''
  AND H.DAY_MAGAM_YN = ''Y''
GROUP BY H.PLANT_CD, B.PLANT_NM
ORDER BY 생산량합계 DESC',
N'생산량 합계/집계는 반드시 SUM(PROD_QTY) + GROUP BY 사용. 단순 SELECT PROD_QTY는 행 목록 조회이지 합계가 아님.',
3, 'Y', 'SQL'
),

-- ── 9. 기간 조회 BETWEEN 사용 ─────────────────────────────────────
(
N'날짜 범위 조건 시 BETWEEN 미사용',
N'WHERE PROD_DT >= ''20260301'' AND PROD_DT < ''20260401'' — 불필요하게 복잡, 마지막 날 누락 위험',
N'SELECT H.PLANT_CD, SUM(H.PROD_QTY) AS 생산량
FROM P_PROD_DAILY_HDR_CKO087 H WITH(NOLOCK)
WHERE H.PROD_DT BETWEEN ''20260301'' AND ''20260331''
  AND H.DAY_MAGAM_YN = ''Y''
GROUP BY H.PLANT_CD',
N'CHAR(8) 형식 PROD_DT 기간 조건은 BETWEEN ''시작일'' AND ''종료일'' 사용. >= < 조합은 종료일 누락 오류 위험 있음.',
5, 'Y', 'SQL'
),

-- ── 10. 이번 달 동적 조건 ─────────────────────────────────────────
(
N'이번 달 데이터 조회 시 하드코딩 날짜 사용',
N'WHERE PROD_DT BETWEEN ''20260401'' AND ''20260430'' — 매달 SQL이 달라짐',
N'-- 이번 달 1일 ~ 오늘
WHERE H.PROD_DT BETWEEN CONVERT(CHAR(6),GETDATE(),112)+''01''
                    AND CONVERT(CHAR(8),GETDATE(),112)

-- 전월 전체
-- DECLARE @LastMonth CHAR(6) = CONVERT(CHAR(6),DATEADD(MONTH,-1,GETDATE()),112)
-- WHERE H.PROD_DT BETWEEN @LastMonth+''01''
--                     AND @LastMonth+RIGHT(DAY(EOMONTH(DATEADD(MONTH,-1,GETDATE()))),2)',
N'이번 달 조회는 동적 날짜 사용: 시작=CONVERT(CHAR(6),GETDATE(),112)+''01'', 종료=CONVERT(CHAR(8),GETDATE(),112). 하드코딩 날짜 절대 금지.',
2, 'Y', 'SQL'
),

-- ── 11. 작업장명 P_WORK_CENTER JOIN 필수 ────────────────────────
(
N'작업장명(WC_NM) 조회 시 P_WORK_CENTER 미조인',
N'생산실적 테이블에 WC_NM 컬럼이 있다고 가정하여 조회 — 실제 컬럼 없음, 오류 발생',
N'SELECT H.WC_CD AS 작업장코드,
       W.WC_NM  AS 작업장명,
       SUM(H.PROD_QTY) AS 생산량_TON
FROM P_PROD_DAILY_HDR_CKO087 H WITH(NOLOCK)
LEFT JOIN P_WORK_CENTER W WITH(NOLOCK)
       ON W.PLANT_CD = H.PLANT_CD AND W.WC_CD = H.WC_CD
WHERE H.PLANT_CD = ''P001''
  AND H.PROD_DT = CONVERT(CHAR(8), GETDATE(), 112)
  AND H.DAY_MAGAM_YN = ''Y''
GROUP BY H.WC_CD, W.WC_NM',
N'작업장명(WC_NM)은 생산실적 테이블에 없음. P_WORK_CENTER를 LEFT JOIN하여 조회. JOIN 조건: W.PLANT_CD = H.PLANT_CD AND W.WC_CD = H.WC_CD.',
4, 'Y', 'SQL'
),

-- ── 12. 공장 전체 조회 시 UNION ALL 4개 테이블 ────────────────────
(
N'공장 구분 미지정 시 전체 생산실적 조회',
N'한 테이블만 조회 — 반복제조 외 공장 데이터 누락',
N'SELECT 공장구분, PLANT_CD, SUM(PROD_QTY) AS 생산량
FROM (
    SELECT ''반복제조'' AS 공장구분, PLANT_CD, PROD_QTY
    FROM P_PROD_DAILY_HDR_CKO087 WITH(NOLOCK)
    WHERE DAY_MAGAM_YN = ''Y''
    AND PROD_DT BETWEEN ''20260301'' AND ''20260331''
    UNION ALL
    SELECT ''레미콘'', PLANT_CD, PROD_QTY
    FROM P_PROD_REMICON_HDR_CKO087 WITH(NOLOCK)
    WHERE DAY_MAGAM_YN = ''Y''
    AND PROD_DT BETWEEN ''20260301'' AND ''20260331''
    UNION ALL
    SELECT ''레미탈'', PLANT_CD, PROD_QTY
    FROM P_PROD_REMITAL_HDR_CKO087 WITH(NOLOCK)
    WHERE DAY_MAGAM_YN = ''Y''
    AND PROD_DT BETWEEN ''20260301'' AND ''20260331''
) A
GROUP BY 공장구분, PLANT_CD
ORDER BY 공장구분, PLANT_CD',
N'공장 구분을 지정하지 않은 전체 생산실적 조회 시 반복제조/레미콘/레미탈 3개(또는 4개) 테이블 UNION ALL. 한 테이블만 조회하면 타 공장 유형 누락.',
3, 'Y', 'SQL'
),

-- ── 13. PLANT_GUBUN_CD 활용 ─────────────────────────────────────
(
N'공장 유형 분기 조건에 PLANT_TYPE 문자열 사용',
N'WHERE B.PLANT_TYPE = ''REMICON'' — PLANT_GUBUN_CD가 더 정확한 분기 기준',
N'SELECT P.PLANT_CD, P.PLANT_NM,
       CASE P.PLANT_GUBUN_CD
           WHEN ''100'' THEN ''반복제조(시멘트)''
           WHEN ''300'' THEN ''레미콘''
           WHEN ''400'' THEN ''레미탈''
           ELSE ''기타''
       END AS 공장유형
FROM B_PLANT P WITH(NOLOCK)
WHERE P.USE_YN = ''Y''
ORDER BY P.PLANT_GUBUN_CD, P.PLANT_CD',
N'공장 유형별 분기·필터 시 B_PLANT.PLANT_GUBUN_CD 사용: 100=반복제조, 200=지역공장, 300=레미콘, 400=레미탈. PLANT_TYPE 문자열보다 PLANT_GUBUN_CD가 신뢰도 높음.',
4, 'Y', 'SQL'
),

-- ── 14. 달성률 계산 NULLIF 활용 ──────────────────────────────────
(
N'생산 목표 달성률 계산 시 0 나누기 오류',
N'PROD_QTY / PLAN_QTY * 100 — PLAN_QTY=0이면 오류 발생',
N'SELECT H.PLANT_CD, B.PLANT_NM,
       SUM(H.PROD_QTY) AS 생산량_TON,
       SUM(H.PLAN_QTY) AS 계획량_TON,
       CAST(
           SUM(H.PROD_QTY) / NULLIF(SUM(H.PLAN_QTY), 0) * 100
       AS DECIMAL(5,1)) AS 달성률_PCT
FROM P_PROD_DAILY_HDR_CKO087 H WITH(NOLOCK)
JOIN B_PLANT B WITH(NOLOCK) ON B.PLANT_CD = H.PLANT_CD
WHERE H.PROD_DT BETWEEN ''20260301'' AND ''20260331''
  AND H.DAY_MAGAM_YN = ''Y''
GROUP BY H.PLANT_CD, B.PLANT_NM',
N'달성률 = PROD_QTY / NULLIF(PLAN_QTY, 0) * 100. NULLIF로 0 나누기 방지 필수. CAST(... AS DECIMAL(5,1))로 소수점 한 자리 표시 권장.',
5, 'Y', 'SQL'
),

-- ── 15. 가동시간 FAC_WORK_TIME_MM (분 단위, 변환 요청 시만) ───────
(
N'가동시간 조회 시 컬럼명 오류 또는 임의 단위 변환',
N'FAC_WORK_TIME 또는 WORK_TIME 등 존재하지 않는 컬럼명 사용, 또는 분→시간 무단 변환',
N'SELECT H.PLANT_CD, H.WC_CD,
       H.FAC_WORK_TIME_MM          AS 가동시간_분,
       CAST(H.FAC_WORK_TIME_MM / 60.0 AS DECIMAL(6,1)) AS 가동시간_시간
FROM P_PROD_DAILY_HDR_CKO087 H WITH(NOLOCK)
WHERE H.PLANT_CD = ''P001''
  AND H.PROD_DT = CONVERT(CHAR(8), GETDATE(), 112)
  AND H.DAY_MAGAM_YN = ''Y''',
N'가동시간 컬럼명은 FAC_WORK_TIME_MM (분 단위). 사용자가 시간 변환을 요청하지 않으면 분 단위 그대로 출력. 변환 시 /60.0 사용.',
5, 'Y', 'SQL'
),

-- ── 16. 레미콘 단위 M3 (TON 혼용 금지) ───────────────────────────
(
N'레미콘 생산량 단위를 TON으로 표기',
N'레미콘 생산량을 TON으로 안내 — 실제 단위는 M3(입방미터)',
NULL,
N'레미콘 생산량 단위는 M3(입방미터, 세제곱미터). TON으로 혼용 금지. 반드시 "M3" 또는 "입방미터" 표기. 시멘트/레미탈은 TON.',
4, 'Y', 'SQL'
),

-- ── 17. 미마감 실적 조회 DAY_MAGAM_YN = 'N' ──────────────────────
(
N'미마감(잠정) 실적 현황 조회',
N'DAY_MAGAM_YN=''N'' 조건 없이 미마감 건수 조회 → 잘못된 결과',
N'SELECT ''반복제조'' AS 공장구분, H.PLANT_CD, B.PLANT_NM, COUNT(*) AS 미마감건수
FROM P_PROD_DAILY_HDR_CKO087 H WITH(NOLOCK)
JOIN B_PLANT B WITH(NOLOCK) ON B.PLANT_CD = H.PLANT_CD
WHERE H.DAY_MAGAM_YN = ''N''
  AND H.PROD_DT < CONVERT(CHAR(8), GETDATE(), 112)  -- 오늘 이전 행만
GROUP BY H.PLANT_CD, B.PLANT_NM
UNION ALL
SELECT ''레미콘'', H.PLANT_CD, B.PLANT_NM, COUNT(*)
FROM P_PROD_REMICON_HDR_CKO087 H WITH(NOLOCK)
JOIN B_PLANT B WITH(NOLOCK) ON B.PLANT_CD = H.PLANT_CD
WHERE H.DAY_MAGAM_YN = ''N''
  AND H.PROD_DT < CONVERT(CHAR(8), GETDATE(), 112)
GROUP BY H.PLANT_CD, B.PLANT_NM
ORDER BY 1, 2',
N'미마감(잠정) 실적 = DAY_MAGAM_YN = ''N''. 마감 완료 = ''Y''. 미마감 현황 조회 시 오늘 이전(PROD_DT < 오늘) 데이터로 범위 한정 권장.',
5, 'Y', 'SQL'
),

-- ── 18. 호기(WC_CD)별 생산량 조회 ────────────────────────────────
(
N'반복제조 공장 호기별 생산실적 조회',
N'공장 단위로만 집계, 호기별 세분화 미적용',
N'SELECT H.PLANT_CD, B.PLANT_NM,
       H.WC_CD  AS 호기코드,
       W.WC_NM  AS 호기명,
       SUM(H.PROD_QTY)         AS 생산량_TON,
       SUM(H.FAC_WORK_TIME_MM) AS 총가동시간_분
FROM P_PROD_DAILY_HDR_CKO087 H WITH(NOLOCK)
LEFT JOIN P_WORK_CENTER W WITH(NOLOCK)
       ON W.PLANT_CD = H.PLANT_CD AND W.WC_CD = H.WC_CD
JOIN B_PLANT B WITH(NOLOCK) ON B.PLANT_CD = H.PLANT_CD
WHERE H.PLANT_CD = ''P001''
  AND H.PROD_DT BETWEEN ''20260301'' AND ''20260331''
  AND H.DAY_MAGAM_YN = ''Y''
GROUP BY H.PLANT_CD, B.PLANT_NM, H.WC_CD, W.WC_NM
ORDER BY H.WC_CD',
N'반복제조 공장 호기별 집계: PLANT_CD + WC_CD 기준 GROUP BY. 호기명은 P_WORK_CENTER LEFT JOIN. WC_GROUP_CD LIKE ''RM%''로 소성/원료계열 필터링 가능.',
5, 'Y', 'SQL'
),

-- ── 19. 공장명 출력 시 B_PLANT JOIN 필수 ─────────────────────────
(
N'공장명(PLANT_NM) 출력 시 B_PLANT 미조인',
N'생산실적 테이블에 PLANT_NM이 있다고 가정 — 실제로 없어 오류 발생',
N'SELECT H.PLANT_CD,
       B.PLANT_NM,    -- B_PLANT JOIN 필수
       B.REGION_NM,
       SUM(H.PROD_QTY) AS 생산량
FROM P_PROD_DAILY_HDR_CKO087 H WITH(NOLOCK)
JOIN B_PLANT B WITH(NOLOCK) ON B.PLANT_CD = H.PLANT_CD
WHERE H.PROD_DT = CONVERT(CHAR(8), GETDATE(), 112)
  AND H.DAY_MAGAM_YN = ''Y''
GROUP BY H.PLANT_CD, B.PLANT_NM, B.REGION_NM',
N'생산실적 테이블에는 PLANT_NM, REGION_NM 없음. 공장명/지역명 출력 필요 시 반드시 B_PLANT JOIN. 공장코드(PLANT_CD)만 존재.',
4, 'Y', 'SQL'
),

-- ── 20. 공장별 생산 순위 비교 ────────────────────────────────────
(
N'공장별 생산량 비교/순위 조회',
N'ORDER BY 없이 비교 결과 제공, 또는 단위 혼용(TON+M3 합산)',
N'-- 반복제조 공장 비교 (단위: TON)
SELECT B.PLANT_NM, SUM(H.PROD_QTY) AS 생산량_TON,
       RANK() OVER (ORDER BY SUM(H.PROD_QTY) DESC) AS 순위
FROM P_PROD_DAILY_HDR_CKO087 H WITH(NOLOCK)
JOIN B_PLANT B WITH(NOLOCK) ON B.PLANT_CD = H.PLANT_CD
WHERE H.PROD_DT BETWEEN ''20260301'' AND ''20260331''
  AND H.DAY_MAGAM_YN = ''Y''
GROUP BY B.PLANT_NM
ORDER BY 생산량_TON DESC

-- ※ 레미콘(M3)과 반복제조(TON)는 단위가 달라 UNION 합산 금지',
N'공장별 생산량 순위는 RANK() OVER (ORDER BY SUM(PROD_QTY) DESC) 사용. 레미콘(M3)과 시멘트(TON)는 단위가 달라 단순 합산 또는 UNION 합산 금지. 유형별로 분리 출력.',
4, 'Y', 'SQL'
);

PRINT 'PA999_FEEDBACK_PATTERN 삽입 완료: ' + CAST(@@ROWCOUNT AS VARCHAR) + '건';
GO

-- 검증
SELECT
    PATTERN_SEQ,
    PRIORITY,
    LEFT(QUERY_PATTERN, 40)  AS 패턴요약,
    CASE WHEN CORRECT_SQL IS NULL THEN '[행동지침]'
         ELSE '[SQL]'
    END AS 구분,
    LEFT(LESSON, 60) AS 교훈미리보기
FROM PA999_FEEDBACK_PATTERN
ORDER BY PATTERN_SEQ;
GO

PRINT '══ 06 완료: 피드백 패턴 20건 삽입 ══';
GO
