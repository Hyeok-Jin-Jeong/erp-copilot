-- ══════════════════════════════════════════════════════════════
-- PA999_DEMO 테이블 생성 스크립트
-- 실행 대상: PA999_DEMO 데이터베이스
-- ══════════════════════════════════════════════════════════════

USE PA999_DEMO;
GO

-- ──────────────────────────────────────────────────────────────
-- ■ ERP 마스터 테이블
-- ──────────────────────────────────────────────────────────────

-- B_PLANT (공장 마스터)
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'B_PLANT')
BEGIN
    CREATE TABLE B_PLANT (
        PLANT_CD        NVARCHAR(10)  NOT NULL PRIMARY KEY,
        PLANT_NM        NVARCHAR(100) NOT NULL,
        PLANT_TYPE      NVARCHAR(20)  NULL,       -- REMICON, CEMENT, REMITAL
        BIZ_AREA_CD     NVARCHAR(10)  NULL,
        REGION_NM       NVARCHAR(50)  NULL,
        USE_YN          CHAR(1)       NOT NULL DEFAULT 'Y',
        INSRT_DT        DATETIME      NOT NULL DEFAULT GETDATE(),
        UPDT_DT         DATETIME      NOT NULL DEFAULT GETDATE()
    );
    PRINT 'B_PLANT 생성 완료';
END
GO

-- B_ITEM (품목 마스터)
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'B_ITEM')
BEGIN
    CREATE TABLE B_ITEM (
        ITEM_CD         NVARCHAR(20)  NOT NULL PRIMARY KEY,
        ITEM_NM         NVARCHAR(200) NOT NULL,
        ITEM_TYPE_CD    NVARCHAR(10)  NULL,
        UNIT            NVARCHAR(10)  NULL,
        USE_YN          CHAR(1)       NOT NULL DEFAULT 'Y',
        INSRT_DT        DATETIME      NOT NULL DEFAULT GETDATE()
    );
    PRINT 'B_ITEM 생성 완료';
END
GO

-- A_ACCT (계정과목 마스터)
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'A_ACCT')
BEGIN
    CREATE TABLE A_ACCT (
        ACCT_CD         NVARCHAR(10)  NOT NULL PRIMARY KEY,
        ACCT_NM         NVARCHAR(100) NOT NULL,
        ACCT_TYPE_CD    NVARCHAR(10)  NULL,
        USE_YN          CHAR(1)       NOT NULL DEFAULT 'Y',
        INSRT_DT        DATETIME      NOT NULL DEFAULT GETDATE()
    );
    PRINT 'A_ACCT 생성 완료';
END
GO

-- ──────────────────────────────────────────────────────────────
-- ■ 생산 실적 테이블
-- ──────────────────────────────────────────────────────────────

-- P_PROD_DAILY_HDR (일별 생산 실적 헤더)
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'P_PROD_DAILY_HDR')
BEGIN
    CREATE TABLE P_PROD_DAILY_HDR (
        PROD_SEQ        BIGINT        NOT NULL IDENTITY(1,1) PRIMARY KEY,
        PLANT_CD        NVARCHAR(10)  NOT NULL,
        PROD_DT         CHAR(8)       NOT NULL,   -- YYYYMMDD
        WORK_CD         NVARCHAR(20)  NULL,        -- 작업장코드
        PROD_QTY        DECIMAL(18,2) NULL DEFAULT 0,
        PROD_TYPE       NVARCHAR(10)  NULL,        -- 반복제조, 주문제조
        ITEM_CD         NVARCHAR(20)  NULL,
        INSRT_DT        DATETIME      NOT NULL DEFAULT GETDATE()
    );
    CREATE INDEX IX_P_PROD_DAILY_HDR_PLANT_DT
        ON P_PROD_DAILY_HDR(PLANT_CD, PROD_DT);
    PRINT 'P_PROD_DAILY_HDR 생성 완료';
END
GO

-- ──────────────────────────────────────────────────────────────
-- ■ PA999 AI 챗봇 전용 테이블
-- ──────────────────────────────────────────────────────────────

-- PA999_TABLE_META (테이블 메타데이터 — AI 테이블 식별용)
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'PA999_TABLE_META')
BEGIN
    CREATE TABLE PA999_TABLE_META (
        META_SEQ        INT           NOT NULL IDENTITY(1,1) PRIMARY KEY,
        TABLE_NM        NVARCHAR(100) NOT NULL,
        TABLE_DESC      NVARCHAR(500) NULL,
        KEYWORD_LIST    NVARCHAR(1000) NULL,       -- 쉼표 구분 키워드
        USE_YN          CHAR(1)       NOT NULL DEFAULT 'Y',
        SRC_TYPE        CHAR(1)       NOT NULL DEFAULT 'A',  -- A=AI분석, M=수동입력
        INSRT_DT        DATETIME      NOT NULL DEFAULT GETDATE(),
        UPDT_DT         DATETIME      NOT NULL DEFAULT GETDATE()
    );
    CREATE UNIQUE INDEX UX_PA999_TABLE_META_NM
        ON PA999_TABLE_META(TABLE_NM);
    PRINT 'PA999_TABLE_META 생성 완료';
END
GO

-- PA999_CHAT_LOG (채팅 로그)
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'PA999_CHAT_LOG')
BEGIN
    CREATE TABLE PA999_CHAT_LOG (
        LOG_SEQ         BIGINT        NOT NULL IDENTITY(1,1) PRIMARY KEY,
        SESSION_ID      NVARCHAR(100) NULL,
        USER_ID         NVARCHAR(50)  NULL,
        USER_QUERY      NVARCHAR(2000) NOT NULL,
        AI_RESPONSE     NVARCHAR(MAX) NULL,
        GENERATED_SQL   NVARCHAR(MAX) NULL,
        RELATED_TABLES  NVARCHAR(1000) NULL,
        IS_ERROR        BIT           NOT NULL DEFAULT 0,
        PERF_SCORE      TINYINT       NULL,         -- 1~5
        DEV_FEEDBACK    NVARCHAR(2000) NULL,
        FEEDBACK_DT     DATETIME      NULL,
        FEEDBACK_BY     NVARCHAR(50)  NULL,
        FEEDBACK_TYPE   CHAR(1)       NULL DEFAULT 'D',  -- D=개발자, U=사용자
        CREATED_DT      DATETIME      NOT NULL DEFAULT GETDATE()
    );
    CREATE INDEX IX_PA999_CHAT_LOG_SESSION
        ON PA999_CHAT_LOG(SESSION_ID);
    CREATE INDEX IX_PA999_CHAT_LOG_DT
        ON PA999_CHAT_LOG(CREATED_DT DESC);
    PRINT 'PA999_CHAT_LOG 생성 완료';
END
GO

-- PA999_FEEDBACK_PATTERN (피드백 패턴 — RAG 임베딩)
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'PA999_FEEDBACK_PATTERN')
BEGIN
    CREATE TABLE PA999_FEEDBACK_PATTERN (
        PATTERN_SEQ     INT           NOT NULL IDENTITY(1,1) PRIMARY KEY,
        LOG_SEQ         BIGINT        NULL,
        QUERY_PATTERN   NVARCHAR(500) NOT NULL,
        WRONG_APPROACH  NVARCHAR(1000) NULL,
        CORRECT_SQL     NVARCHAR(MAX) NULL,
        LESSON          NVARCHAR(1000) NOT NULL,
        PRIORITY        TINYINT       NOT NULL DEFAULT 5,  -- 1=최고 ~ 10=최저
        APPLY_YN        CHAR(1)       NOT NULL DEFAULT 'Y',
        PREFERRED_MODE  NVARCHAR(10)  NULL,        -- SP, SQL, SOP
        EMBEDDING       NVARCHAR(MAX) NULL,        -- JSON float[] (RAG 벡터)
        INSRT_USER_ID   NVARCHAR(50)  NULL,
        INSRT_DT        DATETIME      NOT NULL DEFAULT GETDATE(),
        UPDT_USER_ID    NVARCHAR(50)  NULL,
        UPDT_DT         DATETIME      NOT NULL DEFAULT GETDATE()
    );
    PRINT 'PA999_FEEDBACK_PATTERN 생성 완료';
END
GO

PRINT '══ 전체 테이블 생성 완료 ══';
GO
