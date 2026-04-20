-- ══════════════════════════════════════════════════════════════
-- PA999_DEMO 데이터베이스 생성 (Railway MSSQL)
-- 실행 대상: nozomi.proxy.rlwy.net,36504  (SA 계정)
-- 실행 순서: 1번 → 2번 → 3번 순서로 실행
-- ══════════════════════════════════════════════════════════════

USE master;
GO

IF NOT EXISTS (SELECT 1 FROM sys.databases WHERE name = 'PA999_DEMO')
BEGIN
    CREATE DATABASE PA999_DEMO
        COLLATE Korean_Wansung_CI_AS;
    PRINT 'PA999_DEMO 데이터베이스 생성 완료';
END
ELSE
    PRINT 'PA999_DEMO 데이터베이스 이미 존재';
GO

USE PA999_DEMO;
GO
