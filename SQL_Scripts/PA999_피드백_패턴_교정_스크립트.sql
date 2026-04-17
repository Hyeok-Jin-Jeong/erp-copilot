-- ═══════════════════════════════════════════════════════════════════════════
-- PA999_FEEDBACK_PATTERN 교정 스크립트
-- 실행 대상: UNIERP60N DB (DBA 권한 필요)
-- 목적: CORRECT_SQL에 들어간 자연어를 LESSON으로 이동, 올바른 SQL로 교체
-- 날짜: 2026-04-03
-- ═══════════════════════════════════════════════════════════════════════════

BEGIN TRAN;

-- ════════════════════════════════════════════════════════════════
-- 1. WRONG_APPROACH 컬럼 MAX로 확장 (복잡한 SQL 저장 가능하도록)
-- ════════════════════════════════════════════════════════════════
ALTER TABLE PA999_FEEDBACK_PATTERN ALTER COLUMN WRONG_APPROACH NVARCHAR(MAX);

-- ════════════════════════════════════════════════════════════════
-- 2. 자연어 → LESSON 이동 + 올바른 SQL 교체
-- ════════════════════════════════════════════════════════════════

-- ── SEQ 21, 22, 23: "B_PLANT 테이블, PLANT_GUBUN_CD 활용" ─────────
-- 레미탈/레미콘 공장 목록 조회
UPDATE PA999_FEEDBACK_PATTERN SET
    LESSON = N'공장 목록/유형 조회 시 B_PLANT 테이블의 PLANT_GUBUN_CD 컬럼 활용. 반복제조=100, 지역공장=200, 레미콘=300, 레미탈=400',
    CORRECT_SQL = N'SELECT P.PLANT_CD AS 공장코드, P.PLANT_NM AS 공장명, P.BIZ_AREA_CD AS 사업장코드
FROM B_PLANT (NOLOCK) P
WHERE P.PLANT_GUBUN_CD = ''400''   -- 레미탈(400), 레미콘(300) 등 조건 변경
  AND P.USE_YN = ''Y''
ORDER BY P.PLANT_CD'
WHERE PATTERN_SEQ IN (21, 22, 23);

-- ── SEQ 24: "B_PLANT 테이블이 공장 정보 테이블이야..." ──────────────
UPDATE PA999_FEEDBACK_PATTERN SET
    LESSON = N'B_PLANT가 공장 마스터 테이블. PLANT_GUBUN_CD로 구분: 반복제조(100), 지역공장(200), 레미콘(300), 레미탈(400). 공장 목록/유형 관련 질문은 이 테이블 사용.',
    CORRECT_SQL = N'SELECT P.PLANT_CD AS 공장코드, P.PLANT_NM AS 공장명,
       CASE P.PLANT_GUBUN_CD
           WHEN ''100'' THEN ''반복제조''
           WHEN ''200'' THEN ''지역공장''
           WHEN ''300'' THEN ''레미콘''
           WHEN ''400'' THEN ''레미탈''
           ELSE P.PLANT_GUBUN_CD
       END AS 공장유형
FROM B_PLANT (NOLOCK) P
WHERE P.USE_YN = ''Y''
ORDER BY P.PLANT_GUBUN_CD, P.PLANT_CD'
WHERE PATTERN_SEQ = 24;

-- ── SEQ 25: "PLANT_GUBUN_CD 컬럼 활용해야 해." ────────────────────
UPDATE PA999_FEEDBACK_PATTERN SET
    LESSON = N'공장 유형별 조회 시 B_PLANT.PLANT_GUBUN_CD 활용 필수. 레미탈=400.',
    CORRECT_SQL = N'SELECT P.PLANT_CD AS 공장코드, P.PLANT_NM AS 공장명, P.BIZ_AREA_CD AS 사업장코드
FROM B_PLANT (NOLOCK) P
WHERE P.PLANT_GUBUN_CD = ''400'' AND P.USE_YN = ''Y''
ORDER BY P.PLANT_CD'
WHERE PATTERN_SEQ = 25;

-- ── SEQ 41: "전기 사용량은 P_ELEC 테이블 참고" ────────────────────
UPDATE PA999_FEEDBACK_PATTERN SET
    LESSON = N'전기 사용량(KWH) 문의 시 P_ELEC_DAILY_RESULT_CKO087(일별) 또는 P_ELEC_PANEL_RESULT_CKO087(판넬별) 테이블 사용. 생산 테이블의 KWH_QTY가 아님.',
    CORRECT_SQL = N'SELECT E.PLANT_CD AS 공장코드, B.PLANT_NM AS 공장명,
       SUM(E.KWH_QTY) AS 총전력량_KWH
FROM P_ELEC_DAILY_RESULT_CKO087 (NOLOCK) E
JOIN B_PLANT (NOLOCK) B ON B.PLANT_CD = E.PLANT_CD
WHERE E.WORK_DT >= CONVERT(CHAR(6), GETDATE(), 112) + ''01''
  AND E.WORK_DT <= CONVERT(CHAR(8), GETDATE(), 112)
GROUP BY E.PLANT_CD, B.PLANT_NM
ORDER BY SUM(E.KWH_QTY) DESC'
WHERE PATTERN_SEQ = 41;

-- ── SEQ 42: "시멘트 = 반복제조 공장" ───────────────────────────────
UPDATE PA999_FEEDBACK_PATTERN SET
    LESSON = N'시멘트 품목 생산실적 = 반복제조(PLANT_GUBUN_CD=100) 공장의 P_PROD_DAILY_HDR_CKO087 테이블 사용. P_PROD_ORDER_HDR_CKO087(지역공장)은 부적합.',
    CORRECT_SQL = N'SELECT H.PLANT_CD AS 공장코드, B.PLANT_NM AS 공장명,
       H.ITEM_CD AS 품목코드, H.PROD_DT AS 생산일자,
       H.PROD_QTY AS 생산량
FROM P_PROD_DAILY_HDR_CKO087 (NOLOCK) H
JOIN B_PLANT (NOLOCK) B ON B.PLANT_CD = H.PLANT_CD AND B.PLANT_GUBUN_CD = ''100''
WHERE H.PROD_DT >= CONVERT(CHAR(6), GETDATE(), 112) + ''01''
  AND H.DAY_MAGAM_YN = ''Y''
ORDER BY H.PLANT_CD, H.PROD_DT'
WHERE PATTERN_SEQ = 42;

-- ── SEQ 35: "미마감 실적은 전체 공장 대상" ─────────────────────────
UPDATE PA999_FEEDBACK_PATTERN SET
    LESSON = N'특정 공장 지정 없이 전체 미마감 실적 문의 시, 반복제조/지역공장/레미콘/레미탈 4개 테이블 모두 UNION ALL로 조회해야 함.',
    CORRECT_SQL = N'SELECT ''반복제조'' AS 공장구분, H.PLANT_CD, COUNT(*) AS 미마감건수
FROM P_PROD_DAILY_HDR_CKO087 (NOLOCK) H WHERE H.DAY_MAGAM_YN = ''N'' GROUP BY H.PLANT_CD
UNION ALL
SELECT ''지역공장'', H.PLANT_CD, COUNT(*)
FROM P_PROD_ORDER_HDR_CKO087 (NOLOCK) H WHERE H.DAY_MAGAM_YN = ''N'' GROUP BY H.PLANT_CD
UNION ALL
SELECT ''레미콘'', H.PLANT_CD, COUNT(*)
FROM P_PROD_REMICON_HDR_CKO087 (NOLOCK) H WHERE H.DAY_MAGAM_YN = ''N'' GROUP BY H.PLANT_CD
UNION ALL
SELECT ''레미탈'', H.PLANT_CD, COUNT(*)
FROM P_PROD_REMITAL_HDR_CKO087 (NOLOCK) H WHERE H.DAY_MAGAM_YN = ''N'' GROUP BY H.PLANT_CD
ORDER BY 1, 2'
WHERE PATTERN_SEQ = 35;

-- ── SEQ 40: "작업장별 집계는 전체 공장 대상" ───────────────────────
UPDATE PA999_FEEDBACK_PATTERN SET
    LESSON = N'특정 공장 미지정 시 작업장별 생산실적 집계는 4개 생산 테이블 전체를 UNION ALL로 조회. 한 테이블만 조회하면 안 됨.',
    CORRECT_SQL = N'SELECT 공장구분, PLANT_CD AS 공장코드, WC_CD AS 작업장코드, SUM(PROD_QTY) AS 생산량합계
FROM (
    SELECT ''반복제조'' AS 공장구분, PLANT_CD, WC_CD, PROD_QTY FROM P_PROD_DAILY_HDR_CKO087 (NOLOCK) WHERE DAY_MAGAM_YN = ''Y''
    UNION ALL
    SELECT ''지역공장'', PLANT_CD, WC_CD, PROD_QTY FROM P_PROD_ORDER_HDR_CKO087 (NOLOCK) WHERE DAY_MAGAM_YN = ''Y''
    UNION ALL
    SELECT ''레미콘'', PLANT_CD, WC_CD, PROD_QTY FROM P_PROD_REMICON_HDR_CKO087 (NOLOCK) WHERE DAY_MAGAM_YN = ''Y''
    UNION ALL
    SELECT ''레미탈'', PLANT_CD, WC_CD, PROD_QTY FROM P_PROD_REMITAL_HDR_CKO087 (NOLOCK) WHERE DAY_MAGAM_YN = ''Y''
) A
GROUP BY 공장구분, PLANT_CD, WC_CD
ORDER BY 공장구분, PLANT_CD, WC_CD'
WHERE PATTERN_SEQ = 40;

-- ── SEQ 9: "WC_NM은 P_WORK_CENTER에서 조인" ──────────────────────
UPDATE PA999_FEEDBACK_PATTERN SET
    LESSON = N'작업장 명칭(WC_NM)은 생산실적 테이블에 없음. P_WORK_CENTER 테이블을 LEFT JOIN하여 WC_NM 가져올 것.',
    CORRECT_SQL = N'SELECT H.WC_CD AS 작업장코드, W.WC_NM AS 작업장명,
       SUM(H.PROD_QTY) AS 생산량합계
FROM P_PROD_DAILY_HDR_CKO087 (NOLOCK) H
LEFT JOIN P_WORK_CENTER (NOLOCK) W ON W.PLANT_CD = H.PLANT_CD AND W.WC_CD = H.WC_CD
WHERE H.PLANT_CD = ''P001''
  AND H.PROD_DT = CONVERT(CHAR(8), GETDATE(), 112)
GROUP BY H.WC_CD, W.WC_NM
ORDER BY H.WC_CD'
WHERE PATTERN_SEQ = 9;

-- ── SEQ 8: "호기별 가동시간/생산량은 P_PROD_DAILY_HDR" ─────────────
UPDATE PA999_FEEDBACK_PATTERN SET
    LESSON = N'반복제조 공장 호기별 가동시간/생산량은 P_PROD_DAILY_HDR_CKO087 테이블의 WC_CD(작업장)별 조회. WC_GROUP_CD로 RM 그룹 필터링.',
    CORRECT_SQL = N'SELECT H.WC_CD AS 호기코드, W.WC_NM AS 호기명,
       H.PROD_QTY AS 생산량, H.FAC_WORK_TIME_MM AS 가동시간_분
FROM P_PROD_DAILY_HDR_CKO087 (NOLOCK) H
LEFT JOIN P_WORK_CENTER (NOLOCK) W ON W.PLANT_CD = H.PLANT_CD AND W.WC_CD = H.WC_CD
WHERE H.PLANT_CD = ''P001''
  AND H.PROD_DT = CONVERT(CHAR(8), GETDATE(), 112)
  AND H.WC_GROUP_CD LIKE ''RM%''
ORDER BY H.WC_CD'
WHERE PATTERN_SEQ = 8;

-- ════════════════════════════════════════════════════════════════
-- 3. 행동 지침 패턴 — CORRECT_SQL을 LESSON으로 이동, SQL은 NULL 처리
-- ════════════════════════════════════════════════════════════════

-- SEQ 6: 테이블 안내 → LESSON으로
UPDATE PA999_FEEDBACK_PATTERN SET
    LESSON = N'생산 계획 테이블은 P_PROD_PLAN_CKO087 활용, 작업장 마스터는 P_WORK_CENTER 사용.',
    CORRECT_SQL = NULL
WHERE PATTERN_SEQ = 6;

-- SEQ 10: 표기 방식 지침
UPDATE PA999_FEEDBACK_PATTERN SET
    LESSON = N'가동시간 표기는 추가 요청 없는 한 원본 데이터 양식(분 단위) 그대로 표시. 임의 변환 금지.',
    CORRECT_SQL = NULL
WHERE PATTERN_SEQ = 10;

-- SEQ 11: 생산 테이블 매핑
UPDATE PA999_FEEDBACK_PATTERN SET
    LESSON = N'생산실적 조회 시 공장 구분별 테이블 사용: 반복제조=P_PROD_DAILY_HDR_CKO087, 지역공장=P_PROD_ORDER_HDR_CKO087, 레미콘=P_PROD_REMICON_HDR_CKO087, 레미탈=P_PROD_REMITAL_HDR_CKO087. 특정 공장 미지정 시 4개 모두 UNION ALL.',
    CORRECT_SQL = NULL
WHERE PATTERN_SEQ = 11;

-- SEQ 12: SQL 미생성 오류
UPDATE PA999_FEEDBACK_PATTERN SET
    LESSON = N'생산실적 + 목표 대비 달성률 문의 시, 실적은 P_PROD_DAILY_HDR_CKO087, 계획은 P_PROD_PLAN_CKO087에서 각각 조회 후 조인. SQL 생성 불가능하다고 답하지 말 것.',
    CORRECT_SQL = NULL
WHERE PATTERN_SEQ = 12;

-- SEQ 13: P_PROD_PLAN 구조 안내 → LESSON으로
UPDATE PA999_FEEDBACK_PATTERN SET
    LESSON = N'P_PROD_PLAN_CKO087 구조: YEAR(연도) + PLAN_QTY_01~12(월별 계획량). 월별 필터 시 해당 월 컬럼 사용. 생산실적 조회 시 WC_GROUP_CD를 WC_CD보다 선행 조건으로 사용.',
    CORRECT_SQL = NULL
WHERE PATTERN_SEQ = 13;

-- SEQ 20: RBAC 지침
UPDATE PA999_FEEDBACK_PATTERN SET
    LESSON = N'사용자의 소속 공장/사업장 외 데이터 요청은 거부할 것. 시스템 프롬프트의 [사용자 조직 정보]에 명시된 공장만 조회 허용.',
    CORRECT_SQL = NULL
WHERE PATTERN_SEQ = 20;

-- SEQ 29, 30: 존재하지 않는 테이블 방지
UPDATE PA999_FEEDBACK_PATTERN SET
    LESSON = N'PA999_TABLE_META에 등록되지 않은 테이블은 관련 테이블로 설정하지 말 것. 존재 여부 불확실하면 유사 테이블 안내 후 확인 요청.',
    CORRECT_SQL = NULL
WHERE PATTERN_SEQ IN (29, 30);

-- SEQ 37: 조건 부족 시 재질문
UPDATE PA999_FEEDBACK_PATTERN SET
    LESSON = N'필수 조건(공장코드, 기간 등) 없이 생산실적 조회 요청 시, SQL 생성 대신 "어느 공장/기간을 조회할까요?" 등 조건을 재확인하는 답변 출력.',
    CORRECT_SQL = NULL
WHERE PATTERN_SEQ = 37;

-- ════════════════════════════════════════════════════════════════
-- 4. 검증
-- ════════════════════════════════════════════════════════════════
SELECT PATTERN_SEQ, PRIORITY,
       LEFT(LESSON, 60) AS LESSON_PREVIEW,
       CASE
           WHEN CORRECT_SQL IS NULL THEN '[NULL - 행동지침]'
           WHEN CORRECT_SQL LIKE 'SELECT%' THEN '[SQL OK]'
           WHEN CORRECT_SQL LIKE '--%' THEN '[SQL OK]'
           WHEN CORRECT_SQL LIKE 'CASE%' THEN '[SQL OK]'
           ELSE '[확인 필요: ' + LEFT(CORRECT_SQL, 30) + ']'
       END AS SQL_STATUS
FROM PA999_FEEDBACK_PATTERN
WHERE APPLY_YN = 'Y'
ORDER BY PATTERN_SEQ;

-- 문제 없으면 COMMIT, 문제 있으면 ROLLBACK
-- COMMIT;
-- ROLLBACK;
