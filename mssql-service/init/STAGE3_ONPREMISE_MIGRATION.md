# Stage 3 — 온프레미스 UNIERP 전환 가이드

> 현재 Railway 데모는 **Mode A(SP) 시연용 최소 카탈로그**(3개 SP / 5 파라미터 / 3 SOP / 2 ERROR_MAP)만 탑재.
> 실제 UNIERP 온프레미스 환경 붙일 때 아래 순서대로 교체.

---

## 📋 체크리스트

### 1. 실제 UNIERP DB 접속 정보로 교체
- `appsettings.local.json` 또는 환경변수 `PA999S1__ConnectionString`
  ```
  Server=UNIERP_SERVER,1433;Database=UNIERP_PROD;User Id=...;Password=...;
  ```
- Railway의 `PA999S1__ConnectionString` 환경변수 제거 또는 온프레미스 주소로 교체

### 2. `PA999_DEMO_*` 프로시저 제거 (온프레미스에서는 실제 SP 사용)
```sql
DROP PROCEDURE IF EXISTS PA999_DEMO_SP_PROD_DAILY_R;
DROP PROCEDURE IF EXISTS PA999_DEMO_SP_REMICON_MONTHLY_R;
DROP PROCEDURE IF EXISTS PA999_DEMO_SP_PLANT_LIST_R;
```

### 3. 실제 SP 카탈로그로 교체
- `PA999_SP_CATALOG` / `PA999_SP_PARAMS` 를 **실제 UNIERP SP 200+개**로 재시드
- `Python_Scripts/rebuild_sp_keywords.py` 를 실행하여 키워드 자동 추출
- 실제 SP 예시:
  - `PA999_SP_PROD_DAILY_R` (생산일보)
  - `PA999_SP_REMICON_MONTHLY_R` (레미콘 월보)
  - `PA999_SP_AUTO_CLOSE_X` (자동마감 실행)
  - ... 이하 200+개

### 4. SOP / ERROR_MAP 확장
- 데모 3건 → 실제 운영 이슈 기반 **50~100건**으로 확장
- 주요 ERROR_TYPE:
  - `STOCK_SHORTAGE` (재고 부족)
  - `AUTO_CLOSE_FAIL` (자동마감 실패)
  - `BOM_MISSING` (BOM 누락)
  - `STATUS_CD_MISMATCH` (상태코드 불일치)

### 5. 관리자 엔드포인트 제거 (보안)
- `PA999Controller.cs`
  - `[HttpPost("admin/migrate")]` 메서드 제거 또는 주석
  - `[HttpPost("admin/query")]` 메서드 제거 또는 주석
  - `[HttpGet("admin/netdiag")]` 메서드 제거
- `Program.cs`
  - X-Migration-Key 미들웨어 우회 로직 제거 (`isMigrationPath` 블록)
- Railway 환경변수 `PA999S1__MigrationKey` 삭제

### 6. Demo 플래그 OFF
- `PA999S1__ShowSqlInResponse = false` (운영 시 SQL 노출 금지)
- `.env.local` `VITE_DEMO_MODE=false`

### 7. Cache Warmup 재활성화
- `Program.cs` L75-83의 `!isRailway` 가드 제거 → 온프레미스에서는 항상 워밍업 실행
- 또는 `PA999S1__EnableCacheWarmup=true` 같은 명시적 플래그로 전환

### 8. CORS 원복
- `Program.cs` L103-126의 `AllowAnyOrigin` → 화이트리스트 기반으로 복원
- `appsettings.json` 의 `AllowedOrigins` 에 온프레미스 프론트 URL 등록

### 9. OpenAI Embedding 키 설정 (선택)
- `PA999S1__OpenAIApiKey = sk-...`
- 임베딩 ON → RAG 선행 매칭 활성화 → 라우팅 정확도 ↑

### 10. 인증/권한 추가 (필수)
- 현재 챗봇 `/api/PA999/ask` 는 **인증 없음** — 포트폴리오 데모 전용
- 온프레미스 배포 전 반드시 JWT 또는 SSO 연동 필요
- UNIERP 사용자 권한 맵핑 (예: 생산팀만 생산실적 조회 가능)

---

## 🔄 SP/SOP 모드 라우팅 — 코드 수정 불필요

`PA999ModeRouter.cs` 는 **카탈로그 데이터에 의해 동작**하므로
실제 UNIERP SP 카탈로그로 재시드만 하면 자동으로 실제 SP를 매칭/실행합니다.
코드 변경 없음.

---

## ⚠️ 전환 전 테스트

온프레미스 전환 후 최소 아래 시나리오 통과 확인:

| 모드 | 질문 | 기대 결과 |
|---|---|---|
| SP | "○○공장 YYYYMMDD 생산일보" | 실제 SP `PA999_SP_PROD_DAILY_R` 실행, 다중 결과셋 반환 |
| SQL | "2025-12 월별 생산량 추이" | AI가 SQL 생성 → 실제 테이블 조회 |
| SOP | "재고부족 자동마감 실패 시 조치방법" | `PA999_SP_SOP` 에서 ACTION_GUIDE 반환 |
| ERROR_MAP | "왜 ○○공장 자동마감이 안됐어요?" | ERROR_MAP 매칭 → SP + 진단 테이블 복합 조회 |

---

_작성: 2026-04-21 — Stage 1(SP/SOP 시드) + Stage 2(E2E 통과) 완료 시점_
