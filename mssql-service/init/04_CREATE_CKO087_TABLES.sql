-- ══════════════════════════════════════════════════════════════════
-- 04. CKO087 생산실적 테이블 + 보조 마스터 테이블 생성
-- 대상 DB : PA999_DEMO
-- 작성일  : 2026-04-20
-- ══════════════════════════════════════════════════════════════════

USE PA999_DEMO;
GO

-- ──────────────────────────────────────────────────────────────────
-- ① B_PLANT — PLANT_GUBUN_CD 컬럼 추가
--    100=반복제조(시멘트), 200=지역공장, 300=레미콘, 400=레미탈
-- ──────────────────────────────────────────────────────────────────
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME='B_PLANT' AND COLUMN_NAME='PLANT_GUBUN_CD')
    ALTER TABLE B_PLANT ADD PLANT_GUBUN_CD NVARCHAR(10) NULL;
GO

UPDATE B_PLANT SET PLANT_GUBUN_CD = '100' WHERE PLANT_TYPE = 'CEMENT';
UPDATE B_PLANT SET PLANT_GUBUN_CD = '300' WHERE PLANT_TYPE = 'REMICON';
UPDATE B_PLANT SET PLANT_GUBUN_CD = '400' WHERE PLANT_TYPE = 'REMITAL';
PRINT 'B_PLANT.PLANT_GUBUN_CD 설정 완료';
GO

-- 레미탈 공장 2개 추가 (없으면)
IF NOT EXISTS (SELECT 1 FROM B_PLANT WHERE PLANT_CD = 'P050')
    INSERT INTO B_PLANT (PLANT_CD, PLANT_NM, PLANT_TYPE, PLANT_GUBUN_CD, BIZ_AREA_CD, REGION_NM, USE_YN)
    VALUES ('P050', '수원레미탈공장', 'REMITAL', '400', 'BA03', '경기', 'Y');

IF NOT EXISTS (SELECT 1 FROM B_PLANT WHERE PLANT_CD = 'P051')
    INSERT INTO B_PLANT (PLANT_CD, PLANT_NM, PLANT_TYPE, PLANT_GUBUN_CD, BIZ_AREA_CD, REGION_NM, USE_YN)
    VALUES ('P051', '광주레미탈공장', 'REMITAL', '400', 'BA02', '광주', 'Y');

PRINT 'B_PLANT 레미탈 공장 추가 완료';
GO

-- ──────────────────────────────────────────────────────────────────
-- ② P_WORK_CENTER — 공장별 작업장(호기/라인) 마스터
-- ──────────────────────────────────────────────────────────────────
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'P_WORK_CENTER')
BEGIN
    CREATE TABLE P_WORK_CENTER (
        WC_SEQ       INT           NOT NULL IDENTITY(1,1) PRIMARY KEY,
        PLANT_CD     NVARCHAR(10)  NOT NULL,
        WC_CD        NVARCHAR(20)  NOT NULL,
        WC_NM        NVARCHAR(100) NOT NULL,
        WC_GROUP_CD  NVARCHAR(20)  NULL,          -- RM=소성/원료, CM=분쇄, RC=레미콘, RT=레미탈
        WC_GROUP_NM  NVARCHAR(100) NULL,
        USE_YN       CHAR(1)       NOT NULL DEFAULT 'Y',
        INSRT_DT     DATETIME      NOT NULL DEFAULT GETDATE()
    );
    CREATE UNIQUE INDEX UX_P_WORK_CENTER ON P_WORK_CENTER(PLANT_CD, WC_CD);
    PRINT 'P_WORK_CENTER 생성 완료';
END
GO

-- ──────────────────────────────────────────────────────────────────
-- ③ P_PROD_DAILY_HDR_CKO087 — 반복제조(시멘트) 생산실적 헤더
-- ──────────────────────────────────────────────────────────────────
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'P_PROD_DAILY_HDR_CKO087')
BEGIN
    CREATE TABLE P_PROD_DAILY_HDR_CKO087 (
        PROD_SEQ         BIGINT        NOT NULL IDENTITY(1,1) PRIMARY KEY,
        PLANT_CD         NVARCHAR(10)  NOT NULL,
        PROD_DT          CHAR(8)       NOT NULL,   -- YYYYMMDD
        WC_CD            NVARCHAR(20)  NULL,        -- 작업장(호기)코드
        WC_GROUP_CD      NVARCHAR(20)  NULL,        -- 작업장그룹코드 (RM% = 소성/원료)
        ITEM_CD          NVARCHAR(20)  NULL,        -- 품목코드
        PROD_QTY         DECIMAL(18,2) NULL DEFAULT 0,   -- 생산량 (TON)
        PLAN_QTY         DECIMAL(18,2) NULL DEFAULT 0,   -- 계획량 (TON)
        FAC_WORK_TIME_MM INT           NULL DEFAULT 0,   -- 가동시간 (분)
        DAY_MAGAM_YN     CHAR(1)       NOT NULL DEFAULT 'N', -- Y=마감완료, N=미마감
        INSRT_DT         DATETIME      NOT NULL DEFAULT GETDATE(),
        UPDT_DT          DATETIME      NOT NULL DEFAULT GETDATE()
    );
    CREATE INDEX IX_DAILY_HDR_PLANT_DT
        ON P_PROD_DAILY_HDR_CKO087(PLANT_CD, PROD_DT);
    PRINT 'P_PROD_DAILY_HDR_CKO087 생성 완료';
END
GO

-- ──────────────────────────────────────────────────────────────────
-- ④ P_PROD_DAILY_DTL_CKO087 — 반복제조 생산실적 상세
-- ──────────────────────────────────────────────────────────────────
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'P_PROD_DAILY_DTL_CKO087')
BEGIN
    CREATE TABLE P_PROD_DAILY_DTL_CKO087 (
        DTL_SEQ          BIGINT        NOT NULL IDENTITY(1,1) PRIMARY KEY,
        PLANT_CD         NVARCHAR(10)  NOT NULL,
        PROD_DT          CHAR(8)       NOT NULL,
        WC_CD            NVARCHAR(20)  NULL,
        ITEM_CD          NVARCHAR(20)  NULL,
        PROD_QTY         DECIMAL(18,2) NULL DEFAULT 0,   -- 생산량 (TON)
        LOSS_QTY         DECIMAL(18,2) NULL DEFAULT 0,   -- 손실량 (TON)
        RAW_MAT_QTY      DECIMAL(18,2) NULL DEFAULT 0,   -- 원료투입량 (TON)
        DAY_MAGAM_YN     CHAR(1)       NOT NULL DEFAULT 'N',
        INSRT_DT         DATETIME      NOT NULL DEFAULT GETDATE()
    );
    CREATE INDEX IX_DAILY_DTL_PLANT_DT
        ON P_PROD_DAILY_DTL_CKO087(PLANT_CD, PROD_DT);
    PRINT 'P_PROD_DAILY_DTL_CKO087 생성 완료';
END
GO

-- ──────────────────────────────────────────────────────────────────
-- ⑤ P_PROD_REMICON_HDR_CKO087 — 레미콘 생산실적 헤더
-- ──────────────────────────────────────────────────────────────────
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'P_PROD_REMICON_HDR_CKO087')
BEGIN
    CREATE TABLE P_PROD_REMICON_HDR_CKO087 (
        PROD_SEQ         BIGINT        NOT NULL IDENTITY(1,1) PRIMARY KEY,
        PLANT_CD         NVARCHAR(10)  NOT NULL,
        PROD_DT          CHAR(8)       NOT NULL,
        WC_CD            NVARCHAR(20)  NULL,        -- 믹서 코드
        MIX_NO           NVARCHAR(20)  NULL,        -- 배합번호
        ITEM_CD          NVARCHAR(20)  NULL,        -- 레미콘 규격 (RC2500/RC3000/RC3500)
        PROD_QTY         DECIMAL(18,2) NULL DEFAULT 0,   -- 생산량 (M3)
        PLAN_QTY         DECIMAL(18,2) NULL DEFAULT 0,   -- 계획량 (M3)
        TRUCK_CNT        INT           NULL DEFAULT 0,   -- 출하 차량수
        DAY_MAGAM_YN     CHAR(1)       NOT NULL DEFAULT 'N',
        INSRT_DT         DATETIME      NOT NULL DEFAULT GETDATE()
    );
    CREATE INDEX IX_REMICON_HDR_PLANT_DT
        ON P_PROD_REMICON_HDR_CKO087(PLANT_CD, PROD_DT);
    PRINT 'P_PROD_REMICON_HDR_CKO087 생성 완료';
END
GO

-- ──────────────────────────────────────────────────────────────────
-- ⑥ P_PROD_REMICON_DTL_CKO087 — 레미콘 생산실적 상세
-- ──────────────────────────────────────────────────────────────────
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'P_PROD_REMICON_DTL_CKO087')
BEGIN
    CREATE TABLE P_PROD_REMICON_DTL_CKO087 (
        DTL_SEQ          BIGINT        NOT NULL IDENTITY(1,1) PRIMARY KEY,
        PLANT_CD         NVARCHAR(10)  NOT NULL,
        PROD_DT          CHAR(8)       NOT NULL,
        WC_CD            NVARCHAR(20)  NULL,
        MIX_NO           NVARCHAR(20)  NULL,
        DELIVERY_CD      NVARCHAR(20)  NULL,        -- 납품처코드
        PROD_QTY         DECIMAL(18,2) NULL DEFAULT 0,
        DAY_MAGAM_YN     CHAR(1)       NOT NULL DEFAULT 'N',
        INSRT_DT         DATETIME      NOT NULL DEFAULT GETDATE()
    );
    CREATE INDEX IX_REMICON_DTL_PLANT_DT
        ON P_PROD_REMICON_DTL_CKO087(PLANT_CD, PROD_DT);
    PRINT 'P_PROD_REMICON_DTL_CKO087 생성 완료';
END
GO

-- ──────────────────────────────────────────────────────────────────
-- ⑦ P_PROD_REMITAL_HDR_CKO087 — 레미탈 생산실적 헤더
-- ──────────────────────────────────────────────────────────────────
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'P_PROD_REMITAL_HDR_CKO087')
BEGIN
    CREATE TABLE P_PROD_REMITAL_HDR_CKO087 (
        PROD_SEQ         BIGINT        NOT NULL IDENTITY(1,1) PRIMARY KEY,
        PLANT_CD         NVARCHAR(10)  NOT NULL,
        PROD_DT          CHAR(8)       NOT NULL,
        WC_CD            NVARCHAR(20)  NULL,
        ITEM_CD          NVARCHAR(20)  NULL,
        PROD_QTY         DECIMAL(18,2) NULL DEFAULT 0,   -- 생산량 (TON)
        PLAN_QTY         DECIMAL(18,2) NULL DEFAULT 0,   -- 계획량 (TON)
        DAY_MAGAM_YN     CHAR(1)       NOT NULL DEFAULT 'N',
        INSRT_DT         DATETIME      NOT NULL DEFAULT GETDATE()
    );
    CREATE INDEX IX_REMITAL_HDR_PLANT_DT
        ON P_PROD_REMITAL_HDR_CKO087(PLANT_CD, PROD_DT);
    PRINT 'P_PROD_REMITAL_HDR_CKO087 생성 완료';
END
GO

-- ──────────────────────────────────────────────────────────────────
-- ⑧ P_PROD_REMITAL_DTL_CKO087 — 레미탈 생산실적 상세
-- ──────────────────────────────────────────────────────────────────
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'P_PROD_REMITAL_DTL_CKO087')
BEGIN
    CREATE TABLE P_PROD_REMITAL_DTL_CKO087 (
        DTL_SEQ          BIGINT        NOT NULL IDENTITY(1,1) PRIMARY KEY,
        PLANT_CD         NVARCHAR(10)  NOT NULL,
        PROD_DT          CHAR(8)       NOT NULL,
        WC_CD            NVARCHAR(20)  NULL,
        ITEM_CD          NVARCHAR(20)  NULL,
        PROD_QTY         DECIMAL(18,2) NULL DEFAULT 0,
        LOSS_QTY         DECIMAL(18,2) NULL DEFAULT 0,   -- 손실량 (TON)
        DAY_MAGAM_YN     CHAR(1)       NOT NULL DEFAULT 'N',
        INSRT_DT         DATETIME      NOT NULL DEFAULT GETDATE()
    );
    CREATE INDEX IX_REMITAL_DTL_PLANT_DT
        ON P_PROD_REMITAL_DTL_CKO087(PLANT_CD, PROD_DT);
    PRINT 'P_PROD_REMITAL_DTL_CKO087 생성 완료';
END
GO

-- ──────────────────────────────────────────────────────────────────
-- ⑨ PA999_COLUMN_META — AI 컬럼 한국어 설명 테이블
-- ──────────────────────────────────────────────────────────────────
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'PA999_COLUMN_META')
BEGIN
    CREATE TABLE PA999_COLUMN_META (
        COL_SEQ      INT            NOT NULL IDENTITY(1,1) PRIMARY KEY,
        TABLE_NM     NVARCHAR(100)  NOT NULL,
        COLUMN_NM    NVARCHAR(100)  NOT NULL,
        KO_NM        NVARCHAR(200)  NULL,          -- 한국어 컬럼명
        DATA_TYPE    NVARCHAR(50)   NULL,
        MAX_LENGTH   INT            NULL,
        IS_NULLABLE  CHAR(1)        NULL DEFAULT 'Y',
        BIZ_RULE     NVARCHAR(1000) NULL,           -- 업무 규칙 / 상세 설명
        CODE_VALUES  NVARCHAR(2000) NULL,            -- 코드값 목록 (예: Y=마감완료|N=미마감)
        USE_YN       CHAR(1)        NOT NULL DEFAULT 'Y',
        SRC_TYPE     CHAR(1)        NOT NULL DEFAULT 'M',  -- M=수동입력, A=AI분석
        INSRT_DT     DATETIME       NOT NULL DEFAULT GETDATE(),
        UPDT_DT      DATETIME       NOT NULL DEFAULT GETDATE()
    );
    CREATE UNIQUE INDEX UX_PA999_COLUMN_META
        ON PA999_COLUMN_META(TABLE_NM, COLUMN_NM);
    PRINT 'PA999_COLUMN_META 생성 완료';
END
GO

-- ──────────────────────────────────────────────────────────────────
-- 최종 확인
-- ──────────────────────────────────────────────────────────────────
SELECT TABLE_NAME, TABLE_TYPE
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_NAME IN (
    'B_PLANT','B_ITEM','P_WORK_CENTER',
    'P_PROD_DAILY_HDR_CKO087','P_PROD_DAILY_DTL_CKO087',
    'P_PROD_REMICON_HDR_CKO087','P_PROD_REMICON_DTL_CKO087',
    'P_PROD_REMITAL_HDR_CKO087','P_PROD_REMITAL_DTL_CKO087',
    'PA999_COLUMN_META'
)
ORDER BY TABLE_NAME;
GO

PRINT '══ 04 완료: CKO087 테이블 생성 ══';
GO
