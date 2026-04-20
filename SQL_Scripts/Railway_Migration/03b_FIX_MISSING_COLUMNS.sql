-- ══════════════════════════════════════════════════════════════
-- 누락 컬럼 추가 픽스 스크립트
-- 기존 테이블에 컬럼이 없을 때 ALTER TABLE로 추가
-- ══════════════════════════════════════════════════════════════

USE PA999_DEMO;
GO

-- ── B_PLANT 누락 컬럼 추가 ────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='B_PLANT' AND COLUMN_NAME='PLANT_TYPE')
    ALTER TABLE B_PLANT ADD PLANT_TYPE NVARCHAR(20) NULL;

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='B_PLANT' AND COLUMN_NAME='BIZ_AREA_CD')
    ALTER TABLE B_PLANT ADD BIZ_AREA_CD NVARCHAR(10) NULL;

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='B_PLANT' AND COLUMN_NAME='REGION_NM')
    ALTER TABLE B_PLANT ADD REGION_NM NVARCHAR(50) NULL;

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='B_PLANT' AND COLUMN_NAME='USE_YN')
    ALTER TABLE B_PLANT ADD USE_YN CHAR(1) NOT NULL DEFAULT 'Y';

PRINT 'B_PLANT 컬럼 추가 완료';
GO

-- ── B_ITEM 누락 컬럼 추가 ─────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='B_ITEM' AND COLUMN_NAME='ITEM_TYPE_CD')
    ALTER TABLE B_ITEM ADD ITEM_TYPE_CD NVARCHAR(10) NULL;

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='B_ITEM' AND COLUMN_NAME='UNIT')
    ALTER TABLE B_ITEM ADD UNIT NVARCHAR(10) NULL;

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='B_ITEM' AND COLUMN_NAME='USE_YN')
    ALTER TABLE B_ITEM ADD USE_YN CHAR(1) NOT NULL DEFAULT 'Y';

PRINT 'B_ITEM 컬럼 추가 완료';
GO

-- ── PA999_TABLE_META 누락 컬럼 추가 ──────────────────────────
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='PA999_TABLE_META' AND COLUMN_NAME='SRC_TYPE')
    ALTER TABLE PA999_TABLE_META ADD SRC_TYPE CHAR(1) NOT NULL DEFAULT 'A';

PRINT 'PA999_TABLE_META 컬럼 추가 완료';
GO

-- ── B_PLANT 데이터 재삽입 ──────────────────────────────────────
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
PRINT 'B_PLANT 재삽입: ' + CAST(@@ROWCOUNT AS VARCHAR) + '건';
GO

-- ── B_ITEM 데이터 재삽입 ───────────────────────────────────────
DELETE FROM B_ITEM;

INSERT INTO B_ITEM (ITEM_CD, ITEM_NM, ITEM_TYPE_CD, UNIT, USE_YN) VALUES
('RC2500', '레미콘 25-21-150', 'REMICON', 'M3',  'Y'),
('RC3000', '레미콘 30-24-150', 'REMICON', 'M3',  'Y'),
('RC3500', '레미콘 35-27-120', 'REMICON', 'M3',  'Y'),
('CM0001', '시멘트(포틀랜드)', 'CEMENT',  'TON', 'Y'),
('CM0002', '고로슬래그시멘트', 'CEMENT',  'TON', 'Y'),
('RT0001', '레미탈(바닥용)',   'REMITAL', 'TON', 'Y');
PRINT 'B_ITEM 재삽입: ' + CAST(@@ROWCOUNT AS VARCHAR) + '건';
GO

-- ── PA999_TABLE_META 재삽입 ───────────────────────────────────
DELETE FROM PA999_TABLE_META;

INSERT INTO PA999_TABLE_META (TABLE_NM, TABLE_DESC, KEYWORD_LIST, USE_YN, SRC_TYPE) VALUES
('B_PLANT',              '공장 마스터 — 레미콘/시멘트/레미탈 공장 목록, 지역, 사업영역', '공장,plant,공장코드,공장명,지역,레미콘,시멘트', 'Y', 'M'),
('B_ITEM',               '품목 마스터 — 생산/판매 품목 목록, 단위', '품목,item,품목코드,품목명,단위,레미콘,시멘트', 'Y', 'M'),
('A_ACCT',               '계정과목 마스터 — 매출/원가/비용 계정', '계정,account,계정과목,매출,원가,비용', 'Y', 'M'),
('P_PROD_DAILY_HDR',     '일별 생산 실적 헤더 — 공장별/일별 생산량, 반복제조/주문제조 구분', '생산,production,생산량,생산실적,일별생산,반복제조,주문제조,생산일보', 'Y', 'M'),
('PA999_CHAT_LOG',       'AI 챗봇 질의 로그 — 사용자 질문, AI 응답, 생성 SQL, 평가 점수', '채팅로그,질의,응답,피드백', 'Y', 'M'),
('PA999_FEEDBACK_PATTERN','피드백 교정 패턴 — AI SQL 오류 교정 이력, RAG 임베딩', '피드백,패턴,교정,임베딩', 'Y', 'M'),
('PA999_TABLE_META',     'AI 테이블 메타 — 테이블 설명, 키워드 목록 (Step1 테이블 식별용)', '테이블메타,메타데이터', 'Y', 'M');
PRINT 'PA999_TABLE_META 재삽입: ' + CAST(@@ROWCOUNT AS VARCHAR) + '건';
GO

-- ── P_PROD_DAILY_HDR 생산 실적 재삽입 ────────────────────────
DELETE FROM P_PROD_DAILY_HDR;

-- 단양공장(P001) 2026년 3월 시멘트 생산
DECLARE @dt INT = 1;
WHILE @dt <= 31
BEGIN
    INSERT INTO P_PROD_DAILY_HDR (PLANT_CD, PROD_DT, PROD_QTY, PROD_TYPE, ITEM_CD)
    VALUES ('P001', '202603' + RIGHT('0'+CAST(@dt AS VARCHAR),2),
            CAST(4500+(ABS(CHECKSUM(NEWID()))%1000) AS DECIMAL(18,2)), '반복제조', 'CM0001');
    SET @dt += 1;
END

-- 영월공장(P031) 2026년 3월 시멘트 생산
SET @dt = 1;
WHILE @dt <= 31
BEGIN
    INSERT INTO P_PROD_DAILY_HDR (PLANT_CD, PROD_DT, PROD_QTY, PROD_TYPE, ITEM_CD)
    VALUES ('P031', '202603' + RIGHT('0'+CAST(@dt AS VARCHAR),2),
            CAST(3800+(ABS(CHECKSUM(NEWID()))%800) AS DECIMAL(18,2)), '반복제조', 'CM0001');
    SET @dt += 1;
END

-- 레미콘 공장 10개 2026년 3월 생산
DECLARE @plants TABLE (PC NVARCHAR(10), BQ INT);
INSERT INTO @plants VALUES
('P003',3200),('P004',3800),('P006',2900),('P007',4100),('P008',3300),
('P011',2800),('P014',3100),('P020',3600),('P022',4200),('P025',3000);

DECLARE @pc NVARCHAR(10), @bq INT;
DECLARE c CURSOR FOR SELECT PC, BQ FROM @plants;
OPEN c; FETCH NEXT FROM c INTO @pc, @bq;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @dt = 1;
    WHILE @dt <= 31
    BEGIN
        DECLARE @dow INT = DATEPART(WEEKDAY,
            CAST('202603'+RIGHT('0'+CAST(@dt AS VARCHAR),2) AS DATE));
        DECLARE @qty DECIMAL(18,2) =
            CASE WHEN @dow IN (1,7)
                 THEN CAST(@bq*0.3+(ABS(CHECKSUM(NEWID()))%200) AS DECIMAL(18,2))
                 ELSE CAST(@bq+(ABS(CHECKSUM(NEWID()))%500)-250  AS DECIMAL(18,2))
            END;
        INSERT INTO P_PROD_DAILY_HDR (PLANT_CD, PROD_DT, PROD_QTY, PROD_TYPE, ITEM_CD)
        VALUES (@pc, '202603'+RIGHT('0'+CAST(@dt AS VARCHAR),2), @qty, '반복제조','RC2500');
        SET @dt += 1;
    END
    FETCH NEXT FROM c INTO @pc, @bq;
END
CLOSE c; DEALLOCATE c;

DECLARE @total INT = (SELECT COUNT(*) FROM P_PROD_DAILY_HDR);
PRINT 'P_PROD_DAILY_HDR 재삽입: ' + CAST(@total AS VARCHAR) + '건';
GO

-- ── 최종 검증 ─────────────────────────────────────────────────
SELECT TABLE_NM = 'B_PLANT',              CNT = COUNT(*) FROM B_PLANT            UNION ALL
SELECT TABLE_NM = 'B_ITEM',               CNT = COUNT(*) FROM B_ITEM             UNION ALL
SELECT TABLE_NM = 'A_ACCT',               CNT = COUNT(*) FROM A_ACCT             UNION ALL
SELECT TABLE_NM = 'P_PROD_DAILY_HDR',     CNT = COUNT(*) FROM P_PROD_DAILY_HDR   UNION ALL
SELECT TABLE_NM = 'PA999_TABLE_META',     CNT = COUNT(*) FROM PA999_TABLE_META   UNION ALL
SELECT TABLE_NM = 'PA999_FEEDBACK_PATTERN', CNT = COUNT(*) FROM PA999_FEEDBACK_PATTERN;
GO
