#!/bin/bash
# ══════════════════════════════════════════════════════════════
# SQL Server 시작 + DB 자동 초기화
# Railway 환경에서 SA_PASSWORD 환경변수로 주입받음
# ══════════════════════════════════════════════════════════════

# set -e 제거: 개별 스크립트 실패 시 전체 중단 방지
SA_PWD="${SA_PASSWORD:-${SA_PWD:-PA999Demo@2026!}}"

echo "[entrypoint] SQL Server 시작 중..."
/opt/mssql/bin/sqlservr &
SQLPID=$!

echo "[entrypoint] SQL Server 준비 대기 (최대 180초)..."
SQL_READY=0
for i in $(seq 1 90); do
    if /opt/mssql-tools18/bin/sqlcmd \
        -S localhost \
        -U SA \
        -P "$SA_PWD" \
        -C \
        -l 3 \
        -Q "SELECT 1" \
        > /dev/null 2>&1; then
        echo "[entrypoint] SQL Server 준비 완료 (${i}번째 시도, $((i*2))초 경과)"
        SQL_READY=1
        break
    fi
    echo "[entrypoint] 대기 중... ($i/90)"
    sleep 2
done

if [ "$SQL_READY" -eq 0 ]; then
    echo "[entrypoint] ❌ SQL Server가 180초 내 시작되지 않았습니다. 초기화 건너뜀."
    wait $SQLPID
    exit 1
fi

echo "[entrypoint] DB 초기화 스크립트 실행..."

run_sql() {
    local script="$1"
    local label="$2"
    echo "[entrypoint] 실행 중: $label..."
    if /opt/mssql-tools18/bin/sqlcmd \
        -S localhost \
        -U SA \
        -P "$SA_PWD" \
        -C \
        -l 30 \
        -i "$script"; then
        echo "[entrypoint] ✅ $label 완료"
    else
        echo "[entrypoint] ⚠️  $label 실패 (계속 진행)"
    fi
}

run_sql /init/01_create_db.sql      "01_create_db.sql"
run_sql /init/02_create_tables.sql  "02_create_tables.sql"
run_sql /init/03_insert_demo_data.sql "03_insert_demo_data.sql"
run_sql /init/04_CREATE_CKO087_TABLES.sql "04_CREATE_CKO087_TABLES.sql"
run_sql /init/05_COLUMN_META_KO_NM.sql    "05_COLUMN_META_KO_NM.sql"
run_sql /init/06_FEEDBACK_PATTERN_20EA.sql "06_FEEDBACK_PATTERN_20EA.sql"
run_sql /init/07_REALISTIC_SAMPLE_DATA.sql "07_REALISTIC_SAMPLE_DATA.sql"
run_sql /init/08_SP_SOP_DEMO.sql           "08_SP_SOP_DEMO.sql"

echo "[entrypoint] ✅ DB 초기화 완료! SQL Server 실행 중..."
wait $SQLPID
