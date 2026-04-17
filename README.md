# UNIERP AI ChatBot (PA999)

> **Claude AI 기반 자연어 질의 챗봇** — ERP 생산관리 데이터를 SQL 없이 한국어로 조회  
> 개발: 정혁진 주임 | ERP 1파트 (생산) | 2026.03.17 ~ 04.06 (3주, 1인)

---

## 핵심 성과

| 지표 | Before | After | 개선 |
|------|--------|-------|------|
| 라우팅 정확도 | 76% | **93%** | +17%p |
| 토큰 사용량 (RAG) | 5,000 토큰/질문 | **670 토큰/질문** | **87% 절감** |
| 개발 기간 | 추정 12주 (4인 팀) | **3주 (1인)** | **4배 향상** |
| 개발 비용 | 외주 추정 3,000만원 | **AI 도구비 15만원/월** | — |

---

## 시스템 구성

```
┌─────────────────────────────────────────────────────────────┐
│  UNIERP Smart Client (WinForms)                             │
│  ┌──────────────┐   ┌────────────────────────────────────┐  │
│  │  PA999M1     │   │  CB990M1                           │  │
│  │  챗봇 UI     │   │  품질관리 (GOOD/BAD + 패턴 등록)   │  │
│  └──────┬───────┘   └──────────────────┬─────────────────┘  │
└─────────┼─────────────────────────────┼────────────────────┘
          │ HTTP REST                   │ HTTP REST
          ▼                             ▼
┌─────────────────────────────────────────────────────────────┐
│  PA999S1  (ASP.NET Core 6  |  C#)                          │
│                                                             │
│  ① 자연어 파싱 → MODE 라우팅 (SP / SQL / SOP)              │
│  ② RAG: OpenAI Embedding + 코사인 유사도 → Top-K 패턴 주입 │
│  ③ Claude Sonnet 4.6 → SELECT SQL 생성 / 답변 생성         │
│  ④ 7단계 SQL 검증 (DML·시스템함수·시간지연 차단)           │
│  ⑤ MSSQL 실행 → 결과 한국어 요약                           │
└────────────────────────┬────────────────────────────────────┘
                         │
          ┌──────────────┼──────────────┐
          ▼              ▼              ▼
   Claude API     OpenAI API      MSSQL
  (Sonnet 4.6)  (Embedding)   (UNIERP60N)
```

---

## 3-Mode 라우팅

| Mode | 트리거 | 동작 |
|------|--------|------|
| **A (SP)** | ERROR_MAP / SP_CATALOG 키워드 매칭 | 기존 저장프로시저 파라미터 추출 후 실행 |
| **B (SQL)** | SP/SOP 미매칭 | Claude가 SELECT 쿼리 생성 → 검증 → 실행 |
| **C (SOP)** | 업무 절차 키워드 매칭 | 절차 안내 텍스트 반환 (SQL 없음) |

```
[PA999][Router] Mode=SP   Confidence=0.95  ERROR_MAP: STOCK_SHORTAGE
[PA999][Router] Mode=SP   Confidence=0.75  SP_CATALOG: USP_PP_PC551Q1_CKO087
[PA999][Router] Mode=SOP  Confidence=0.85  SOP 매칭 2건
[PA999][Router] Mode=SQL  Confidence=0.70  SP/SOP 미매칭 → SQL 생성
```

---

## RAG 아키텍처 (2026-04-06 전환)

```
[Before] 키워드 매칭 → 52개 패턴 전량 프롬프트 주입 (5,000 토큰)
[After]  벡터 임베딩 → 코사인 유사도 0.30 → Top-K만 주입 (670 토큰)

PA999_FEEDBACK_PATTERN (52건)
    └─ OpenAI text-embedding-3-small
    └─ 코사인 유사도 계산
    └─ Top-5 패턴만 Claude 시스템 프롬프트에 주입
```

---

## 보안

```
SQL 7단계 검증
  ① 주석 제거      ② 길이 5,000자 제한   ③ SELECT 시작 강제
  ④ DML/DDL 차단   ⑤ 시스템 함수 차단    ⑥ WAITFOR 차단
  ⑦ 중첩 DML 탐지

추가 보안
  · RBAC: 사용자 DeptCd 기반 조회 범위 제한 (일반 / 원가팀 / 공장장)
  · PII 마스킹: 주민번호·전화·사원번호·원가 자동 마스킹
  · 관리자 API: 등록 IP만 허용 (HTTP 403 차단)
  · DB: READ UNCOMMITTED, 최대 100행 / 30초 타임아웃
```

---

## 프로젝트 구조

```
github_upload/
├── PA999S1_Server/          ← REST API 서버 (C# ASP.NET Core 6)  ~4,000줄
│   ├── Controllers/
│   │   └── PA999Controller.cs       # 8개 API 엔드포인트
│   ├── Services/
│   │   ├── PA999ChtbotService.cs    # 6-Step 파이프라인 메인
│   │   ├── PA999ModeRouter.cs       # 3-Mode 라우팅 엔진
│   │   ├── PA999SpCatalogService.cs # SP 카탈로그 실행 (Mode A)
│   │   ├── PA999SchemaService.cs    # 테이블 스키마 캐시
│   │   ├── PA999DbService.cs        # MSSQL 쿼리 실행 + 검증
│   │   ├── PA999ChatLogService.cs   # 로그 저장 + 피드백 패턴
│   │   ├── PA999QueryCacheService.cs # 쿼리 결과 캐시
│   │   ├── PA999CacheWarmupService.cs # 서버 시작 시 캐시 예열
│   │   ├── PA999MetaBatchService.cs # 배치 처리
│   │   └── PA999SystemPrompt.cs     # Claude 시스템 프롬프트 관리
│   ├── Models/
│   │   └── PA999Models.cs           # DTO / Options 모델
│   ├── Program.cs                   # DI 설정, CORS, 미들웨어
│   ├── appsettings.json             # 설정 템플릿 (시크릿 제외)
│   └── appsettings.local.json.example  # 로컬 설정 예시
│
├── PA999M1_ChatbotUI/       ← WinForms 챗봇 UI (카카오톡 스타일)  ~1,400줄
│   ├── ModuleViewer.cs              # 메인 챗 UI (버블 + 스크롤)
│   ├── ModuleInitializer.cs         # UNIERP 모듈 진입점
│   ├── dsInput.cs / dsWorking.cs    # DataSet 정의
│   └── [*.dll 제외됨 — .gitignore]  # Bizentro 프레임워크 바이너리
│
├── CB990M1_QualityMgmt/     ← 품질관리 UI (GOOD/BAD 피드백 + 패턴 등록)  ~1,200줄
│   ├── ModuleViewer.cs              # 그리드 + 평가바 + 패턴 등록
│   ├── ModuleInitializer.cs
│   └── dsInput.cs / dsWorking.cs
│
├── SQL_Scripts/             ← DDL + 데이터 패치 스크립트
│   ├── PA999_FEEDBACK_TYPE_컬럼추가.sql
│   ├── CB990M1_SP_Q_수정_CORRECT_SQL_LESSON추가.sql
│   ├── PA999_피드백_패턴_교정_스크립트.sql
│   └── USP_CB990M1_CKO087_PAT_CUD_생성.sql
│
├── Python_Scripts/          ← SP 분석 / 키워드 자동화 스크립트
│   ├── rebuild_sp_keywords.py       # SP 카탈로그 키워드 재생성
│   ├── fill_unmapped_keywords.py    # 미매핑 키워드 자동 보충
│   └── test_sp_param_binding.py     # SP 파라미터 바인딩 검증
│
├── .gitignore
└── README.md
```

---

## 기술 스택

| 구분 | 기술 |
|------|------|
| AI | Claude Sonnet 4.6 (런타임) · Opus 4.6 (개발/Claude Code) |
| 벡터 | OpenAI text-embedding-3-small + 코사인 유사도 |
| 백엔드 | C# ASP.NET Core 6 Web API |
| 프론트엔드 | WinForms (UNIERP Bizentro 프레임워크) |
| DB | MSSQL Server |
| 개발도구 | Claude Code · MCP(MSSQL 직접 연동) · Visual Studio 2022 |

---

## 신규 DB 테이블

```sql
-- 질의-응답 전체 로그
CREATE TABLE PA999_CHAT_LOG (
    LOG_SEQ         INT IDENTITY(1,1) PRIMARY KEY,
    USER_ID         NVARCHAR(20),
    DEPT_CD         NVARCHAR(20),
    QUESTION        NVARCHAR(MAX),
    ROUTING_MODE    NVARCHAR(10),   -- SP / SQL / SOP
    GENERATED_SQL   NVARCHAR(MAX),
    ANSWER          NVARCHAR(MAX),
    FEEDBACK_TYPE   NVARCHAR(10),   -- GOOD / BAD
    PERF_SCORE      TINYINT,        -- 개발자 평가 1~5
    TOKEN_USED      INT,
    RESPONSE_MS     INT,
    REG_DT          DATETIME DEFAULT GETDATE()
);

-- RAG 소스: 라우팅 패턴
CREATE TABLE PA999_FEEDBACK_PATTERN (
    PATTERN_SEQ     INT IDENTITY(1,1) PRIMARY KEY,
    KEYWORD         NVARCHAR(200),
    ROUTING_MODE    NVARCHAR(10),
    SP_NM           NVARCHAR(100),
    CORRECT_SQL     NVARCHAR(MAX),
    LESSON          NVARCHAR(MAX),
    EMBEDDING       VARBINARY(MAX), -- OpenAI 임베딩 벡터
    USE_YN          CHAR(1) DEFAULT 'Y',
    REG_DT          DATETIME DEFAULT GETDATE()
);
```

---

## 빠른 시작 (로컬 실행)

```bash
# 1. 저장소 클론
git clone https://github.com/YOUR_GITHUB_ID/UNIERP-AI-ChatBot.git

# 2. 로컬 설정 파일 생성
cd PA999S1_Server
cp appsettings.local.json.example appsettings.local.json
# appsettings.local.json 에 실제 API Key / DB 연결 정보 입력

# 3. 실행
dotnet run

# 4. Swagger UI 확인
# http://localhost:5000/swagger
```

> **주의**: UNIERP DB(MSSQL)가 없으면 DB 연결 오류가 발생합니다.  
> DB 없이 API 구조만 확인하려면 `ConnectionString` 을 빈 값으로 두고  
> `PA999DbService` 의 try-catch 블록에서 Mock 데이터를 반환하도록 수정하세요.

---

## 주요 버그 해결 이력

| 버그 | 원인 | 해결 |
|------|------|------|
| Button NullReferenceException | UNIERP 프레임워크가 WinForms Button.Click 가로채기 | `Button` → `Label + MouseClick` 교체 |
| RAG 패턴 미반영 | SP 저장 경로에 임베딩 생성 로직 없음 (EMBEDDING=NULL) | `EmbedMissingPatternsAsync()` 캐시 무효화 시 자동 호출 |
| SQL 미생성 | Claude가 SQL 없이 안내 텍스트만 반환 | SystemPrompt에 "데이터 조회 시 SQL 생성 강제" 규칙 추가 |
| TVP 값 손실 | `DataTable.Merge()` 후 RowState 초기화 | `CUD_CHAR` 컬럼 기반 수동 DataTable 구성으로 전환 |

---

## API 엔드포인트

| Method | 경로 | 설명 |
|--------|------|------|
| POST | `/api/PA999/chat` | 자연어 질문 처리 (메인) |
| GET | `/api/PA999/log` | 채팅 로그 조회 |
| PATCH | `/api/PA999/log/{seq}/feedback` | GOOD/BAD 피드백 제출 |
| GET | `/api/PA999/log/review` | 개발자 리뷰 목록 |
| GET/POST | `/api/PA999/log/pattern` | 피드백 패턴 조회/등록 |
| DELETE | `/api/PA999/log/pattern/cache` | 패턴 캐시 무효화 + 임베딩 자동 보충 |
| GET | `/api/PA999/admin/stats` | 서버 통계 (관리자 IP 전용) |
| GET | `/api/PA999/health` | 서버 상태 확인 |

---

## 개발 회고

> **AI 없이 예상**: 풀스택 + AI/ML + DB 전문가 4인, 12주  
> **실제**: ERP 도메인 + 기본 C# 수준 1인, 3주 (Claude Code 활용)

**Claude Code가 특히 강력했던 작업**
- 835KB SP 분석 → 사람이 읽으면 이틀, AI는 10분
- 7개 파일 동시 수정 시 모든 시그니처·파라미터 일관 유지
- MCP로 MSSQL 직접 연동 → SSMS 전환 없이 스키마 즉시 확인

**AI가 해결하지 못했던 것**
- UNIERP 프레임워크 고유 제약 (소스코드 없어 추론에 의존)
- 비즈니스 도메인 검증 (SP가 마감 데이터만 반환하는지 현업 확인)

---

*한일네트웍스 UNIERP ChatBot Project | 정혁진 주임 | ERP 1파트 (생산)*
