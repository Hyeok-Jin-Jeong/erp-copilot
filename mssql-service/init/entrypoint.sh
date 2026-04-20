#!/bin/bash
# ══════════════════════════════════════════════════════════════
# SQL Server 시작 + DB 자동 초기화
# Railway 환경에서 SA_PASSWORD 환경변수로 주입받음
# ══════════════════════════════════════════════════════════════

set -e

SA_PWD="${SA_PASSWORD:-PA999Demo@2026!}"

echo "[entrypoint] SQL Server 시작 중..."
/opt/mssql/bin/sqlservr &
SQLPID=$!

echo "[entrypoint] SQL Server 준비 대기 (최대 60초)..."
for i in $(seq 1 30); do
    if /opt/mssql-tools18/bin/sqlcmd \
        -S localhost \
        -U SA \
        -P "$SA_PWD" \
        -C \
        -Q "SELECT 1" \
        > /dev/null 2>&1; then
        echo "[entrypoint] SQL Server 준비 완료 (${i}번째 시도)"
        break
    fi
    echo "[entrypoint] 대기 중... ($i/30)"
    sleep 2
done

echo "[entrypoint] DB 초기화 스크립트 실행..."

# 1단계: 데이터베이스 생성
/opt/mssql-tools18/bin/sqlcmd \
    -S localhost \
    -U SA \
    -P "$SA_PWD" \
    -C \
    -i /init/01_create_db.sql \
    && echo "[entrypoint] 01_create_db.sql 완료"

# 2단계: 테이블 생성
/opt/mssql-tools18/bin/sqlcmd \
    -S localhost \
    -U SA \
    -P "$SA_PWD" \
    -C \
    -i /init/02_create_tables.sql \
    && echo "[entrypoint] 02_create_tables.sql 완료"

# 3단계: 데모 데이터 삽입
/opt/mssql-tools18/bin/sqlcmd \
    -S localhost \
    -U SA \
    -P "$SA_PWD" \
    -C \
    -i /init/03_insert_demo_data.sql \
    && echo "[entrypoint] 03_insert_demo_data.sql 완료"

echo "[entrypoint] ✅ DB 초기화 완료! SQL Server 실행 중..."
wait $SQLPID
