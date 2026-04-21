-- ============================================================
-- PA999 SP/SOP Mode Demo Seed
-- 3분기 라우팅(Mode A: SP, Mode B: SQL, Mode C: SOP) 전체 시연용
--   - 메타 테이블 4종 생성 (CATALOG / PARAMS / SOP / ERROR_MAP)
--   - 데모 Stored Procedure 3개 생성
--   - CATALOG/PARAMS/SOP/ERROR_MAP 시드 삽입
-- ============================================================
USE PA999_DEMO;
GO

-- ────────────────────────────────────────────────────────────
-- ⓪ B_PLANT.PLANT_GUBUN_CD 자동 채움 (04에서 ALTER로 추가된 컬럼)
--   매번 startup 시 REMICON/CEMENT/REMITAL 구분코드 정규화
-- ────────────────────────────────────────────────────────────
IF COL_LENGTH('B_PLANT', 'PLANT_GUBUN_CD') IS NOT NULL
BEGIN
    UPDATE B_PLANT SET PLANT_GUBUN_CD = '300' WHERE PLANT_TYPE = 'REMICON' AND (PLANT_GUBUN_CD IS NULL OR PLANT_GUBUN_CD = '');
    UPDATE B_PLANT SET PLANT_GUBUN_CD = '100' WHERE PLANT_TYPE = 'CEMENT'  AND (PLANT_GUBUN_CD IS NULL OR PLANT_GUBUN_CD = '');
    UPDATE B_PLANT SET PLANT_GUBUN_CD = '400' WHERE PLANT_TYPE = 'REMITAL' AND (PLANT_GUBUN_CD IS NULL OR PLANT_GUBUN_CD = '');
    PRINT 'B_PLANT.PLANT_GUBUN_CD 정규화 완료';
END
GO

-- ────────────────────────────────────────────────────────────
-- ① 메타 테이블 생성
-- ────────────────────────────────────────────────────────────
IF OBJECT_ID('PA999_SP_CATALOG', 'U') IS NULL
BEGIN
    CREATE TABLE PA999_SP_CATALOG
    (
        SP_ID            INT IDENTITY(1,1) PRIMARY KEY,
        SP_NAME          NVARCHAR(128) NOT NULL,
        SP_DESC          NVARCHAR(500) NULL,
        KEYWORD_LIST     NVARCHAR(1000) NULL,
        DIRECT_KEYWORDS  NVARCHAR(500) NULL,
        MODULE_CD        NVARCHAR(20) NULL,
        CATEGORY         NVARCHAR(50) NULL,
        SP_TYPE          NVARCHAR(20) NULL,      -- R=Report, X=Action
        USE_YN           CHAR(1) NOT NULL DEFAULT 'Y',
        INSRT_DT         DATETIME NOT NULL DEFAULT GETDATE()
    );
    PRINT 'PA999_SP_CATALOG 생성';
END
GO

IF OBJECT_ID('PA999_SP_PARAMS', 'U') IS NULL
BEGIN
    CREATE TABLE PA999_SP_PARAMS
    (
        SP_ID         INT NOT NULL,
        PARAM_SEQ     INT NOT NULL,
        PARAM_NAME    NVARCHAR(64) NOT NULL,
        PARAM_DESC    NVARCHAR(200) NULL,
        DATA_TYPE     NVARCHAR(20) NOT NULL DEFAULT 'NVARCHAR',
        MAX_LENGTH    INT NOT NULL DEFAULT 50,
        IS_REQUIRED   CHAR(1) NOT NULL DEFAULT 'N',
        IS_OUTPUT     CHAR(1) NOT NULL DEFAULT 'N',
        DEFAULT_VAL   NVARCHAR(100) NULL,
        MAPPING_HINT  NVARCHAR(500) NULL,
        USE_YN        CHAR(1) NOT NULL DEFAULT 'Y',
        CONSTRAINT PK_PA999_SP_PARAMS PRIMARY KEY (SP_ID, PARAM_SEQ)
    );
    PRINT 'PA999_SP_PARAMS 생성';
END
GO

IF OBJECT_ID('PA999_SP_SOP', 'U') IS NULL
BEGIN
    CREATE TABLE PA999_SP_SOP
    (
        SOP_ID        INT IDENTITY(1,1) PRIMARY KEY,
        SP_ID         INT NULL,                  -- NULL=범용 SOP
        ERROR_TYPE    NVARCHAR(50) NULL,
        CAUSE_DESC    NVARCHAR(1000) NULL,
        ACTION_GUIDE  NVARCHAR(4000) NULL,
        MENU_PATH     NVARCHAR(200) NULL,
        KEYWORD_LIST  NVARCHAR(1000) NULL,
        SEVERITY      NVARCHAR(10) NULL,         -- LOW/MED/HIGH
        USE_YN        CHAR(1) NOT NULL DEFAULT 'Y',
        INSRT_DT      DATETIME NOT NULL DEFAULT GETDATE()
    );
    PRINT 'PA999_SP_SOP 생성';
END
GO

IF OBJECT_ID('PA999_SP_ERROR_MAP', 'U') IS NULL
BEGIN
    CREATE TABLE PA999_SP_ERROR_MAP
    (
        ERROR_MAP_ID  INT IDENTITY(1,1) PRIMARY KEY,
        ERROR_TYPE    NVARCHAR(50) NOT NULL,
        ERROR_DESC    NVARCHAR(500) NULL,
        BIZ_PROCESS   NVARCHAR(200) NULL,
        SP_ID         INT NULL,
        DIAG_TABLES   NVARCHAR(500) NULL,
        KEYWORD_LIST  NVARCHAR(1000) NULL,
        PRIORITY      INT NOT NULL DEFAULT 100,
        USE_YN        CHAR(1) NOT NULL DEFAULT 'Y'
    );
    PRINT 'PA999_SP_ERROR_MAP 생성';
END
GO

-- ────────────────────────────────────────────────────────────
-- ② 데모 Stored Procedure 생성
--   실제 UNIERP SP 구조(다중 결과셋 반환)를 간소화한 데모 버전
-- ────────────────────────────────────────────────────────────

-- SP1: 생산일보 (공장코드 + 생산일자 → 호기별 생산실적)
IF OBJECT_ID('PA999_DEMO_SP_PROD_DAILY_R', 'P') IS NOT NULL
    DROP PROCEDURE PA999_DEMO_SP_PROD_DAILY_R;
GO
CREATE PROCEDURE PA999_DEMO_SP_PROD_DAILY_R
    @P_PLANT_CD NVARCHAR(10),
    @P_PROD_DT  CHAR(8)
AS
BEGIN
    SET NOCOUNT ON;

    -- 결과셋 1: 타이틀/헤더
    SELECT
        N'📋 ' + ISNULL(p.PLANT_NM, @P_PLANT_CD) + N' 생산일보 (' + @P_PROD_DT + N')' AS TITLE,
        @P_PLANT_CD AS PLANT_CD,
        @P_PROD_DT  AS PROD_DT
    FROM (SELECT 1 AS X) d
    LEFT JOIN B_PLANT p WITH (NOLOCK) ON p.PLANT_CD = @P_PLANT_CD;

    -- 결과셋 2: 호기별 생산 실적
    SELECT
        h.WC_CD                         AS 작업장,
        w.WC_NM                         AS 작업장명,
        h.ITEM_CD                       AS 품목코드,
        CAST(h.PROD_QTY AS DECIMAL(18,2)) AS 생산량,
        CAST(h.PLAN_QTY AS DECIMAL(18,2)) AS 계획량,
        CAST(
            CASE WHEN ISNULL(h.PLAN_QTY,0)=0 THEN 0
                 ELSE h.PROD_QTY * 100.0 / h.PLAN_QTY END
        AS DECIMAL(5,1))                AS 달성률_PCT,
        h.DAY_MAGAM_YN                  AS 일마감YN
    FROM P_PROD_DAILY_HDR_CKO087 h WITH (NOLOCK)
    LEFT JOIN P_WORK_CENTER w WITH (NOLOCK)
        ON w.PLANT_CD = h.PLANT_CD AND w.WC_CD = h.WC_CD
    WHERE h.PLANT_CD = @P_PLANT_CD
      AND h.PROD_DT  = @P_PROD_DT
    ORDER BY h.WC_CD;
END
GO

-- SP2: 레미콘 월보 (공장코드 optional + YYYYMM → 공장별 월 집계)
IF OBJECT_ID('PA999_DEMO_SP_REMICON_MONTHLY_R', 'P') IS NOT NULL
    DROP PROCEDURE PA999_DEMO_SP_REMICON_MONTHLY_R;
GO
CREATE PROCEDURE PA999_DEMO_SP_REMICON_MONTHLY_R
    @P_YYYYMM   CHAR(6),
    @P_PLANT_CD NVARCHAR(10) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Start CHAR(8) = @P_YYYYMM + '01';
    DECLARE @End   CHAR(8) = CONVERT(CHAR(8), EOMONTH(CAST(@Start AS DATE)), 112);

    -- 결과셋 1: 타이틀
    SELECT
        N'🚚 레미콘 월보 ('
        + LEFT(@P_YYYYMM,4) + N'-' + RIGHT(@P_YYYYMM,2) + N')'
        + CASE WHEN @P_PLANT_CD IS NULL THEN N' [전체공장]'
               ELSE N' [' + ISNULL((SELECT PLANT_NM FROM B_PLANT WHERE PLANT_CD=@P_PLANT_CD), @P_PLANT_CD) + N']'
          END AS TITLE;

    -- 결과셋 2: 공장별 월간 집계
    SELECT
        h.PLANT_CD                              AS 공장코드,
        p.PLANT_NM                              AS 공장명,
        SUM(ISNULL(h.PROD_QTY,0))              AS 생산량_M3,
        SUM(ISNULL(h.PLAN_QTY,0))              AS 계획량_M3,
        SUM(ISNULL(h.TRUCK_CNT,0))             AS 출하대수,
        CAST(
            SUM(ISNULL(h.PROD_QTY,0))
            / NULLIF(SUM(ISNULL(h.PLAN_QTY,0)),0) * 100
        AS DECIMAL(5,1))                        AS 달성률_PCT,
        RANK() OVER (ORDER BY SUM(ISNULL(h.PROD_QTY,0)) DESC) AS 순위
    FROM P_PROD_REMICON_HDR_CKO087 h WITH (NOLOCK)
    INNER JOIN B_PLANT p WITH (NOLOCK) ON p.PLANT_CD = h.PLANT_CD
    WHERE h.PROD_DT BETWEEN @Start AND @End
      AND h.DAY_MAGAM_YN = 'Y'
      AND (@P_PLANT_CD IS NULL OR h.PLANT_CD = @P_PLANT_CD)
    GROUP BY h.PLANT_CD, p.PLANT_NM
    ORDER BY 생산량_M3 DESC;
END
GO

-- SP3: 공장 마스터 리스트 (PLANT_TYPE 필터)
IF OBJECT_ID('PA999_DEMO_SP_PLANT_LIST_R', 'P') IS NOT NULL
    DROP PROCEDURE PA999_DEMO_SP_PLANT_LIST_R;
GO
CREATE PROCEDURE PA999_DEMO_SP_PLANT_LIST_R
    @P_PLANT_TYPE NVARCHAR(20) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        N'🏭 공장 마스터 ('
        + CASE WHEN @P_PLANT_TYPE IS NULL THEN N'전체'
               ELSE @P_PLANT_TYPE END
        + N')' AS TITLE;

    SELECT
        PLANT_CD                 AS 공장코드,
        PLANT_NM                 AS 공장명,
        PLANT_TYPE               AS 공장유형,
        PLANT_GUBUN_CD           AS 구분코드,
        BIZ_AREA_CD              AS 사업영역,
        REGION_NM                AS 지역
    FROM B_PLANT WITH (NOLOCK)
    WHERE USE_YN = 'Y'
      AND (@P_PLANT_TYPE IS NULL OR PLANT_TYPE = @P_PLANT_TYPE)
    ORDER BY PLANT_CD;
END
GO

-- ────────────────────────────────────────────────────────────
-- ③ SP_CATALOG 시드 (3건)
-- ────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM PA999_SP_CATALOG WHERE SP_NAME = 'PA999_DEMO_SP_PROD_DAILY_R')
BEGIN
    INSERT INTO PA999_SP_CATALOG
        (SP_NAME, SP_DESC, KEYWORD_LIST, DIRECT_KEYWORDS, MODULE_CD, CATEGORY, SP_TYPE)
    VALUES
    (
        'PA999_DEMO_SP_PROD_DAILY_R',
        N'일별 공장 생산실적 리포트 — 호기별 생산량/계획/달성률',
        N'생산일보,일보,일일생산,일별생산,일별생산실적,생산현황,일보출력,호기별,공장일보',
        N'생산일보,일일생산일보,공장생산일보',
        N'PA999', N'PROD_REPORT', 'R'
    );
    PRINT 'SP_CATALOG: PA999_DEMO_SP_PROD_DAILY_R 등록';
END
GO

IF NOT EXISTS (SELECT 1 FROM PA999_SP_CATALOG WHERE SP_NAME = 'PA999_DEMO_SP_REMICON_MONTHLY_R')
BEGIN
    INSERT INTO PA999_SP_CATALOG
        (SP_NAME, SP_DESC, KEYWORD_LIST, DIRECT_KEYWORDS, MODULE_CD, CATEGORY, SP_TYPE)
    VALUES
    (
        'PA999_DEMO_SP_REMICON_MONTHLY_R',
        N'레미콘 월보 — 공장별 월간 생산량/계획/출하대수/순위',
        N'레미콘월보,월보,월별생산,월간생산,레미콘월간,월보리포트,월간레미콘,월생산',
        N'레미콘월보,레미콘 월보,월별레미콘보고서',
        N'PA999', N'PROD_REPORT', 'R'
    );
    PRINT 'SP_CATALOG: PA999_DEMO_SP_REMICON_MONTHLY_R 등록';
END
GO

IF NOT EXISTS (SELECT 1 FROM PA999_SP_CATALOG WHERE SP_NAME = 'PA999_DEMO_SP_PLANT_LIST_R')
BEGIN
    INSERT INTO PA999_SP_CATALOG
        (SP_NAME, SP_DESC, KEYWORD_LIST, DIRECT_KEYWORDS, MODULE_CD, CATEGORY, SP_TYPE)
    VALUES
    (
        'PA999_DEMO_SP_PLANT_LIST_R',
        N'공장 마스터 리포트 — 공장유형별 공장 리스트 출력',
        N'공장마스터,공장기준정보,공장유형,공장분류,공장현황,공장리포트',
        N'공장마스터리포트',
        N'PA999', N'MASTER_REPORT', 'R'
    );
    PRINT 'SP_CATALOG: PA999_DEMO_SP_PLANT_LIST_R 등록';
END
GO

-- ────────────────────────────────────────────────────────────
-- ④ SP_PARAMS 시드
-- ────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM PA999_SP_PARAMS WHERE SP_ID = (SELECT SP_ID FROM PA999_SP_CATALOG WHERE SP_NAME='PA999_DEMO_SP_PROD_DAILY_R'))
BEGIN
    DECLARE @sp1 INT = (SELECT SP_ID FROM PA999_SP_CATALOG WHERE SP_NAME='PA999_DEMO_SP_PROD_DAILY_R');
    INSERT INTO PA999_SP_PARAMS (SP_ID, PARAM_SEQ, PARAM_NAME, PARAM_DESC, DATA_TYPE, MAX_LENGTH, IS_REQUIRED, IS_OUTPUT, MAPPING_HINT) VALUES
    (@sp1, 1, '@P_PLANT_CD', N'공장코드',   'NVARCHAR', 10, 'Y', 'N', N'B_PLANT.PLANT_CD (예: P001 단양공장, P007 성남공장)'),
    (@sp1, 2, '@P_PROD_DT',  N'생산일자',   'CHAR',      8, 'Y', 'N', N'YYYYMMDD 형식 (예: 20260315). 상대표현 금지, 절대일자 필수');
    PRINT 'SP_PARAMS: PROD_DAILY_R 등록';
END
GO

IF NOT EXISTS (SELECT 1 FROM PA999_SP_PARAMS WHERE SP_ID = (SELECT SP_ID FROM PA999_SP_CATALOG WHERE SP_NAME='PA999_DEMO_SP_REMICON_MONTHLY_R'))
BEGIN
    DECLARE @sp2 INT = (SELECT SP_ID FROM PA999_SP_CATALOG WHERE SP_NAME='PA999_DEMO_SP_REMICON_MONTHLY_R');
    INSERT INTO PA999_SP_PARAMS (SP_ID, PARAM_SEQ, PARAM_NAME, PARAM_DESC, DATA_TYPE, MAX_LENGTH, IS_REQUIRED, IS_OUTPUT, MAPPING_HINT) VALUES
    (@sp2, 1, '@P_YYYYMM',   N'조회월',      'CHAR',      6, 'Y', 'N', N'YYYYMM 형식 (예: 202603). 월 단위 필수'),
    (@sp2, 2, '@P_PLANT_CD', N'공장코드',    'NVARCHAR', 10, 'N', 'N', N'공장코드 (생략 시 전체 공장 집계)');
    PRINT 'SP_PARAMS: REMICON_MONTHLY_R 등록';
END
GO

IF NOT EXISTS (SELECT 1 FROM PA999_SP_PARAMS WHERE SP_ID = (SELECT SP_ID FROM PA999_SP_CATALOG WHERE SP_NAME='PA999_DEMO_SP_PLANT_LIST_R'))
BEGIN
    DECLARE @sp3 INT = (SELECT SP_ID FROM PA999_SP_CATALOG WHERE SP_NAME='PA999_DEMO_SP_PLANT_LIST_R');
    INSERT INTO PA999_SP_PARAMS (SP_ID, PARAM_SEQ, PARAM_NAME, PARAM_DESC, DATA_TYPE, MAX_LENGTH, IS_REQUIRED, IS_OUTPUT, MAPPING_HINT) VALUES
    (@sp3, 1, '@P_PLANT_TYPE', N'공장유형', 'NVARCHAR', 20, 'N', 'N', N'REMICON / CEMENT / REMITAL 중 하나 (생략 시 전체)');
    PRINT 'SP_PARAMS: PLANT_LIST_R 등록';
END
GO

-- ────────────────────────────────────────────────────────────
-- ⑤ SP_SOP 시드 (3건 — 범용 절차)
-- ────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM PA999_SP_SOP WHERE ERROR_TYPE = 'CLOSE_CANCEL')
BEGIN
    INSERT INTO PA999_SP_SOP (SP_ID, ERROR_TYPE, CAUSE_DESC, ACTION_GUIDE, MENU_PATH, KEYWORD_LIST, SEVERITY) VALUES
    (NULL, 'CLOSE_CANCEL',
     N'일마감(DAY_MAGAM_YN=Y) 상태에서는 생산실적을 수정할 수 없습니다. 수정이 필요한 경우 반드시 일마감을 취소해야 합니다.',
     N'## 🔧 일마감 취소 절차' + CHAR(13)+CHAR(10) +
     N'1. **[PP-생산관리] → [생산실적관리] → [일마감 취소]** 메뉴 진입' + CHAR(13)+CHAR(10) +
     N'2. 공장코드, 생산일자, 작업장 선택 후 [조회]' + CHAR(13)+CHAR(10) +
     N'3. 대상 행 선택 후 [마감취소] 버튼 클릭' + CHAR(13)+CHAR(10) +
     N'4. 사유 입력 (필수, 감사 로그 기록) 후 확인' + CHAR(13)+CHAR(10) +
     N'5. DAY_MAGAM_YN이 ''N''으로 변경되었는지 조회로 확인' + CHAR(13)+CHAR(10) +
     N'⚠️ 월마감(MON_MAGAM_YN=Y) 이후에는 일마감 취소 불가 — 월마감 먼저 취소',
     N'PP > 생산관리 > 생산실적관리 > 일마감취소',
     N'일마감,마감취소,일마감취소,마감해제,일마감해제,취소,절차,가이드,방법,순서',
     'MED');
    PRINT 'SP_SOP: CLOSE_CANCEL 등록';
END
GO

IF NOT EXISTS (SELECT 1 FROM PA999_SP_SOP WHERE ERROR_TYPE = 'PLANT_ADD')
BEGIN
    INSERT INTO PA999_SP_SOP (SP_ID, ERROR_TYPE, CAUSE_DESC, ACTION_GUIDE, MENU_PATH, KEYWORD_LIST, SEVERITY) VALUES
    (NULL, 'PLANT_ADD',
     N'신규 공장 추가 시 기준정보 3종(B_PLANT / B_WORK_CENTER / B_STORAGE_LOCATION)을 순서대로 등록해야 생산실적 입력이 가능합니다.',
     N'## 🏭 공장 추가 절차' + CHAR(13)+CHAR(10) +
     N'### 1단계: 공장 마스터 등록' + CHAR(13)+CHAR(10) +
     N'   [BM-기준정보] → [공장 마스터] → [추가]' + CHAR(13)+CHAR(10) +
     N'   - PLANT_CD (사용자 정의, 예: P100)' + CHAR(13)+CHAR(10) +
     N'   - PLANT_NM / PLANT_TYPE (REMICON/CEMENT/REMITAL)' + CHAR(13)+CHAR(10) +
     N'   - PLANT_GUBUN_CD (300=레미콘/100=시멘트/400=레미탈)' + CHAR(13)+CHAR(10) +
     N'### 2단계: 작업장 등록' + CHAR(13)+CHAR(10) +
     N'   [BM] → [작업장 마스터] → 공장별 WC_CD 등록' + CHAR(13)+CHAR(10) +
     N'### 3단계: 저장위치 등록' + CHAR(13)+CHAR(10) +
     N'   [BM] → [저장위치] → SL_CD 등록' + CHAR(13)+CHAR(10) +
     N'### 4단계: 품목-공장 매핑' + CHAR(13)+CHAR(10) +
     N'   [BM] → [품목-공장 BOM] → 생산가능 품목 지정' + CHAR(13)+CHAR(10) +
     N'완료 후 [생산계획] → [생산실적] 입력 가능',
     N'BM > 기준정보 > 공장마스터',
     N'공장추가,공장등록,신규공장,공장생성,공장만들기,공장 추가,절차,가이드,단계,순서',
     'LOW');
    PRINT 'SP_SOP: PLANT_ADD 등록';
END
GO

IF NOT EXISTS (SELECT 1 FROM PA999_SP_SOP WHERE ERROR_TYPE = 'WC_REGISTER')
BEGIN
    INSERT INTO PA999_SP_SOP (SP_ID, ERROR_TYPE, CAUSE_DESC, ACTION_GUIDE, MENU_PATH, KEYWORD_LIST, SEVERITY) VALUES
    (NULL, 'WC_REGISTER',
     N'작업장(Work Center) 등록은 공장 등록 이후에 가능하며, 작업장별 가동시간/생산능력이 설정되어야 생산계획 수립이 가능합니다.',
     N'## ⚙️ 작업장 등록 절차' + CHAR(13)+CHAR(10) +
     N'1. **[BM-기준정보] → [작업장 마스터]** 메뉴 진입' + CHAR(13)+CHAR(10) +
     N'2. 상단 [공장 선택] 후 [신규]' + CHAR(13)+CHAR(10) +
     N'3. WC_CD(예: RM1, BM1), WC_NM, WC_GROUP_CD 입력' + CHAR(13)+CHAR(10) +
     N'4. 1일 가동시간(EFF_HOURS), 시간당 CAPA(CAPA_PER_HOUR) 입력' + CHAR(13)+CHAR(10) +
     N'5. 주 품목코드(ITEM_CD) 연결' + CHAR(13)+CHAR(10) +
     N'6. [저장] → 다음 단계: [작업장-품목 BOM] 등록',
     N'BM > 기준정보 > 작업장마스터',
     N'작업장등록,작업장추가,작업장 등록,호기등록,호기추가,라인등록,절차,가이드,단계,순서,방법',
     'LOW');
    PRINT 'SP_SOP: WC_REGISTER 등록';
END
GO

-- ────────────────────────────────────────────────────────────
-- ⑥ SP_ERROR_MAP 시드 (2건)
-- ────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM PA999_SP_ERROR_MAP WHERE ERROR_TYPE = 'NO_PROD_QTY')
BEGIN
    DECLARE @sp_prod INT = (SELECT SP_ID FROM PA999_SP_CATALOG WHERE SP_NAME='PA999_DEMO_SP_PROD_DAILY_R');
    INSERT INTO PA999_SP_ERROR_MAP
        (ERROR_TYPE, ERROR_DESC, BIZ_PROCESS, SP_ID, DIAG_TABLES, KEYWORD_LIST, PRIORITY) VALUES
    ('NO_PROD_QTY',
     N'생산량이 0 또는 NULL로 조회되는 문제',
     N'생산실적 입력/조회',
     @sp_prod,
     N'P_PROD_DAILY_HDR_CKO087,P_PROD_DAILY_DTL_CKO087,P_PROD_AUTO_CLOSE_CKO087',
     N'생산량,0,누락,없어,빠졌,안나와,안 나와,뜨는,떠요,나와,미입력,안 됐',
     10);
    PRINT 'ERROR_MAP: NO_PROD_QTY 등록';
END
GO

IF NOT EXISTS (SELECT 1 FROM PA999_SP_ERROR_MAP WHERE ERROR_TYPE = 'CLOSE_FAIL')
BEGIN
    INSERT INTO PA999_SP_ERROR_MAP
        (ERROR_TYPE, ERROR_DESC, BIZ_PROCESS, SP_ID, DIAG_TABLES, KEYWORD_LIST, PRIORITY) VALUES
    ('CLOSE_FAIL',
     N'일마감 처리가 실패하거나 상태값이 갱신되지 않는 문제',
     N'일마감 처리',
     NULL,
     N'P_PROD_AUTO_CLOSE_CKO087,P_PROD_AUTO_CLOSE_DTL_CKO087,I_ONHAND_STOCK',
     N'마감,일마감,안됨,안 됨,실패,에러,오류,문제,자동마감,마감오류',
     20);
    PRINT 'ERROR_MAP: CLOSE_FAIL 등록';
END
GO

PRINT N'✅ SP/SOP Demo Seed 완료';
GO
