-- ══════════════════════════════════════════════════════════════════
-- 07. 현실화 샘플 데이터 삽입 (2026년 3월 전체 + 4월 1일~오늘)
--
-- 생산량 기준:
--   단양공장(P001) : 월 90,000 TON  → 평일 ~4,090 TON/일 (주말 0)
--   영월공장(P031) : 월 60,000 TON  → 평일 ~2,727 TON/일 (주말 0)
--   레미콘 공장    : 월 5,000~15,000 M3 (주말 0)
--   레미탈 공장    : P050 월 6,600 TON / P051 월 5,500 TON (주말 0)
--
-- 주말 판정: DATEPART(WEEKDAY, date) IN (1=일, 7=토)
-- 작성일: 2026-04-20
-- ══════════════════════════════════════════════════════════════════

USE PA999_DEMO;
GO

-- ──────────────────────────────────────────────────────────────────
-- ① P_WORK_CENTER — 공장별 작업장 마스터
-- ──────────────────────────────────────────────────────────────────
DELETE FROM P_WORK_CENTER;

INSERT INTO P_WORK_CENTER (PLANT_CD, WC_CD, WC_NM, WC_GROUP_CD, WC_GROUP_NM, USE_YN)
VALUES
-- 단양공장(P001) 3개 호기
('P001','RM1','1호기',      'RM','소성/원료','Y'),
('P001','RM2','2호기',      'RM','소성/원료','Y'),
('P001','RM3','3호기',      'RM','소성/원료','Y'),
-- 영월공장(P031) 2개 호기
('P031','RM1','1호기',      'RM','소성/원료','Y'),
('P031','RM2','2호기',      'RM','소성/원료','Y'),
-- 레미콘 공장 (각 1개 믹서)
('P003','RC1','믹서1호기',  'RC','레미콘믹서','Y'),
('P004','RC1','믹서1호기',  'RC','레미콘믹서','Y'),
('P006','RC1','믹서1호기',  'RC','레미콘믹서','Y'),
('P007','RC1','믹서1호기',  'RC','레미콘믹서','Y'),
('P007','RC2','믹서2호기',  'RC','레미콘믹서','Y'),
('P008','RC1','믹서1호기',  'RC','레미콘믹서','Y'),
('P011','RC1','믹서1호기',  'RC','레미콘믹서','Y'),
('P014','RC1','믹서1호기',  'RC','레미콘믹서','Y'),
('P020','RC1','믹서1호기',  'RC','레미콘믹서','Y'),
('P020','RC2','믹서2호기',  'RC','레미콘믹서','Y'),
('P022','RC1','믹서1호기',  'RC','레미콘믹서','Y'),
('P022','RC2','믹서2호기',  'RC','레미콘믹서','Y'),
('P025','RC1','믹서1호기',  'RC','레미콘믹서','Y'),
-- 레미탈 공장
('P050','RT1','분쇄1호기',  'RT','레미탈분쇄','Y'),
('P051','RT1','분쇄1호기',  'RT','레미탈분쇄','Y');

PRINT 'P_WORK_CENTER 삽입 완료: ' + CAST(@@ROWCOUNT AS VARCHAR) + '건';
GO

-- ──────────────────────────────────────────────────────────────────
-- ② P_PROD_DAILY_HDR (기존 테이블) — 현실화 재삽입
--    단양/영월: 주말 0, 평일 현실적 생산량
--    레미콘: 주말 0
-- ──────────────────────────────────────────────────────────────────
DELETE FROM P_PROD_DAILY_HDR;

-- 단양(P001): 평일 4,090 TON/day (±300), 주말 0
DECLARE @dt INT = 1;
WHILE @dt <= 31
BEGIN
    DECLARE @dateStr CHAR(8) = '202603' + RIGHT('0' + CAST(@dt AS VARCHAR), 2);
    DECLARE @dow INT = DATEPART(WEEKDAY, CAST(@dateStr AS DATE));
    DECLARE @qty DECIMAL(18,2) =
        CASE WHEN @dow IN (1,7) THEN 0
             ELSE CAST(4090 + (ABS(CHECKSUM(NEWID())) % 601) - 300 AS DECIMAL(18,2))
        END;
    IF @qty > 0
        INSERT INTO P_PROD_DAILY_HDR (PLANT_CD, PROD_DT, PROD_QTY, PROD_TYPE, ITEM_CD)
        VALUES ('P001', @dateStr, @qty, '반복제조', 'CM0001');
    SET @dt += 1;
END

-- 영월(P031): 평일 2,727 TON/day (±200), 주말 0
SET @dt = 1;
WHILE @dt <= 31
BEGIN
    DECLARE @dateStr2 CHAR(8) = '202603' + RIGHT('0' + CAST(@dt AS VARCHAR), 2);
    DECLARE @dow2 INT = DATEPART(WEEKDAY, CAST(@dateStr2 AS DATE));
    DECLARE @qty2 DECIMAL(18,2) =
        CASE WHEN @dow2 IN (1,7) THEN 0
             ELSE CAST(2727 + (ABS(CHECKSUM(NEWID())) % 401) - 200 AS DECIMAL(18,2))
        END;
    IF @qty2 > 0
        INSERT INTO P_PROD_DAILY_HDR (PLANT_CD, PROD_DT, PROD_QTY, PROD_TYPE, ITEM_CD)
        VALUES ('P031', @dateStr2, @qty2, '반복제조', 'CM0001');
    SET @dt += 1;
END

-- 레미콘 공장별 기준 생산량 (평일, 주말 0)
DECLARE @rc_plants TABLE (PLANT_CD NVARCHAR(10), BASE_QTY INT);
INSERT INTO @rc_plants VALUES
('P007', 682), ('P022', 636), ('P004', 591), ('P020', 545),
('P003', 455), ('P014', 409), ('P008', 364), ('P025', 318),
('P006', 273), ('P011', 227);

DECLARE @pc NVARCHAR(10), @bq INT;
DECLARE rc_cur CURSOR FOR SELECT PLANT_CD, BASE_QTY FROM @rc_plants;
OPEN rc_cur; FETCH NEXT FROM rc_cur INTO @pc, @bq;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @dt = 1;
    WHILE @dt <= 31
    BEGIN
        DECLARE @dateStr3 CHAR(8) = '202603' + RIGHT('0' + CAST(@dt AS VARCHAR), 2);
        DECLARE @dow3 INT = DATEPART(WEEKDAY, CAST(@dateStr3 AS DATE));
        DECLARE @qty3 DECIMAL(18,2) =
            CASE WHEN @dow3 IN (1,7) THEN 0
                 ELSE CAST(@bq + (ABS(CHECKSUM(NEWID())) % 101) - 50 AS DECIMAL(18,2))
            END;
        IF @qty3 > 0
            INSERT INTO P_PROD_DAILY_HDR (PLANT_CD, PROD_DT, PROD_QTY, PROD_TYPE, ITEM_CD)
            VALUES (@pc, @dateStr3, @qty3, '반복제조', 'RC2500');
        SET @dt += 1;
    END
    FETCH NEXT FROM rc_cur INTO @pc, @bq;
END
CLOSE rc_cur; DEALLOCATE rc_cur;

PRINT 'P_PROD_DAILY_HDR 재삽입: ' + CAST((SELECT COUNT(*) FROM P_PROD_DAILY_HDR) AS VARCHAR) + '건';
GO

-- ──────────────────────────────────────────────────────────────────
-- ③ P_PROD_DAILY_HDR_CKO087 — 반복제조 생산실적 (호기별)
--    단양(P001): RM1/RM2/RM3 각각 분담, 평일 합계 ~4,090 TON
--    영월(P031): RM1/RM2 각각, 평일 합계 ~2,727 TON
--    기간: 2026년 3월 전체 + 4월 1일~20일
-- ──────────────────────────────────────────────────────────────────
DELETE FROM P_PROD_DAILY_HDR_CKO087;

-- 단양공장(P001) — 3개 호기별 삽입
DECLARE @wc_list TABLE (WC_CD NVARCHAR(20), BASE_QTY INT, PLAN_QTY INT, WORK_MIN INT);
INSERT INTO @wc_list VALUES
('RM1', 1400, 1450, 1380),  -- 1호기: 1,400 TON/day, 가동 23h
('RM2', 1350, 1400, 1350),  -- 2호기: 1,350 TON/day, 가동 22.5h
('RM3', 1340, 1380, 1320);  -- 3호기: 1,340 TON/day, 가동 22h

DECLARE @wc NVARCHAR(20), @wbq INT, @wpq INT, @wmm INT;
DECLARE wc_cur CURSOR FOR SELECT WC_CD, BASE_QTY, PLAN_QTY, WORK_MIN FROM @wc_list;
OPEN wc_cur; FETCH NEXT FROM wc_cur INTO @wc, @wbq, @wpq, @wmm;
WHILE @@FETCH_STATUS = 0
BEGIN
    -- 2026년 3월
    DECLARE @d INT = 1;
    WHILE @d <= 31
    BEGIN
        DECLARE @ds CHAR(8) = '202603' + RIGHT('0' + CAST(@d AS VARCHAR), 2);
        DECLARE @dw INT = DATEPART(WEEKDAY, CAST(@ds AS DATE));
        IF @dw NOT IN (1,7)  -- 평일만
        BEGIN
            DECLARE @pq DECIMAL(18,2) = CAST(@wbq + (ABS(CHECKSUM(NEWID())) % 201) - 100 AS DECIMAL(18,2));
            INSERT INTO P_PROD_DAILY_HDR_CKO087
                (PLANT_CD, PROD_DT, WC_CD, WC_GROUP_CD, ITEM_CD, PROD_QTY, PLAN_QTY, FAC_WORK_TIME_MM, DAY_MAGAM_YN)
            VALUES ('P001', @ds, @wc, 'RM', 'CM0001', @pq,
                    CAST(@wpq AS DECIMAL(18,2)),
                    @wmm + (ABS(CHECKSUM(NEWID())) % 61) - 30,
                    'Y');
        END
        SET @d += 1;
    END
    -- 2026년 4월 1일~20일
    SET @d = 1;
    WHILE @d <= 20
    BEGIN
        DECLARE @ds4 CHAR(8) = '202604' + RIGHT('0' + CAST(@d AS VARCHAR), 2);
        DECLARE @dw4 INT = DATEPART(WEEKDAY, CAST(@ds4 AS DATE));
        DECLARE @is_today BIT = CASE WHEN @ds4 = CONVERT(CHAR(8),GETDATE(),112) THEN 1 ELSE 0 END;
        IF @dw4 NOT IN (1,7)
        BEGIN
            DECLARE @pq4 DECIMAL(18,2) = CAST(@wbq + (ABS(CHECKSUM(NEWID())) % 201) - 100 AS DECIMAL(18,2));
            INSERT INTO P_PROD_DAILY_HDR_CKO087
                (PLANT_CD, PROD_DT, WC_CD, WC_GROUP_CD, ITEM_CD, PROD_QTY, PLAN_QTY, FAC_WORK_TIME_MM, DAY_MAGAM_YN)
            VALUES ('P001', @ds4, @wc, 'RM', 'CM0001', @pq4,
                    CAST(@wpq AS DECIMAL(18,2)),
                    @wmm + (ABS(CHECKSUM(NEWID())) % 61) - 30,
                    CASE WHEN @is_today = 1 THEN 'N' ELSE 'Y' END);
        END
        SET @d += 1;
    END
    FETCH NEXT FROM wc_cur INTO @wc, @wbq, @wpq, @wmm;
END
CLOSE wc_cur; DEALLOCATE wc_cur;

-- 영월공장(P031) — 2개 호기
DELETE FROM @wc_list;
INSERT INTO @wc_list VALUES
('RM1', 1400, 1450, 1380),
('RM2', 1327, 1380, 1320);

OPEN wc_cur; -- 재사용 불가, 재선언
DECLARE @wc2 NVARCHAR(20); DECLARE @wbq2 INT; DECLARE @wpq2 INT; DECLARE @wmm2 INT;
DECLARE wc_cur2 CURSOR FOR SELECT WC_CD, BASE_QTY, PLAN_QTY, WORK_MIN FROM @wc_list;
OPEN wc_cur2; FETCH NEXT FROM wc_cur2 INTO @wc2, @wbq2, @wpq2, @wmm2;
WHILE @@FETCH_STATUS = 0
BEGIN
    DECLARE @d2 INT = 1;
    WHILE @d2 <= 31
    BEGIN
        DECLARE @ds2 CHAR(8) = '202603' + RIGHT('0' + CAST(@d2 AS VARCHAR), 2);
        DECLARE @dw2 INT = DATEPART(WEEKDAY, CAST(@ds2 AS DATE));
        IF @dw2 NOT IN (1,7)
        BEGIN
            DECLARE @pq2 DECIMAL(18,2) = CAST(@wbq2 + (ABS(CHECKSUM(NEWID())) % 201) - 100 AS DECIMAL(18,2));
            INSERT INTO P_PROD_DAILY_HDR_CKO087
                (PLANT_CD, PROD_DT, WC_CD, WC_GROUP_CD, ITEM_CD, PROD_QTY, PLAN_QTY, FAC_WORK_TIME_MM, DAY_MAGAM_YN)
            VALUES ('P031', @ds2, @wc2, 'RM', 'CM0001', @pq2,
                    CAST(@wpq2 AS DECIMAL(18,2)),
                    @wmm2 + (ABS(CHECKSUM(NEWID())) % 61) - 30,
                    'Y');
        END
        SET @d2 += 1;
    END
    DECLARE @d2a INT = 1;
    WHILE @d2a <= 20
    BEGIN
        DECLARE @ds2a CHAR(8) = '202604' + RIGHT('0' + CAST(@d2a AS VARCHAR), 2);
        DECLARE @dw2a INT = DATEPART(WEEKDAY, CAST(@ds2a AS DATE));
        IF @dw2a NOT IN (1,7)
        BEGIN
            DECLARE @pq2a DECIMAL(18,2) = CAST(@wbq2 + (ABS(CHECKSUM(NEWID())) % 201) - 100 AS DECIMAL(18,2));
            INSERT INTO P_PROD_DAILY_HDR_CKO087
                (PLANT_CD, PROD_DT, WC_CD, WC_GROUP_CD, ITEM_CD, PROD_QTY, PLAN_QTY, FAC_WORK_TIME_MM, DAY_MAGAM_YN)
            VALUES ('P031', @ds2a, @wc2, 'RM', 'CM0001', @pq2a,
                    CAST(@wpq2 AS DECIMAL(18,2)),
                    @wmm2 + (ABS(CHECKSUM(NEWID())) % 61) - 30,
                    CASE WHEN @ds2a = CONVERT(CHAR(8),GETDATE(),112) THEN 'N' ELSE 'Y' END);
        END
        SET @d2a += 1;
    END
    FETCH NEXT FROM wc_cur2 INTO @wc2, @wbq2, @wpq2, @wmm2;
END
CLOSE wc_cur2; DEALLOCATE wc_cur2;

PRINT 'P_PROD_DAILY_HDR_CKO087 삽입: '
      + CAST((SELECT COUNT(*) FROM P_PROD_DAILY_HDR_CKO087) AS VARCHAR) + '건';
GO

-- ──────────────────────────────────────────────────────────────────
-- ④ P_PROD_DAILY_DTL_CKO087 — 반복제조 상세 (HDR 기준 상세 생성)
-- ──────────────────────────────────────────────────────────────────
DELETE FROM P_PROD_DAILY_DTL_CKO087;

INSERT INTO P_PROD_DAILY_DTL_CKO087
    (PLANT_CD, PROD_DT, WC_CD, ITEM_CD, PROD_QTY, LOSS_QTY, RAW_MAT_QTY, DAY_MAGAM_YN)
SELECT
    H.PLANT_CD,
    H.PROD_DT,
    H.WC_CD,
    H.ITEM_CD,
    H.PROD_QTY,
    -- 손실률 0.8~1.5%
    CAST(H.PROD_QTY * (0.008 + (ABS(CHECKSUM(NEWID())) % 8) * 0.001) AS DECIMAL(18,2)),
    -- 원료투입 = 생산량 × 1.55 (시멘트 소성 원료비)
    CAST(H.PROD_QTY * 1.55 AS DECIMAL(18,2)),
    H.DAY_MAGAM_YN
FROM P_PROD_DAILY_HDR_CKO087 H;

PRINT 'P_PROD_DAILY_DTL_CKO087 삽입: '
      + CAST((SELECT COUNT(*) FROM P_PROD_DAILY_DTL_CKO087) AS VARCHAR) + '건';
GO

-- ──────────────────────────────────────────────────────────────────
-- ⑤ P_PROD_REMICON_HDR_CKO087 — 레미콘 생산실적 (믹서별)
--    공장별 일일 기준 생산량 (M3), 주말 0
-- ──────────────────────────────────────────────────────────────────
DELETE FROM P_PROD_REMICON_HDR_CKO087;

DECLARE @rc2 TABLE (PLANT_CD NVARCHAR(10), WC_CD NVARCHAR(20), BASE_QTY INT, PLAN_QTY INT);
INSERT INTO @rc2 VALUES
-- 성남(P007): 2개 믹서로 682 M3/day 분담
('P007','RC1', 360, 380), ('P007','RC2', 322, 340),
-- 화성(P022): 2개 믹서로 636 M3/day 분담
('P022','RC1', 340, 360), ('P022','RC2', 296, 310),
-- 부산(P004): 591 M3/day
('P004','RC1', 591, 620),
-- 서인천(P020): 2개 믹서로 545 M3/day 분담
('P020','RC1', 290, 310), ('P020','RC2', 255, 270),
-- 대구(P003): 455 M3/day
('P003','RC1', 455, 480),
-- 서대구(P014): 409 M3/day
('P014','RC1', 409, 430),
-- 대전(P008): 364 M3/day
('P008','RC1', 364, 385),
-- 부천(P025): 318 M3/day
('P025','RC1', 318, 335),
-- 청주(P006): 273 M3/day
('P006','RC1', 273, 290),
-- 김해(P011): 227 M3/day
('P011','RC1', 227, 245);

DECLARE @rpc NVARCHAR(10), @rwc NVARCHAR(20), @rbq INT, @rpq INT;
DECLARE rc2_cur CURSOR FOR SELECT PLANT_CD, WC_CD, BASE_QTY, PLAN_QTY FROM @rc2;
OPEN rc2_cur; FETCH NEXT FROM rc2_cur INTO @rpc, @rwc, @rbq, @rpq;
WHILE @@FETCH_STATUS = 0
BEGIN
    -- ITEM_CD 사이클 (RC2500/RC3000/RC3500 혼합 — 6:3:1 비율로 배합번호 결정)
    DECLARE @dd INT = 1;
    WHILE @dd <= 31
    BEGIN
        DECLARE @dds CHAR(8) = '202603' + RIGHT('0' + CAST(@dd AS VARCHAR), 2);
        DECLARE @dws INT = DATEPART(WEEKDAY, CAST(@dds AS DATE));
        IF @dws NOT IN (1,7)
        BEGIN
            DECLARE @rqty DECIMAL(18,2)  = CAST(@rbq + (ABS(CHECKSUM(NEWID())) % 51) - 25 AS DECIMAL(18,2));
            DECLARE @icd  NVARCHAR(20)   = CASE (ABS(CHECKSUM(NEWID())) % 10)
                                             WHEN 9 THEN 'RC3500'
                                             WHEN 8 THEN 'RC3000'
                                             WHEN 7 THEN 'RC3000'
                                             WHEN 6 THEN 'RC3000'
                                             ELSE 'RC2500'
                                           END;
            DECLARE @mixno NVARCHAR(20) = 'MX' + RIGHT('000' + CAST(ABS(CHECKSUM(NEWID())) % 900 + 100, 3), 3);
            DECLARE @truck INT = CAST(@rqty / 6.0 AS INT) + 1;  -- 레미콘 1대 = 약 6M3
            INSERT INTO P_PROD_REMICON_HDR_CKO087
                (PLANT_CD, PROD_DT, WC_CD, MIX_NO, ITEM_CD, PROD_QTY, PLAN_QTY, TRUCK_CNT, DAY_MAGAM_YN)
            VALUES (@rpc, @dds, @rwc, @mixno, @icd, @rqty, CAST(@rpq AS DECIMAL(18,2)), @truck, 'Y');
        END
        SET @dd += 1;
    END
    -- 4월 1~20일
    SET @dd = 1;
    WHILE @dd <= 20
    BEGIN
        DECLARE @dds4r CHAR(8) = '202604' + RIGHT('0' + CAST(@dd AS VARCHAR), 2);
        DECLARE @dws4r INT = DATEPART(WEEKDAY, CAST(@dds4r AS DATE));
        IF @dws4r NOT IN (1,7)
        BEGIN
            DECLARE @rqty4 DECIMAL(18,2) = CAST(@rbq + (ABS(CHECKSUM(NEWID())) % 51) - 25 AS DECIMAL(18,2));
            DECLARE @icd4  NVARCHAR(20)  = CASE (ABS(CHECKSUM(NEWID())) % 10)
                                             WHEN 9 THEN 'RC3500'
                                             WHEN 7,8 THEN 'RC3000'
                                             ELSE 'RC2500'
                                           END;
            DECLARE @mixno4 NVARCHAR(20) = 'MX' + RIGHT('000' + CAST(ABS(CHECKSUM(NEWID())) % 900 + 100, 3), 3);
            INSERT INTO P_PROD_REMICON_HDR_CKO087
                (PLANT_CD, PROD_DT, WC_CD, MIX_NO, ITEM_CD, PROD_QTY, PLAN_QTY, TRUCK_CNT, DAY_MAGAM_YN)
            VALUES (@rpc, @dds4r, @rwc, @mixno4, @icd4, @rqty4,
                    CAST(@rpq AS DECIMAL(18,2)),
                    CAST(@rqty4 / 6.0 AS INT) + 1,
                    CASE WHEN @dds4r = CONVERT(CHAR(8),GETDATE(),112) THEN 'N' ELSE 'Y' END);
        END
        SET @dd += 1;
    END
    FETCH NEXT FROM rc2_cur INTO @rpc, @rwc, @rbq, @rpq;
END
CLOSE rc2_cur; DEALLOCATE rc2_cur;

PRINT 'P_PROD_REMICON_HDR_CKO087 삽입: '
      + CAST((SELECT COUNT(*) FROM P_PROD_REMICON_HDR_CKO087) AS VARCHAR) + '건';
GO

-- ──────────────────────────────────────────────────────────────────
-- ⑥ P_PROD_REMICON_DTL_CKO087 — 레미콘 상세 (HDR 기준 납품처 추가)
-- ──────────────────────────────────────────────────────────────────
DELETE FROM P_PROD_REMICON_DTL_CKO087;

-- 납품처코드 목록 (건설현장 코드 예시)
DECLARE @deliver TABLE (SEQ INT IDENTITY(1,1), DLV_CD NVARCHAR(20));
INSERT INTO @deliver VALUES
('SITE001'),('SITE002'),('SITE003'),('SITE004'),('SITE005');

INSERT INTO P_PROD_REMICON_DTL_CKO087
    (PLANT_CD, PROD_DT, WC_CD, MIX_NO, DELIVERY_CD, PROD_QTY, DAY_MAGAM_YN)
SELECT
    H.PLANT_CD,
    H.PROD_DT,
    H.WC_CD,
    H.MIX_NO,
    'SITE' + RIGHT('00' + CAST((ABS(CHECKSUM(NEWID())) % 5) + 1 AS VARCHAR), 3),
    -- 상세는 HDR의 60~80% 물량 (나머지는 다른 배치)
    CAST(H.PROD_QTY * (0.60 + (ABS(CHECKSUM(NEWID())) % 21) * 0.01) AS DECIMAL(18,2)),
    H.DAY_MAGAM_YN
FROM P_PROD_REMICON_HDR_CKO087 H;

PRINT 'P_PROD_REMICON_DTL_CKO087 삽입: '
      + CAST((SELECT COUNT(*) FROM P_PROD_REMICON_DTL_CKO087) AS VARCHAR) + '건';
GO

-- ──────────────────────────────────────────────────────────────────
-- ⑦ P_PROD_REMITAL_HDR_CKO087 — 레미탈 생산실적
--    P050(수원): 평일 ~300 TON/day
--    P051(광주): 평일 ~250 TON/day
-- ──────────────────────────────────────────────────────────────────
DELETE FROM P_PROD_REMITAL_HDR_CKO087;

DECLARE @rt_plants TABLE (PLANT_CD NVARCHAR(10), WC_CD NVARCHAR(20), BASE_QTY INT, PLAN_QTY INT);
INSERT INTO @rt_plants VALUES
('P050', 'RT1', 300, 315),
('P051', 'RT1', 250, 265);

DECLARE @rtpc NVARCHAR(10), @rtwc NVARCHAR(20), @rtbq INT, @rtpq INT;
DECLARE rt_cur CURSOR FOR SELECT PLANT_CD, WC_CD, BASE_QTY, PLAN_QTY FROM @rt_plants;
OPEN rt_cur; FETCH NEXT FROM rt_cur INTO @rtpc, @rtwc, @rtbq, @rtpq;
WHILE @@FETCH_STATUS = 0
BEGIN
    DECLARE @rtd INT = 1;
    WHILE @rtd <= 31
    BEGIN
        DECLARE @rtds CHAR(8) = '202603' + RIGHT('0' + CAST(@rtd AS VARCHAR), 2);
        DECLARE @rtdw INT = DATEPART(WEEKDAY, CAST(@rtds AS DATE));
        IF @rtdw NOT IN (1,7)
        BEGIN
            DECLARE @rtqty DECIMAL(18,2) = CAST(@rtbq + (ABS(CHECKSUM(NEWID())) % 61) - 30 AS DECIMAL(18,2));
            INSERT INTO P_PROD_REMITAL_HDR_CKO087
                (PLANT_CD, PROD_DT, WC_CD, ITEM_CD, PROD_QTY, PLAN_QTY, DAY_MAGAM_YN)
            VALUES (@rtpc, @rtds, @rtwc, 'RT0001', @rtqty, CAST(@rtpq AS DECIMAL(18,2)), 'Y');
        END
        SET @rtd += 1;
    END
    SET @rtd = 1;
    WHILE @rtd <= 20
    BEGIN
        DECLARE @rtds4 CHAR(8) = '202604' + RIGHT('0' + CAST(@rtd AS VARCHAR), 2);
        DECLARE @rtdw4 INT = DATEPART(WEEKDAY, CAST(@rtds4 AS DATE));
        IF @rtdw4 NOT IN (1,7)
        BEGIN
            DECLARE @rtqty4 DECIMAL(18,2) = CAST(@rtbq + (ABS(CHECKSUM(NEWID())) % 61) - 30 AS DECIMAL(18,2));
            INSERT INTO P_PROD_REMITAL_HDR_CKO087
                (PLANT_CD, PROD_DT, WC_CD, ITEM_CD, PROD_QTY, PLAN_QTY, DAY_MAGAM_YN)
            VALUES (@rtpc, @rtds4, @rtwc, 'RT0001', @rtqty4,
                    CAST(@rtpq AS DECIMAL(18,2)),
                    CASE WHEN @rtds4 = CONVERT(CHAR(8),GETDATE(),112) THEN 'N' ELSE 'Y' END);
        END
        SET @rtd += 1;
    END
    FETCH NEXT FROM rt_cur INTO @rtpc, @rtwc, @rtbq, @rtpq;
END
CLOSE rt_cur; DEALLOCATE rt_cur;

PRINT 'P_PROD_REMITAL_HDR_CKO087 삽입: '
      + CAST((SELECT COUNT(*) FROM P_PROD_REMITAL_HDR_CKO087) AS VARCHAR) + '건';
GO

-- ──────────────────────────────────────────────────────────────────
-- ⑧ P_PROD_REMITAL_DTL_CKO087 — 레미탈 상세
-- ──────────────────────────────────────────────────────────────────
DELETE FROM P_PROD_REMITAL_DTL_CKO087;

INSERT INTO P_PROD_REMITAL_DTL_CKO087
    (PLANT_CD, PROD_DT, WC_CD, ITEM_CD, PROD_QTY, LOSS_QTY, DAY_MAGAM_YN)
SELECT
    H.PLANT_CD, H.PROD_DT, H.WC_CD, H.ITEM_CD,
    H.PROD_QTY,
    CAST(H.PROD_QTY * (0.005 + (ABS(CHECKSUM(NEWID())) % 6) * 0.001) AS DECIMAL(18,2)),
    H.DAY_MAGAM_YN
FROM P_PROD_REMITAL_HDR_CKO087 H;

PRINT 'P_PROD_REMITAL_DTL_CKO087 삽입: '
      + CAST((SELECT COUNT(*) FROM P_PROD_REMITAL_DTL_CKO087) AS VARCHAR) + '건';
GO

-- ──────────────────────────────────────────────────────────────────
-- 최종 검증 — 테이블별 행 수 + 주요 집계
-- ──────────────────────────────────────────────────────────────────
SELECT '── 테이블별 행 수 ──' AS INFO;

SELECT TBL, CNT FROM (
    SELECT 'P_WORK_CENTER'               AS TBL, COUNT(*) AS CNT FROM P_WORK_CENTER                  UNION ALL
    SELECT 'P_PROD_DAILY_HDR',                    COUNT(*)          FROM P_PROD_DAILY_HDR              UNION ALL
    SELECT 'P_PROD_DAILY_HDR_CKO087',             COUNT(*)          FROM P_PROD_DAILY_HDR_CKO087       UNION ALL
    SELECT 'P_PROD_DAILY_DTL_CKO087',             COUNT(*)          FROM P_PROD_DAILY_DTL_CKO087       UNION ALL
    SELECT 'P_PROD_REMICON_HDR_CKO087',           COUNT(*)          FROM P_PROD_REMICON_HDR_CKO087     UNION ALL
    SELECT 'P_PROD_REMICON_DTL_CKO087',           COUNT(*)          FROM P_PROD_REMICON_DTL_CKO087     UNION ALL
    SELECT 'P_PROD_REMITAL_HDR_CKO087',           COUNT(*)          FROM P_PROD_REMITAL_HDR_CKO087     UNION ALL
    SELECT 'P_PROD_REMITAL_DTL_CKO087',           COUNT(*)          FROM P_PROD_REMITAL_DTL_CKO087
) X ORDER BY TBL;

SELECT '── 2026년 3월 공장별 생산량(TON) ──' AS INFO;

SELECT B.PLANT_NM, SUM(H.PROD_QTY) AS 생산량_TON
FROM P_PROD_DAILY_HDR_CKO087 H
JOIN B_PLANT B ON B.PLANT_CD = H.PLANT_CD
WHERE H.PROD_DT BETWEEN '20260301' AND '20260331' AND H.DAY_MAGAM_YN = 'Y'
GROUP BY B.PLANT_NM ORDER BY 생산량_TON DESC;

SELECT '── 2026년 3월 레미콘 공장별 생산량(M3) ──' AS INFO;

SELECT B.PLANT_NM, SUM(H.PROD_QTY) AS 생산량_M3
FROM P_PROD_REMICON_HDR_CKO087 H
JOIN B_PLANT B ON B.PLANT_CD = H.PLANT_CD
WHERE H.PROD_DT BETWEEN '20260301' AND '20260331' AND H.DAY_MAGAM_YN = 'Y'
GROUP BY B.PLANT_NM ORDER BY 생산량_M3 DESC;
GO

PRINT '══ 07 완료: 현실화 샘플 데이터 삽입 ══';
GO
