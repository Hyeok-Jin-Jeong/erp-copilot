-- ══════════════════════════════════════════════════════════════
-- PA999_DEMO 데모 데이터 삽입 스크립트
-- 면접관/포트폴리오 시연용 샘플 데이터
-- ══════════════════════════════════════════════════════════════

USE PA999_DEMO;
GO

-- ──────────────────────────────────────────────────────────────
-- ■ B_PLANT (공장 마스터 — 레미콘 10개 + 시멘트 2개)
-- ──────────────────────────────────────────────────────────────
DELETE FROM B_PLANT;

INSERT INTO B_PLANT (PLANT_CD, PLANT_NM, PLANT_TYPE, BIZ_AREA_CD, REGION_NM, USE_YN) VALUES
('P001', '단양공장',   'CEMENT',  'BA01', '충북', 'Y'),
('P003', '대구공장',   'REMICON', 'BA02', '대구', 'Y'),
('P004', '부산공장',   'REMICON', 'BA02', '부산', 'Y'),
('P006', '청주공장',   'REMICON', 'BA01', '충북', 'Y'),
('P007', '성남공장',   'REMICON', 'BA03', '경기', 'Y'),
('P008', '대전공장',   'REMICON', 'BA01', '대전', 'Y'),
('P011', '김해공장',   'REMICON', 'BA02', '경남', 'Y'),
('P014', '서대구공장', 'REMICON', 'BA02', '대구', 'Y'),
('P020', '서인천공장', 'REMICON', 'BA03', '인천', 'Y'),
('P022', '화성공장',   'REMICON', 'BA03', '경기', 'Y'),
('P025', '부천공장',   'REMICON', 'BA03', '경기', 'Y'),
('P031', '영월공장',   'CEMENT',  'BA01', '강원', 'Y');
PRINT 'B_PLANT 삽입 완료: ' + CAST(@@ROWCOUNT AS VARCHAR) + '건';
GO

-- ──────────────────────────────────────────────────────────────
-- ■ B_ITEM (품목 마스터)
-- ──────────────────────────────────────────────────────────────
DELETE FROM B_ITEM;

INSERT INTO B_ITEM (ITEM_CD, ITEM_NM, ITEM_TYPE_CD, UNIT, USE_YN) VALUES
('RC2500', '레미콘 25-21-150',  'REMICON', 'M3', 'Y'),
('RC3000', '레미콘 30-24-150',  'REMICON', 'M3', 'Y'),
('RC3500', '레미콘 35-27-120',  'REMICON', 'M3', 'Y'),
('CM0001', '시멘트(포틀랜드)',   'CEMENT',  'TON', 'Y'),
('CM0002', '고로슬래그시멘트',   'CEMENT',  'TON', 'Y'),
('RT0001', '레미탈(바닥용)',    'REMITAL', 'TON', 'Y');
PRINT 'B_ITEM 삽입 완료: ' + CAST(@@ROWCOUNT AS VARCHAR) + '건';
GO

-- ──────────────────────────────────────────────────────────────
-- ■ A_ACCT (계정과목)
-- ──────────────────────────────────────────────────────────────
DELETE FROM A_ACCT;

INSERT INTO A_ACCT (ACCT_CD, ACCT_NM, ACCT_TYPE_CD, USE_YN) VALUES
('4001', '매출액',     'SALES',  'Y'),
('5001', '재료비',     'COST',   'Y'),
('5002', '노무비',     'COST',   'Y'),
('5003', '제조경비',   'COST',   'Y'),
('5100', '제조원가',   'COST',   'Y'),
('6001', '판매관리비', 'SGA',    'Y');
PRINT 'A_ACCT 삽입 완료: ' + CAST(@@ROWCOUNT AS VARCHAR) + '건';
GO

-- ──────────────────────────────────────────────────────────────
-- ■ P_PROD_DAILY_HDR (2026년 3월 일별 생산 실적)
-- ──────────────────────────────────────────────────────────────
DELETE FROM P_PROD_DAILY_HDR;

-- 단양공장 (P001) — 시멘트 생산 31일치
DECLARE @dt INT = 1;
WHILE @dt <= 31
BEGIN
    INSERT INTO P_PROD_DAILY_HDR (PLANT_CD, PROD_DT, PROD_QTY, PROD_TYPE, ITEM_CD)
    VALUES (
        'P001',
        '202603' + RIGHT('0' + CAST(@dt AS VARCHAR), 2),
        CAST(4500 + (ABS(CHECKSUM(NEWID())) % 1000) AS DECIMAL(18,2)),
        '반복제조', 'CM0001'
    );
    SET @dt = @dt + 1;
END

-- 영월공장 (P031) — 시멘트 생산 31일치
SET @dt = 1;
WHILE @dt <= 31
BEGIN
    INSERT INTO P_PROD_DAILY_HDR (PLANT_CD, PROD_DT, PROD_QTY, PROD_TYPE, ITEM_CD)
    VALUES (
        'P031',
        '202603' + RIGHT('0' + CAST(@dt AS VARCHAR), 2),
        CAST(3800 + (ABS(CHECKSUM(NEWID())) % 800) AS DECIMAL(18,2)),
        '반복제조', 'CM0001'
    );
    SET @dt = @dt + 1;
END

-- 레미콘 공장들 (P003~P025) — 3월 실적
DECLARE @plants TABLE (PLANT_CD NVARCHAR(10), BASE_QTY INT);
INSERT INTO @plants VALUES
('P003', 3200), ('P004', 3800), ('P006', 2900),
('P007', 4100), ('P008', 3300), ('P011', 2800),
('P014', 3100), ('P020', 3600), ('P022', 4200), ('P025', 3000);

DECLARE @p_cd NVARCHAR(10), @base INT;
DECLARE plant_cur CURSOR FOR SELECT PLANT_CD, BASE_QTY FROM @plants;
OPEN plant_cur;
FETCH NEXT FROM plant_cur INTO @p_cd, @base;

WHILE @@FETCH_STATUS = 0
BEGIN
    SET @dt = 1;
    WHILE @dt <= 31
    BEGIN
        -- 주말(토/일)은 생산량 감소
        DECLARE @dow INT = DATEPART(WEEKDAY, CAST('202603' + RIGHT('0' + CAST(@dt AS VARCHAR), 2) AS DATE));
        DECLARE @qty DECIMAL(18,2) =
            CASE WHEN @dow IN (1, 7)
                 THEN CAST(@base * 0.3 + (ABS(CHECKSUM(NEWID())) % 200) AS DECIMAL(18,2))
                 ELSE CAST(@base + (ABS(CHECKSUM(NEWID())) % 500) - 250 AS DECIMAL(18,2))
            END;

        INSERT INTO P_PROD_DAILY_HDR (PLANT_CD, PROD_DT, PROD_QTY, PROD_TYPE, ITEM_CD)
        VALUES (@p_cd, '202603' + RIGHT('0' + CAST(@dt AS VARCHAR), 2), @qty, '반복제조', 'RC2500');

        SET @dt = @dt + 1;
    END
    FETCH NEXT FROM plant_cur INTO @p_cd, @base;
END

CLOSE plant_cur;
DEALLOCATE plant_cur;

PRINT 'P_PROD_DAILY_HDR 삽입 완료: ' + CAST((SELECT COUNT(*) FROM P_PROD_DAILY_HDR) AS VARCHAR) + '건';
GO

-- ──────────────────────────────────────────────────────────────
-- ■ PA999_TABLE_META (AI 테이블 식별용 메타데이터)
-- ──────────────────────────────────────────────────────────────
DELETE FROM PA999_TABLE_META;

INSERT INTO PA999_TABLE_META (TABLE_NM, TABLE_DESC, KEYWORD_LIST, USE_YN, SRC_TYPE) VALUES
('B_PLANT',             '공장 마스터 — 레미콘/시멘트/레미탈 공장 목록, 지역, 사업영역', '공장,plant,공장코드,공장명,지역,레미콘,시멘트', 'Y', 'M'),
('B_ITEM',              '품목 마스터 — 생산/판매 품목 목록, 단위', '품목,item,품목코드,품목명,단위,레미콘,시멘트', 'Y', 'M'),
('A_ACCT',              '계정과목 마스터 — 매출/원가/비용 계정', '계정,account,계정과목,매출,원가,비용', 'Y', 'M'),
('P_PROD_DAILY_HDR',    '일별 생산 실적 헤더 — 공장별/일별 생산량, 반복제조/주문제조 구분', '생산,production,생산량,생산실적,일별생산,반복제조,주문제조,생산일보', 'Y', 'M'),
('PA999_CHAT_LOG',      'AI 챗봇 질의 로그 — 사용자 질문, AI 응답, 생성 SQL, 평가 점수', '채팅로그,질의,응답,피드백', 'Y', 'M'),
('PA999_FEEDBACK_PATTERN', '피드백 교정 패턴 — AI SQL 오류 교정 이력, RAG 임베딩', '피드백,패턴,교정,임베딩', 'Y', 'M'),
('PA999_TABLE_META',    'AI 테이블 메타 — 테이블 설명, 키워드 목록 (Step1 테이블 식별용)', '테이블메타,메타데이터', 'Y', 'M');

PRINT 'PA999_TABLE_META 삽입 완료: ' + CAST(@@ROWCOUNT AS VARCHAR) + '건';
GO

-- ──────────────────────────────────────────────────────────────
-- ■ PA999_FEEDBACK_PATTERN (샘플 교정 패턴)
-- ──────────────────────────────────────────────────────────────
DELETE FROM PA999_FEEDBACK_PATTERN;

INSERT INTO PA999_FEEDBACK_PATTERN
    (QUERY_PATTERN, WRONG_APPROACH, CORRECT_SQL, LESSON, PRIORITY, APPLY_YN, PREFERRED_MODE) VALUES
(
    '공장명으로 조회 시 서브쿼리 남용',
    'WHERE PLANT_CD = (SELECT PLANT_CD FROM B_PLANT WHERE PLANT_NM LIKE ''%단양%'') 형태로 서브쿼리 사용',
    'SELECT h.* FROM P_PROD_DAILY_HDR h WITH(NOLOCK)
JOIN B_PLANT p WITH(NOLOCK) ON p.PLANT_CD = h.PLANT_CD
WHERE p.PLANT_NM LIKE ''%단양%''',
    'JOIN을 사용하고 서브쿼리보다 명시적인 조건을 선호할 것',
    8, 'Y', 'SQL'
),
(
    '생산량 합계 조회',
    'SELECT SUM 없이 단순 SELECT PROD_QTY',
    'SELECT PLANT_CD, SUM(PROD_QTY) AS TOTAL_QTY
FROM P_PROD_DAILY_HDR WITH(NOLOCK)
WHERE PROD_DT BETWEEN ''20260301'' AND ''20260331''
GROUP BY PLANT_CD
ORDER BY TOTAL_QTY DESC',
    '생산량 집계는 SUM + GROUP BY를 사용. 기간 조건은 PROD_DT(CHAR8) 범위로.',
    7, 'Y', 'SQL'
),
(
    '레미콘 공장 목록 조회',
    'B_PLANT WHERE PLANT_NM LIKE 레미콘%',
    'SELECT PLANT_CD, PLANT_NM, REGION_NM
FROM B_PLANT WITH(NOLOCK)
WHERE PLANT_TYPE = ''REMICON'' AND USE_YN = ''Y''
ORDER BY PLANT_CD',
    '공장 유형 필터는 PLANT_TYPE 컬럼 사용. PLANT_NM 패턴 검색은 부정확함.',
    6, 'Y', 'SQL'
);

PRINT 'PA999_FEEDBACK_PATTERN 삽입 완료: ' + CAST(@@ROWCOUNT AS VARCHAR) + '건';
GO

-- ──────────────────────────────────────────────────────────────
-- ■ 검증 쿼리
-- ──────────────────────────────────────────────────────────────
SELECT '=== 테이블 행 수 확인 ===' AS INFO;

SELECT 'B_PLANT'               AS TBL, COUNT(*) AS CNT FROM B_PLANT            UNION ALL
SELECT 'B_ITEM'                AS TBL, COUNT(*) AS CNT FROM B_ITEM             UNION ALL
SELECT 'A_ACCT'                AS TBL, COUNT(*) AS CNT FROM A_ACCT             UNION ALL
SELECT 'P_PROD_DAILY_HDR'      AS TBL, COUNT(*) AS CNT FROM P_PROD_DAILY_HDR   UNION ALL
SELECT 'PA999_TABLE_META'      AS TBL, COUNT(*) AS CNT FROM PA999_TABLE_META   UNION ALL
SELECT 'PA999_FEEDBACK_PATTERN' AS TBL, COUNT(*) AS CNT FROM PA999_FEEDBACK_PATTERN;
GO
