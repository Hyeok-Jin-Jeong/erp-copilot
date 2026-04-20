# Railway MSSQL 데이터 보강 실행 가이드

## 접속 정보
| 항목 | 값 |
|------|-----|
| 외부 접속 | Railway 대시보드 → mssql 서비스 → Connect 탭에서 확인 |
| 내부 접속 | `erp-copilot.railway.internal,1433` (Railway 내부망 전용) |
| Database | `PA999_DEMO` |
| User | `sa` |
| Password | `PA999Demo@2026!` |

## 실행 순서 (SSMS 또는 sqlcmd)

```
04_CREATE_CKO087_TABLES.sql   → 테이블 생성
05_COLUMN_META_KO_NM.sql      → 컬럼 한국어 메타 삽입
06_FEEDBACK_PATTERN_20EA.sql  → 피드백 패턴 20개 삽입
07_REALISTIC_SAMPLE_DATA.sql  → 현실화 샘플 데이터 삽입
```

## Railway Shell에서 실행하는 방법
```bash
# Railway CLI로 쉘 접속
railway shell

# sqlcmd로 스크립트 실행
/opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P 'PA999Demo@2026!' \
  -i /scripts/04_CREATE_CKO087_TABLES.sql
```

## 추가된 테이블 목록
| 테이블명 | 설명 |
|---------|------|
| P_WORK_CENTER | 공장별 작업장(호기/라인) 마스터 |
| P_PROD_DAILY_HDR_CKO087 | 반복제조(시멘트) 생산실적 헤더 |
| P_PROD_DAILY_DTL_CKO087 | 반복제조 생산실적 상세 |
| P_PROD_REMICON_HDR_CKO087 | 레미콘 생산실적 헤더 |
| P_PROD_REMICON_DTL_CKO087 | 레미콘 생산실적 상세 |
| P_PROD_REMITAL_HDR_CKO087 | 레미탈 생산실적 헤더 |
| P_PROD_REMITAL_DTL_CKO087 | 레미탈 생산실적 상세 |
| PA999_COLUMN_META | AI 컬럼 한국어 설명 |

## 샘플 데이터 규모 (2026년 3월 + 4월 1~20일)
| 공장 | 유형 | 월 목표 | 비고 |
|-----|------|--------|------|
| 단양(P001) | 시멘트 | 90,000 TON | 3개 호기, 주말 0 |
| 영월(P031) | 시멘트 | 60,000 TON | 2개 호기, 주말 0 |
| 성남(P007) | 레미콘 | 15,000 M3 | 2개 믹서, 주말 0 |
| 화성(P022) | 레미콘 | 14,000 M3 | 2개 믹서, 주말 0 |
| 부산(P004) | 레미콘 | 13,000 M3 | 1개 믹서, 주말 0 |
| 수원(P050) | 레미탈 | 6,600 TON | 1개 분쇄기, 주말 0 |
| 광주(P051) | 레미탈 | 5,500 TON | 1개 분쇄기, 주말 0 |
