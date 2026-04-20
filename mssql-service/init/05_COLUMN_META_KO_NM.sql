-- ══════════════════════════════════════════════════════════════════
-- 05. PA999_COLUMN_META — 전체 테이블 컬럼 한국어 설명 삽입
-- 대상 테이블 (8개):
--   B_PLANT, B_ITEM, P_WORK_CENTER
--   P_PROD_DAILY_HDR_CKO087, P_PROD_DAILY_DTL_CKO087
--   P_PROD_REMICON_HDR_CKO087, P_PROD_REMICON_DTL_CKO087
--   P_PROD_REMITAL_HDR_CKO087, P_PROD_REMITAL_DTL_CKO087
-- 작성일: 2026-04-20
-- ══════════════════════════════════════════════════════════════════

USE PA999_DEMO;
GO

-- 기존 수동 입력(SRC_TYPE='M') 데이터 전체 초기화
DELETE FROM PA999_COLUMN_META WHERE SRC_TYPE = 'M';
PRINT 'PA999_COLUMN_META 초기화 완료';
GO

-- ──────────────────────────────────────────────────────────────────
-- ■ B_PLANT (공장 마스터)
-- ──────────────────────────────────────────────────────────────────
INSERT INTO PA999_COLUMN_META (TABLE_NM, COLUMN_NM, KO_NM, DATA_TYPE, IS_NULLABLE, BIZ_RULE, CODE_VALUES, USE_YN, SRC_TYPE)
VALUES
('B_PLANT','PLANT_CD',      N'공장코드',       'NVARCHAR(10)', 'N',
 N'공장을 식별하는 유일 코드. 생산실적 테이블의 외래키(FK)',
 N'P001=단양공장|P031=영월공장|P003=대구공장|P004=부산공장|P006=청주공장|P007=성남공장|P008=대전공장|P011=김해공장|P014=서대구공장|P020=서인천공장|P022=화성공장|P025=부천공장|P050=수원레미탈공장|P051=광주레미탈공장',
 'Y','M'),

('B_PLANT','PLANT_NM',      N'공장명',         'NVARCHAR(100)', 'N',
 N'공장 한국어 이름. 조회 결과 출력 시 PLANT_CD와 함께 사용',
 NULL, 'Y','M'),

('B_PLANT','PLANT_TYPE',    N'공장유형',       'NVARCHAR(20)', 'Y',
 N'공장 생산 유형 구분',
 N'CEMENT=시멘트(반복제조)|REMICON=레미콘|REMITAL=레미탈',
 'Y','M'),

('B_PLANT','PLANT_GUBUN_CD',N'공장구분코드',   'NVARCHAR(10)', 'Y',
 N'AI SQL 생성 시 공장 유형별 생산 테이블 분기에 사용하는 핵심 코드. 공장 유형 조건 필터링은 PLANT_TYPE 대신 이 컬럼 사용 권장',
 N'100=반복제조(시멘트공장, P_PROD_DAILY_HDR_CKO087 사용)|200=지역공장(P_PROD_ORDER_HDR_CKO087 사용)|300=레미콘(P_PROD_REMICON_HDR_CKO087 사용)|400=레미탈(P_PROD_REMITAL_HDR_CKO087 사용)',
 'Y','M'),

('B_PLANT','BIZ_AREA_CD',   N'사업영역코드',   'NVARCHAR(10)', 'Y',
 N'공장이 속한 사업 영역(권역) 코드',
 N'BA01=충청권|BA02=영남권|BA03=수도권',
 'Y','M'),

('B_PLANT','REGION_NM',     N'지역명',         'NVARCHAR(50)', 'Y',
 N'공장 소재 시/도 지역 이름 (예: 충북, 강원, 경기)',
 NULL, 'Y','M'),

('B_PLANT','USE_YN',        N'사용여부',       'CHAR(1)', 'N',
 N'공장 활성 여부. 조회 시 항상 USE_YN = ''Y'' 조건 포함',
 N'Y=사용|N=미사용(폐쇄/미운영)', 'Y','M');

PRINT 'B_PLANT 컬럼 메타 삽입 완료';
GO

-- ──────────────────────────────────────────────────────────────────
-- ■ B_ITEM (품목 마스터)
-- ──────────────────────────────────────────────────────────────────
INSERT INTO PA999_COLUMN_META (TABLE_NM, COLUMN_NM, KO_NM, DATA_TYPE, IS_NULLABLE, BIZ_RULE, CODE_VALUES, USE_YN, SRC_TYPE)
VALUES
('B_ITEM','ITEM_CD',        N'품목코드',       'NVARCHAR(20)', 'N',
 N'생산/판매 품목 유일 식별 코드. 생산실적 테이블의 ITEM_CD와 조인',
 N'RC2500=레미콘 25-21-150|RC3000=레미콘 30-24-150|RC3500=레미콘 35-27-120|CM0001=시멘트(포틀랜드)|CM0002=고로슬래그시멘트|RT0001=레미탈(바닥용)',
 'Y','M'),

('B_ITEM','ITEM_NM',        N'품목명',         'NVARCHAR(200)', 'N',
 N'품목 한국어 이름. 규격 정보 포함',
 NULL, 'Y','M'),

('B_ITEM','ITEM_TYPE_CD',   N'품목유형코드',   'NVARCHAR(10)', 'Y',
 N'품목 계열 구분',
 N'CEMENT=시멘트계열|REMICON=레미콘계열|REMITAL=레미탈계열',
 'Y','M'),

('B_ITEM','UNIT',           N'단위',           'NVARCHAR(10)', 'Y',
 N'생산량 기준 단위. 시멘트=TON, 레미콘=M3(입방미터), 레미탈=TON',
 N'TON=톤(시멘트/레미탈)|M3=입방미터(레미콘)|BAG=포대',
 'Y','M'),

('B_ITEM','USE_YN',         N'사용여부',       'CHAR(1)', 'N',
 N'품목 활성 여부',
 N'Y=사용|N=미사용', 'Y','M');

PRINT 'B_ITEM 컬럼 메타 삽입 완료';
GO

-- ──────────────────────────────────────────────────────────────────
-- ■ P_WORK_CENTER (작업장 마스터)
-- ──────────────────────────────────────────────────────────────────
INSERT INTO PA999_COLUMN_META (TABLE_NM, COLUMN_NM, KO_NM, DATA_TYPE, IS_NULLABLE, BIZ_RULE, CODE_VALUES, USE_YN, SRC_TYPE)
VALUES
('P_WORK_CENTER','PLANT_CD',    N'공장코드',       'NVARCHAR(10)', 'N',
 N'B_PLANT.PLANT_CD 외래키. 작업장이 속한 공장',
 NULL, 'Y','M'),

('P_WORK_CENTER','WC_CD',       N'작업장코드',     'NVARCHAR(20)', 'N',
 N'공장 내 생산 라인/호기 유일 코드. 생산실적 테이블의 WC_CD와 조인하여 작업장명 조회',
 N'RM1=1호기|RM2=2호기|RM3=3호기|RC1=믹서1호기|RT1=분쇄1호기',
 'Y','M'),

('P_WORK_CENTER','WC_NM',       N'작업장명',       'NVARCHAR(100)', 'N',
 N'작업장(호기/라인) 한국어 이름. 생산실적 테이블에는 WC_NM이 없으므로 반드시 이 테이블과 LEFT JOIN 필요',
 NULL, 'Y','M'),

('P_WORK_CENTER','WC_GROUP_CD', N'작업장그룹코드', 'NVARCHAR(20)', 'Y',
 N'작업장의 기능 그룹 코드. ''RM%'' LIKE 조건으로 소성/원료 계열 필터링',
 N'RM=소성/원료(반복제조)|CM=시멘트분쇄|RC=레미콘믹서|RT=레미탈분쇄',
 'Y','M'),

('P_WORK_CENTER','WC_GROUP_NM', N'작업장그룹명',   'NVARCHAR(100)', 'Y',
 N'작업장그룹 한국어 이름',
 NULL, 'Y','M'),

('P_WORK_CENTER','USE_YN',      N'사용여부',       'CHAR(1)', 'N',
 N'작업장 활성 여부. 조회 시 USE_YN = ''Y'' 권장',
 N'Y=사용|N=미사용', 'Y','M');

PRINT 'P_WORK_CENTER 컬럼 메타 삽입 완료';
GO

-- ──────────────────────────────────────────────────────────────────
-- ■ P_PROD_DAILY_HDR_CKO087 (반복제조 생산실적 헤더)
-- ──────────────────────────────────────────────────────────────────
INSERT INTO PA999_COLUMN_META (TABLE_NM, COLUMN_NM, KO_NM, DATA_TYPE, IS_NULLABLE, BIZ_RULE, CODE_VALUES, USE_YN, SRC_TYPE)
VALUES
('P_PROD_DAILY_HDR_CKO087','PLANT_CD',         N'공장코드',      'NVARCHAR(10)', 'N',
 N'반복제조(시멘트) 공장 코드. PLANT_GUBUN_CD=100인 공장만 이 테이블에 실적 존재 (P001=단양, P031=영월)',
 NULL, 'Y','M'),

('P_PROD_DAILY_HDR_CKO087','PROD_DT',          N'생산일자',      'CHAR(8)', 'N',
 N'생산 실적 일자. YYYYMMDD 형식 문자열. 기간 조건: BETWEEN ''20260301'' AND ''20260331''. 이번달: BETWEEN CONVERT(CHAR(6),GETDATE(),112)+''01'' AND CONVERT(CHAR(8),GETDATE(),112)',
 NULL, 'Y','M'),

('P_PROD_DAILY_HDR_CKO087','WC_CD',            N'작업장코드',    'NVARCHAR(20)', 'Y',
 N'호기/라인 코드. P_WORK_CENTER.WC_CD와 LEFT JOIN하여 WC_NM(작업장명) 조회. 생산량 집계 시 GROUP BY PLANT_CD, WC_CD 가능',
 NULL, 'Y','M'),

('P_PROD_DAILY_HDR_CKO087','WC_GROUP_CD',      N'작업장그룹코드','NVARCHAR(20)', 'Y',
 N'소성/원료 라인 필터 시 WC_GROUP_CD LIKE ''RM%'' 조건 사용',
 N'RM=소성/원료계열|CM=분쇄계열', 'Y','M'),

('P_PROD_DAILY_HDR_CKO087','ITEM_CD',          N'품목코드',      'NVARCHAR(20)', 'Y',
 N'생산된 품목 코드. B_ITEM과 조인하여 품목명/단위 조회 가능',
 NULL, 'Y','M'),

('P_PROD_DAILY_HDR_CKO087','PROD_QTY',         N'생산량(TON)',   'DECIMAL(18,2)', 'Y',
 N'해당 일자 해당 작업장의 실제 생산 톤수. 합계는 SUM(PROD_QTY), 집계 시 DAY_MAGAM_YN=''Y'' 조건 필수',
 NULL, 'Y','M'),

('P_PROD_DAILY_HDR_CKO087','PLAN_QTY',         N'계획량(TON)',   'DECIMAL(18,2)', 'Y',
 N'생산 목표 톤수. 달성률 = PROD_QTY / NULLIF(PLAN_QTY,0) * 100',
 NULL, 'Y','M'),

('P_PROD_DAILY_HDR_CKO087','FAC_WORK_TIME_MM', N'가동시간(분)',  'INT', 'Y',
 N'설비 실가동 시간(분 단위). 시간으로 변환: FAC_WORK_TIME_MM / 60.0. 임의 단위 변환 금지 — 사용자 요청 없으면 분 단위 그대로 출력',
 NULL, 'Y','M'),

('P_PROD_DAILY_HDR_CKO087','DAY_MAGAM_YN',     N'일마감여부',    'CHAR(1)', 'N',
 N'생산 실적 확정 여부. 생산량 조회 시 반드시 DAY_MAGAM_YN = ''Y'' 조건 포함. N인 데이터는 잠정(미확정) 수치로 신뢰 불가',
 N'Y=마감완료(확정실적)|N=미마감(잠정/수정가능)', 'Y','M');

PRINT 'P_PROD_DAILY_HDR_CKO087 컬럼 메타 삽입 완료';
GO

-- ──────────────────────────────────────────────────────────────────
-- ■ P_PROD_DAILY_DTL_CKO087 (반복제조 생산실적 상세)
-- ──────────────────────────────────────────────────────────────────
INSERT INTO PA999_COLUMN_META (TABLE_NM, COLUMN_NM, KO_NM, DATA_TYPE, IS_NULLABLE, BIZ_RULE, CODE_VALUES, USE_YN, SRC_TYPE)
VALUES
('P_PROD_DAILY_DTL_CKO087','PLANT_CD',     N'공장코드',       'NVARCHAR(10)', 'N',
 N'반복제조 공장 코드',
 NULL, 'Y','M'),

('P_PROD_DAILY_DTL_CKO087','PROD_DT',      N'생산일자',       'CHAR(8)', 'N',
 N'YYYYMMDD. 헤더(HDR)와 동일 조건으로 조회',
 NULL, 'Y','M'),

('P_PROD_DAILY_DTL_CKO087','WC_CD',        N'작업장코드',     'NVARCHAR(20)', 'Y',
 N'호기/라인 코드. HDR.WC_CD와 동일 기준',
 NULL, 'Y','M'),

('P_PROD_DAILY_DTL_CKO087','ITEM_CD',      N'품목코드',       'NVARCHAR(20)', 'Y',
 N'상세 품목 코드. 복수 품목 혼합 생산 시 품목별 분리 집계에 사용',
 NULL, 'Y','M'),

('P_PROD_DAILY_DTL_CKO087','PROD_QTY',     N'생산량(TON)',    'DECIMAL(18,2)', 'Y',
 N'품목별 세부 생산 톤수',
 NULL, 'Y','M'),

('P_PROD_DAILY_DTL_CKO087','LOSS_QTY',     N'손실량(TON)',    'DECIMAL(18,2)', 'Y',
 N'생산 과정 중 발생한 손실/불량 톤수. 수율 = (PROD_QTY / (PROD_QTY+LOSS_QTY)) * 100',
 NULL, 'Y','M'),

('P_PROD_DAILY_DTL_CKO087','RAW_MAT_QTY',  N'원료투입량(TON)','DECIMAL(18,2)', 'Y',
 N'생산에 투입된 원료 총 톤수. 원료 소비 분석 시 사용',
 NULL, 'Y','M'),

('P_PROD_DAILY_DTL_CKO087','DAY_MAGAM_YN', N'일마감여부',     'CHAR(1)', 'N',
 N'확정 여부. 조회 시 Y 조건 필수',
 N'Y=마감완료|N=미마감', 'Y','M');

PRINT 'P_PROD_DAILY_DTL_CKO087 컬럼 메타 삽입 완료';
GO

-- ──────────────────────────────────────────────────────────────────
-- ■ P_PROD_REMICON_HDR_CKO087 (레미콘 생산실적 헤더)
-- ──────────────────────────────────────────────────────────────────
INSERT INTO PA999_COLUMN_META (TABLE_NM, COLUMN_NM, KO_NM, DATA_TYPE, IS_NULLABLE, BIZ_RULE, CODE_VALUES, USE_YN, SRC_TYPE)
VALUES
('P_PROD_REMICON_HDR_CKO087','PLANT_CD',   N'공장코드',       'NVARCHAR(10)', 'N',
 N'레미콘 공장 코드. PLANT_GUBUN_CD=300인 공장(P003~P025)의 실적',
 NULL, 'Y','M'),

('P_PROD_REMICON_HDR_CKO087','PROD_DT',    N'생산일자',       'CHAR(8)', 'N',
 N'YYYYMMDD. 레미콘은 주말(토/일) 생산량 0 또는 미기록',
 NULL, 'Y','M'),

('P_PROD_REMICON_HDR_CKO087','WC_CD',      N'믹서코드',       'NVARCHAR(20)', 'Y',
 N'레미콘 믹서(혼합기) 코드. P_WORK_CENTER와 JOIN으로 믹서명 조회 가능',
 NULL, 'Y','M'),

('P_PROD_REMICON_HDR_CKO087','MIX_NO',     N'배합번호',       'NVARCHAR(20)', 'Y',
 N'레미콘 배합설계 번호. 같은 날 다른 배합으로 생산 시 복수 행 존재',
 NULL, 'Y','M'),

('P_PROD_REMICON_HDR_CKO087','ITEM_CD',    N'품목코드',       'NVARCHAR(20)', 'Y',
 N'레미콘 규격 코드. RC2500(25-21-150), RC3000(30-24-150), RC3500(35-27-120) 등',
 NULL, 'Y','M'),

('P_PROD_REMICON_HDR_CKO087','PROD_QTY',   N'생산량(M3)',     'DECIMAL(18,2)', 'Y',
 N'레미콘 생산량. 단위는 M3(입방미터). TON이 아님. 집계 시 SUM(PROD_QTY), DAY_MAGAM_YN=''Y'' 조건 필수',
 NULL, 'Y','M'),

('P_PROD_REMICON_HDR_CKO087','PLAN_QTY',   N'계획량(M3)',     'DECIMAL(18,2)', 'Y',
 N'레미콘 생산 목표량(M3)',
 NULL, 'Y','M'),

('P_PROD_REMICON_HDR_CKO087','TRUCK_CNT',  N'출하차량수',     'INT', 'Y',
 N'해당 배합의 레미콘 출하 트럭 대수. 출하 현황 분석 시 활용',
 NULL, 'Y','M'),

('P_PROD_REMICON_HDR_CKO087','DAY_MAGAM_YN',N'일마감여부',    'CHAR(1)', 'N',
 N'생산 실적 확정 여부. 반드시 DAY_MAGAM_YN = ''Y'' 조건 포함',
 N'Y=마감완료|N=미마감', 'Y','M');

PRINT 'P_PROD_REMICON_HDR_CKO087 컬럼 메타 삽입 완료';
GO

-- ──────────────────────────────────────────────────────────────────
-- ■ P_PROD_REMICON_DTL_CKO087 (레미콘 생산실적 상세)
-- ──────────────────────────────────────────────────────────────────
INSERT INTO PA999_COLUMN_META (TABLE_NM, COLUMN_NM, KO_NM, DATA_TYPE, IS_NULLABLE, BIZ_RULE, CODE_VALUES, USE_YN, SRC_TYPE)
VALUES
('P_PROD_REMICON_DTL_CKO087','PLANT_CD',      N'공장코드',     'NVARCHAR(10)', 'N',
 N'레미콘 공장 코드',
 NULL, 'Y','M'),

('P_PROD_REMICON_DTL_CKO087','PROD_DT',       N'생산일자',     'CHAR(8)', 'N',
 N'YYYYMMDD',
 NULL, 'Y','M'),

('P_PROD_REMICON_DTL_CKO087','WC_CD',         N'믹서코드',     'NVARCHAR(20)', 'Y',
 N'레미콘 믹서 코드',
 NULL, 'Y','M'),

('P_PROD_REMICON_DTL_CKO087','MIX_NO',        N'배합번호',     'NVARCHAR(20)', 'Y',
 N'레미콘 배합설계 번호',
 NULL, 'Y','M'),

('P_PROD_REMICON_DTL_CKO087','DELIVERY_CD',   N'납품처코드',   'NVARCHAR(20)', 'Y',
 N'레미콘 출하 납품처(현장) 코드. 납품처별 출하량 집계 시 활용',
 NULL, 'Y','M'),

('P_PROD_REMICON_DTL_CKO087','PROD_QTY',      N'생산량(M3)',   'DECIMAL(18,2)', 'Y',
 N'납품처별 출하량(M3)',
 NULL, 'Y','M'),

('P_PROD_REMICON_DTL_CKO087','DAY_MAGAM_YN',  N'일마감여부',   'CHAR(1)', 'N',
 N'조회 시 Y 조건 필수',
 N'Y=마감완료|N=미마감', 'Y','M');

PRINT 'P_PROD_REMICON_DTL_CKO087 컬럼 메타 삽입 완료';
GO

-- ──────────────────────────────────────────────────────────────────
-- ■ P_PROD_REMITAL_HDR_CKO087 (레미탈 생산실적 헤더)
-- ──────────────────────────────────────────────────────────────────
INSERT INTO PA999_COLUMN_META (TABLE_NM, COLUMN_NM, KO_NM, DATA_TYPE, IS_NULLABLE, BIZ_RULE, CODE_VALUES, USE_YN, SRC_TYPE)
VALUES
('P_PROD_REMITAL_HDR_CKO087','PLANT_CD',   N'공장코드',       'NVARCHAR(10)', 'N',
 N'레미탈 공장 코드. PLANT_GUBUN_CD=400인 공장(P050=수원레미탈, P051=광주레미탈)',
 NULL, 'Y','M'),

('P_PROD_REMITAL_HDR_CKO087','PROD_DT',    N'생산일자',       'CHAR(8)', 'N',
 N'YYYYMMDD. 주말 생산 없음(0)',
 NULL, 'Y','M'),

('P_PROD_REMITAL_HDR_CKO087','WC_CD',      N'작업장코드',     'NVARCHAR(20)', 'Y',
 N'레미탈 분쇄/혼합 라인 코드',
 NULL, 'Y','M'),

('P_PROD_REMITAL_HDR_CKO087','ITEM_CD',    N'품목코드',       'NVARCHAR(20)', 'Y',
 N'레미탈 품목 코드. B_ITEM.ITEM_TYPE_CD=''REMITAL'' 품목',
 NULL, 'Y','M'),

('P_PROD_REMITAL_HDR_CKO087','PROD_QTY',   N'생산량(TON)',    'DECIMAL(18,2)', 'Y',
 N'레미탈 생산량. 단위 TON. 집계 시 SUM(PROD_QTY), DAY_MAGAM_YN=''Y'' 조건 필수',
 NULL, 'Y','M'),

('P_PROD_REMITAL_HDR_CKO087','PLAN_QTY',   N'계획량(TON)',    'DECIMAL(18,2)', 'Y',
 N'레미탈 생산 목표량(TON)',
 NULL, 'Y','M'),

('P_PROD_REMITAL_HDR_CKO087','DAY_MAGAM_YN',N'일마감여부',    'CHAR(1)', 'N',
 N'확정 여부. 조회 시 Y 조건 필수',
 N'Y=마감완료|N=미마감', 'Y','M');

PRINT 'P_PROD_REMITAL_HDR_CKO087 컬럼 메타 삽입 완료';
GO

-- ──────────────────────────────────────────────────────────────────
-- ■ P_PROD_REMITAL_DTL_CKO087 (레미탈 생산실적 상세)
-- ──────────────────────────────────────────────────────────────────
INSERT INTO PA999_COLUMN_META (TABLE_NM, COLUMN_NM, KO_NM, DATA_TYPE, IS_NULLABLE, BIZ_RULE, CODE_VALUES, USE_YN, SRC_TYPE)
VALUES
('P_PROD_REMITAL_DTL_CKO087','PLANT_CD',      N'공장코드',     'NVARCHAR(10)', 'N',
 N'레미탈 공장 코드',
 NULL, 'Y','M'),

('P_PROD_REMITAL_DTL_CKO087','PROD_DT',       N'생산일자',     'CHAR(8)', 'N',
 N'YYYYMMDD',
 NULL, 'Y','M'),

('P_PROD_REMITAL_DTL_CKO087','WC_CD',         N'작업장코드',   'NVARCHAR(20)', 'Y',
 N'레미탈 작업장 코드',
 NULL, 'Y','M'),

('P_PROD_REMITAL_DTL_CKO087','ITEM_CD',       N'품목코드',     'NVARCHAR(20)', 'Y',
 N'레미탈 세부 품목 코드',
 NULL, 'Y','M'),

('P_PROD_REMITAL_DTL_CKO087','PROD_QTY',      N'생산량(TON)',  'DECIMAL(18,2)', 'Y',
 N'품목별 세부 생산 톤수',
 NULL, 'Y','M'),

('P_PROD_REMITAL_DTL_CKO087','LOSS_QTY',      N'손실량(TON)',  'DECIMAL(18,2)', 'Y',
 N'생산 중 발생한 손실 톤수',
 NULL, 'Y','M'),

('P_PROD_REMITAL_DTL_CKO087','DAY_MAGAM_YN',  N'일마감여부',   'CHAR(1)', 'N',
 N'조회 시 Y 조건 필수',
 N'Y=마감완료|N=미마감', 'Y','M');

PRINT 'P_PROD_REMITAL_DTL_CKO087 컬럼 메타 삽입 완료';
GO

-- ──────────────────────────────────────────────────────────────────
-- PA999_TABLE_META — CKO087 테이블 메타 추가/갱신
-- ──────────────────────────────────────────────────────────────────
MERGE INTO PA999_TABLE_META AS T
USING (VALUES
  ('P_PROD_DAILY_HDR_CKO087',
   N'반복제조(시멘트) 생산실적 헤더 — 단양/영월 공장 일별/호기별 생산량, 가동시간, 일마감 여부',
   N'생산,생산실적,생산량,시멘트,반복제조,일별생산,단양,영월,가동시간,호기,마감,DAY_MAGAM_YN'),

  ('P_PROD_DAILY_DTL_CKO087',
   N'반복제조(시멘트) 생산실적 상세 — 품목별 생산량, 손실량, 원료투입량',
   N'생산상세,손실,원료,수율,반복제조상세'),

  ('P_PROD_REMICON_HDR_CKO087',
   N'레미콘 생산실적 헤더 — 레미콘 공장 일별/믹서별 생산량(M3), 배합번호, 출하차량수, 일마감 여부',
   N'레미콘,생산,생산량,M3,믹서,배합,출하,차량,마감,DAY_MAGAM_YN'),

  ('P_PROD_REMICON_DTL_CKO087',
   N'레미콘 생산실적 상세 — 납품처별 출하량(M3)',
   N'레미콘상세,납품,출하,M3'),

  ('P_PROD_REMITAL_HDR_CKO087',
   N'레미탈 생산실적 헤더 — 레미탈 공장 일별 생산량(TON), 일마감 여부',
   N'레미탈,생산,생산량,TON,마감,DAY_MAGAM_YN'),

  ('P_PROD_REMITAL_DTL_CKO087',
   N'레미탈 생산실적 상세 — 품목별 생산량, 손실량',
   N'레미탈상세,손실,TON'),

  ('P_WORK_CENTER',
   N'작업장(호기/라인) 마스터 — 공장별 작업장코드, 작업장명, 그룹코드. 생산실적 WC_CD 조인용',
   N'작업장,호기,라인,WC_CD,작업장명,그룹')
) AS S(TABLE_NM, TABLE_DESC, KEYWORD_LIST)
ON T.TABLE_NM = S.TABLE_NM
WHEN MATCHED THEN
    UPDATE SET TABLE_DESC=S.TABLE_DESC, KEYWORD_LIST=S.KEYWORD_LIST, UPDT_DT=GETDATE()
WHEN NOT MATCHED THEN
    INSERT (TABLE_NM, TABLE_DESC, KEYWORD_LIST, USE_YN, SRC_TYPE)
    VALUES (S.TABLE_NM, S.TABLE_DESC, S.KEYWORD_LIST, 'Y', 'M');

PRINT 'PA999_TABLE_META CKO087 항목 추가 완료';
GO

-- ──────────────────────────────────────────────────────────────────
-- 검증
-- ──────────────────────────────────────────────────────────────────
SELECT
    TABLE_NM,
    COUNT(*) AS 컬럼수
FROM PA999_COLUMN_META
WHERE USE_YN = 'Y'
GROUP BY TABLE_NM
ORDER BY TABLE_NM;
GO

PRINT '══ 05 완료: 컬럼 메타 삽입 (' +
      CAST((SELECT COUNT(*) FROM PA999_COLUMN_META WHERE SRC_TYPE='M') AS VARCHAR)
      + '건) ══';
GO
