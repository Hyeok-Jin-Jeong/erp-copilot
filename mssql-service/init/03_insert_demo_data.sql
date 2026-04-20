USE PA999_DEMO;

-- B_PLANT
IF NOT EXISTS (SELECT 1 FROM B_PLANT WHERE PLANT_CD = 'P001')
BEGIN
    INSERT INTO B_PLANT (PLANT_CD, PLANT_NM, PLANT_TYPE, BIZ_AREA_CD, REGION_NM, USE_YN) VALUES
    ('P001', N'단양공장',   'CEMENT',  'BA01', N'충북', 'Y'),
    ('P003', N'대구공장',   'REMICON', 'BA02', N'대구', 'Y'),
    ('P004', N'부산공장',   'REMICON', 'BA02', N'부산', 'Y'),
    ('P006', N'청주공장',   'REMICON', 'BA01', N'충북', 'Y'),
    ('P007', N'성남공장',   'REMICON', 'BA03', N'경기', 'Y'),
    ('P008', N'대전공장',   'REMICON', 'BA01', N'대전', 'Y'),
    ('P011', N'김해공장',   'REMICON', 'BA02', N'경남', 'Y'),
    ('P014', N'서대구공장', 'REMICON', 'BA02', N'대구', 'Y'),
    ('P020', N'서인천공장', 'REMICON', 'BA03', N'인천', 'Y'),
    ('P022', N'화성공장',   'REMICON', 'BA03', N'경기', 'Y'),
    ('P025', N'부천공장',   'REMICON', 'BA03', N'경기', 'Y'),
    ('P031', N'영월공장',   'CEMENT',  'BA01', N'강원', 'Y');
    PRINT 'B_PLANT 삽입 완료';
END

-- B_ITEM
IF NOT EXISTS (SELECT 1 FROM B_ITEM WHERE ITEM_CD = 'RC2500')
BEGIN
    INSERT INTO B_ITEM (ITEM_CD, ITEM_NM, ITEM_TYPE_CD, UNIT, USE_YN) VALUES
    ('RC2500', N'레미콘 25-21-150', 'REMICON', 'M3',  'Y'),
    ('RC3000', N'레미콘 30-24-150', 'REMICON', 'M3',  'Y'),
    ('RC3500', N'레미콘 35-27-120', 'REMICON', 'M3',  'Y'),
    ('CM0001', N'시멘트(포틀랜드)', 'CEMENT',  'TON', 'Y'),
    ('CM0002', N'고로슬래그시멘트', 'CEMENT',  'TON', 'Y'),
    ('RT0001', N'레미탈(바닥용)',   'REMITAL', 'TON', 'Y');
    PRINT 'B_ITEM 삽입 완료';
END

-- A_ACCT
IF NOT EXISTS (SELECT 1 FROM A_ACCT WHERE ACCT_CD = '4001')
BEGIN
    INSERT INTO A_ACCT (ACCT_CD, ACCT_NM, ACCT_TYPE_CD, USE_YN) VALUES
    ('4001', N'매출액',     'SALES', 'Y'),
    ('5001', N'재료비',     'COST',  'Y'),
    ('5002', N'노무비',     'COST',  'Y'),
    ('5003', N'제조경비',   'COST',  'Y'),
    ('5100', N'제조원가',   'COST',  'Y'),
    ('6001', N'판매관리비', 'SGA',   'Y');
    PRINT 'A_ACCT 삽입 완료';
END

-- P_PROD_DAILY_HDR
IF NOT EXISTS (SELECT 1 FROM P_PROD_DAILY_HDR WHERE PLANT_CD = 'P001')
BEGIN
    DECLARE @dt INT = 1;

    WHILE @dt <= 31
    BEGIN
        INSERT INTO P_PROD_DAILY_HDR (PLANT_CD, PROD_DT, PROD_QTY, PROD_TYPE, ITEM_CD)
        VALUES ('P001',
                '202603' + RIGHT('0' + CAST(@dt AS VARCHAR), 2),
                CAST(4500 + (ABS(CHECKSUM(NEWID())) % 1000) AS DECIMAL(18,2)),
                N'반복제조', 'CM0001');
        SET @dt = @dt + 1;
    END

    SET @dt = 1;
    WHILE @dt <= 31
    BEGIN
        INSERT INTO P_PROD_DAILY_HDR (PLANT_CD, PROD_DT, PROD_QTY, PROD_TYPE, ITEM_CD)
        VALUES ('P031',
                '202603' + RIGHT('0' + CAST(@dt AS VARCHAR), 2),
                CAST(3800 + (ABS(CHECKSUM(NEWID())) % 800) AS DECIMAL(18,2)),
                N'반복제조', 'CM0001');
        SET @dt = @dt + 1;
    END

    DECLARE @plants TABLE (PLANT_CD NVARCHAR(10), BASE_QTY INT, ITEM_CD NVARCHAR(20));
    INSERT INTO @plants VALUES
    ('P003',3200,'RC2500'),('P004',3800,'RC2500'),('P006',2900,'RC3000'),
    ('P007',4100,'RC2500'),('P008',3300,'RC3000'),('P011',2800,'RC3500'),
    ('P014',3100,'RC2500'),('P020',3600,'RC3000'),('P022',4200,'RC2500'),('P025',3000,'RC3500');

    DECLARE @p_cd NVARCHAR(10), @base INT, @itm NVARCHAR(20);
    DECLARE plant_cur CURSOR FOR SELECT PLANT_CD, BASE_QTY, ITEM_CD FROM @plants;
    OPEN plant_cur;
    FETCH NEXT FROM plant_cur INTO @p_cd, @base, @itm;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @dt = 1;
        WHILE @dt <= 31
        BEGIN
            DECLARE @dow INT = DATEPART(WEEKDAY,
                CAST('202603' + RIGHT('0' + CAST(@dt AS VARCHAR), 2) AS DATE));
            DECLARE @qty DECIMAL(18,2) =
                CASE WHEN @dow IN (1,7)
                     THEN CAST(@base * 0.3 + (ABS(CHECKSUM(NEWID())) % 200) AS DECIMAL(18,2))
                     ELSE CAST(@base + (ABS(CHECKSUM(NEWID())) % 500) - 250   AS DECIMAL(18,2))
                END;
            INSERT INTO P_PROD_DAILY_HDR (PLANT_CD, PROD_DT, PROD_QTY, PROD_TYPE, ITEM_CD)
            VALUES (@p_cd,
                    '202603' + RIGHT('0' + CAST(@dt AS VARCHAR), 2),
                    @qty, N'반복제조', @itm);
            SET @dt = @dt + 1;
        END
        FETCH NEXT FROM plant_cur INTO @p_cd, @base, @itm;
    END
    CLOSE plant_cur;
    DEALLOCATE plant_cur;

    PRINT 'P_PROD_DAILY_HDR 삽입 완료: '
          + CAST((SELECT COUNT(*) FROM P_PROD_DAILY_HDR) AS VARCHAR) + '건';
END

-- PA999_TABLE_META
IF NOT EXISTS (SELECT 1 FROM PA999_TABLE_META WHERE TABLE_NM = 'B_PLANT')
BEGIN
    INSERT INTO PA999_TABLE_META (TABLE_NM, TABLE_DESC, KEYWORD_LIST, USE_YN, SRC_TYPE) VALUES
    ('B_PLANT',               N'공장 마스터 — 레미콘/시멘트/레미탈 공장 목록, 지역, 사업영역',
                               N'공장,plant,공장코드,공장명,지역,레미콘,시멘트','Y','M'),
    ('B_ITEM',                N'품목 마스터 — 생산/판매 품목 목록, 단위',
                               N'품목,item,품목코드,품목명,단위,레미콘,시멘트','Y','M'),
    ('A_ACCT',                N'계정과목 마스터 — 매출/원가/비용 계정',
                               N'계정,account,계정과목,매출,원가,비용','Y','M'),
    ('P_PROD_DAILY_HDR',      N'일별 생산 실적 헤더 — 공장별/일별 생산량',
                               N'생산,production,생산량,생산실적,일별생산,반복제조','Y','M'),
    ('PA999_CHAT_LOG',         N'AI 챗봇 질의 로그 — 사용자 질문, AI 응답, 생성 SQL',
                               N'채팅로그,질의,응답,피드백','Y','M'),
    ('PA999_FEEDBACK_PATTERN', N'피드백 교정 패턴 — AI SQL 오류 교정 이력, RAG 임베딩',
                               N'피드백,패턴,교정,임베딩','Y','M'),
    ('PA999_TABLE_META',       N'AI 테이블 메타 — 테이블 설명, 키워드 목록',
                               N'테이블메타,메타데이터','Y','M');
    PRINT 'PA999_TABLE_META 삽입 완료';
END

-- PA999_FEEDBACK_PATTERN
IF NOT EXISTS (SELECT 1 FROM PA999_FEEDBACK_PATTERN WHERE QUERY_PATTERN LIKE N'%공장명%')
BEGIN
    INSERT INTO PA999_FEEDBACK_PATTERN
        (QUERY_PATTERN, WRONG_APPROACH, CORRECT_SQL, LESSON, PRIORITY, APPLY_YN, PREFERRED_MODE)
    VALUES
    (N'공장명으로 조회 시 서브쿼리 남용',
     N'WHERE PLANT_CD = (SELECT PLANT_CD FROM B_PLANT WHERE PLANT_NM LIKE ...) 형태',
     N'SELECT h.* FROM P_PROD_DAILY_HDR h WITH(NOLOCK) JOIN B_PLANT p WITH(NOLOCK) ON p.PLANT_CD = h.PLANT_CD WHERE p.PLANT_NM LIKE N''%단양%''',
     N'JOIN을 사용하고 서브쿼리보다 명시적인 조건을 선호', 8,'Y','SQL'),
    (N'생산량 합계 조회',
     N'SELECT SUM 없이 단순 SELECT PROD_QTY',
     N'SELECT PLANT_CD, SUM(PROD_QTY) AS TOTAL_QTY FROM P_PROD_DAILY_HDR WITH(NOLOCK) WHERE PROD_DT BETWEEN ''20260301'' AND ''20260331'' GROUP BY PLANT_CD ORDER BY TOTAL_QTY DESC',
     N'생산량 집계는 SUM + GROUP BY. 기간 조건은 PROD_DT(CHAR8) 범위로.', 7,'Y','SQL'),
    (N'레미콘 공장 목록 조회',
     N'PLANT_NM LIKE 레미콘% — 이름 패턴 검색은 부정확',
     N'SELECT PLANT_CD, PLANT_NM, REGION_NM FROM B_PLANT WITH(NOLOCK) WHERE PLANT_TYPE = ''REMICON'' AND USE_YN = ''Y'' ORDER BY PLANT_CD',
     N'공장 유형 필터는 PLANT_TYPE 컬럼 사용', 6,'Y','SQL');
    PRINT 'PA999_FEEDBACK_PATTERN 삽입 완료';
END

PRINT '=== 데모 데이터 초기화 완료 ===';
SELECT TABLE_NAME AS TBL FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_TYPE = 'BASE TABLE' ORDER BY TABLE_NAME;
