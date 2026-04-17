using System.ComponentModel.DataAnnotations;

namespace Bizentro.App.SV.PP.PA999S1_CKO087.Models
{
    // ══════════════════════════════════════════════════════════
    // ■ Request / Response
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// PA999M1 UI → 챗봇 서버로 전달하는 질문 요청
    /// </summary>
    public class PA999ChatRequest
    {
        /// <summary>사용자 자연어 질문</summary>
        [Required]
        public string Question { get; set; } = string.Empty;

        /// <summary>대화 맥락 유지용 세션 ID (UNIERP 로그인 세션 단위)</summary>
        public string SessionId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>로그인 사용자 ID (권한 필터링 확장 시 활용)</summary>
        public string? UserId { get; set; }

        /// <summary>공장코드 (조회 범위 제한 시 활용)</summary>
        public string? PlantCd { get; set; }

        /// <summary>
        /// UNIERP 부서코드 (예: PROD=생산부, COST=원가팀, MAINT=보전팀)
        /// SystemPrompt에 주입되어 AI 응답 범위 제어에 사용
        /// </summary>
        public string? DeptCd { get; set; }

        /// <summary>
        /// UNIERP 사용자 역할 (예: PLANT_MGR, PROD_OPR, COST_MGR)
        /// 민감 데이터(원가, 인사) 접근 권한 제어에 사용
        /// </summary>
        public string? UserRole { get; set; }

        // ── RBAC Layer-2: 조직 범위 필터 ─────────────────────────────
        // Z_USR_ORG_MAST 에서 조회한 값을 UI 측이 채워서 전달
        // 미설정(null / 빈 문자열) = 전체 접근 허용 (관리자 계정)

        /// <summary>
        /// RBAC Layer-2 조직 유형 (BA=사업영역 / BU=사업부 / PL=공장)
        /// 미설정 시 조직 범위 제한 없음
        /// </summary>
        public string? OrgType { get; set; }

        /// <summary>
        /// RBAC Layer-2 허가된 조직 코드 (예: P031)
        /// OrgType = 'PL' 이면 PLANT_CD, 'BU' 이면 BU_CD, 'BA' 이면 BA_CD 에 적용
        /// 미설정 시 조직 범위 제한 없음
        /// </summary>
        public string? OrgCd { get; set; }
    }

    /// <summary>
    /// 챗봇 서버 → PA999M1 UI로 반환하는 답변
    /// </summary>
    public class PA999ChatResponse
    {
        /// <summary>Claude AI 최종 답변 (한국어)</summary>
        public string Answer { get; set; } = string.Empty;

        /// <summary>세션 ID (에코)</summary>
        public string SessionId { get; set; } = string.Empty;

        /// <summary>내부적으로 생성된 SELECT SQL (ShowSqlInResponse = true 일 때만 노출)</summary>
        public string? GeneratedSql { get; set; }

        /// <summary>Claude가 참조한 관련 테이블 목록</summary>
        public List<string>? RelevantTables { get; set; }

        /// <summary>오류 여부</summary>
        public bool IsError { get; set; }

        /// <summary>처리 모드 (SP, SQL, SOP) — 클라이언트 디버그/로그용</summary>
        public string? ProcessMode { get; set; }

        /// <summary>SOP 조치 가이드 (Mode A+SOP 결합 또는 Mode C 단독)</summary>
        public string? SopGuide { get; set; }

        /// <summary>SOP 메뉴 경로</summary>
        public string? MenuPath { get; set; }

        /// <summary>
        /// PA999_CHAT_LOG.LOG_SEQ — 피드백 제출 시 클라이언트에서 참조
        /// POST /api/PA999/log/{LogSeq}/feedback 에 사용
        /// </summary>
        public long? LogSeq { get; set; }

        // ── Dual-Channel: UI 그리드 바인딩용 구조화 데이터 ─────────────
        // Step 5 MSSQL 실행 결과(qr.Rows)를 직접 전달 — Claude 재생성 없음(Zero Hallucination)
        // null = 이번 답변에 표 형태 데이터 없음 (일반 대화, 집계 단일값 등)
        // 클라이언트(ModuleViewer)에서 DataTable 변환 후 dgvAiResult.DataSource 에 바인딩

        /// <summary>
        /// AI 조회 결과 구조화 데이터 (DB 컬럼명 → 값 매핑 리스트)
        /// null 이면 그리드 미표시
        /// </summary>
        public List<Dictionary<string, object?>>? GridData { get; set; }
    }

    // ══════════════════════════════════════════════════════════
    // ■ Configuration (appsettings.json 바인딩)
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// appsettings.json > "PA999S1" 섹션 바인딩 클래스
    /// </summary>
    public class PA999Options
    {
        public const string SectionName = "PA999S1";

        /// <summary>Anthropic API Key (환경변수 PA999S1__AnthropicApiKey 권장)</summary>
        public string AnthropicApiKey { get; set; } = string.Empty;

        /// <summary>사용 모델 (기본값: claude-sonnet-4-6)</summary>
        public string Model { get; set; } = "claude-sonnet-4-6";

        /// <summary>UNIERP MSSQL 연결 문자열 (읽기 전용 계정 사용 권장)</summary>
        public string ConnectionString { get; set; } = string.Empty;

        /// <summary>응답에 생성된 SQL 포함 여부 (개발: true, 운영: false)</summary>
        public bool ShowSqlInResponse { get; set; } = false;

        /// <summary>세션 타임아웃 (분)</summary>
        public int SessionTimeoutMinutes { get; set; } = 30;

        // ── RAG 임베딩 (OpenAI) ───────────────────────────────────

        /// <summary>OpenAI API Key (RAG 임베딩용, 미설정 시 임베딩 비활성화)</summary>
        public string OpenAIApiKey { get; set; } = string.Empty;

        /// <summary>임베딩 모델 (기본: text-embedding-3-small)</summary>
        public string EmbeddingModel { get; set; } = "text-embedding-3-small";

        /// <summary>RAG Top-K 유사 패턴 수 (기본: 5)</summary>
        public int EmbeddingTopK { get; set; } = 5;
    }

    // ══════════════════════════════════════════════════════════
    // ■ Internal (서비스 간 전달 모델)
    // ══════════════════════════════════════════════════════════

    /// <summary>Claude API 메시지 (role: user | assistant)</summary>
    public class PA999ConversationMessage
    {
        public string Role    { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }

    /// <summary>Claude가 생성한 SQL 결과</summary>
    public class PA999SqlResult
    {
        public bool   HasSql { get; set; }
        public string Sql    { get; set; } = string.Empty;
    }

    /// <summary>MSSQL 쿼리 실행 결과</summary>
    public class PA999QueryResult
    {
        public List<string>                        Columns      { get; set; } = new();
        public List<Dictionary<string, object?>>   Rows         { get; set; } = new();
        public bool                                IsSuccess    { get; set; }
        public string?                             ErrorMessage { get; set; }
    }

    /// <summary>DB 컬럼 메타데이터</summary>
    public class PA999ColumnInfo
    {
        public string ColumnName { get; set; } = string.Empty;
        public string DataType   { get; set; } = string.Empty;
        public int    MaxLength  { get; set; }
        public bool   IsNullable { get; set; }
    }

    /// <summary>FK 관계 메타데이터</summary>
    public class PA999FkInfo
    {
        public string ParentTable  { get; set; } = string.Empty;
        public string ParentColumn { get; set; } = string.Empty;
        public string RefTable     { get; set; } = string.Empty;
        public string RefColumn    { get; set; } = string.Empty;
    }

    /// <summary>관리자 SQL 직접 실행 요청 (분석/학습용)</summary>
    public class PA999AdminQueryRequest
    {
        public string Sql { get; set; } = string.Empty;
    }

    // ══════════════════════════════════════════════════════════
    // ■ PA999ChatLogService 관련 모델
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// PA999_CHAT_LOG INSERT용 엔트리
    /// PA999ChtbotService.AskAsync() 완료 후 PA999ChatLogService.SaveLogAsync()에 전달
    /// </summary>
    public class PA999ChatLogEntry
    {
        public string  SessionId     { get; set; } = string.Empty;
        public string? UserId        { get; set; }
        public string  UserQuery     { get; set; } = string.Empty;
        public string? AiResponse    { get; set; }
        public string? GeneratedSql  { get; set; }
        public string? RelatedTables { get; set; }
        public bool    IsError       { get; set; }
    }

    /// <summary>
    /// 개발자 피드백 제출 요청
    /// PATCH /api/PA999/log/{logSeq}/feedback 바디
    /// </summary>
    public class PA999FeedbackRequest
    {
        /// <summary>응답 품질 점수 (1=매우 나쁨 ~ 5=매우 좋음)</summary>
        public int     PerfScore   { get; set; }

        /// <summary>개발자 피드백 내용 (잘못된 SQL, 개선 방향 등)</summary>
        public string  DevFeedback { get; set; } = string.Empty;

        /// <summary>피드백 작성자 ID</summary>
        public string? FeedbackBy  { get; set; }

        /// <summary>피드백 유형: "U"=사용자(현업 GOOD/BAD), "D"=개발자(상세 평가). 기본값 "D"</summary>
        public string? FeedbackType { get; set; }
    }

    /// <summary>
    /// PA999_FEEDBACK_PATTERN 행 — Claude 시스템 프롬프트 주입용
    /// </summary>
    public class PA999FeedbackPattern
    {
        public int     PatternSeq    { get; set; }
        public string  QueryPattern  { get; set; } = string.Empty;
        public string? WrongApproach { get; set; }
        public string? CorrectSql    { get; set; }
        public string  Lesson        { get; set; } = string.Empty;
        public byte    Priority      { get; set; } = 5;
    }

    /// <summary>
    /// 피드백 리뷰 화면용 로그 행 (미평가 목록 조회 결과)
    /// GET /api/PA999/log/review 응답 항목
    /// </summary>
    public class PA999LogReviewItem
    {
        public long     LogSeq       { get; set; }
        public string   SessionId    { get; set; } = string.Empty;
        public string?  UserId       { get; set; }
        public string   UserQuery    { get; set; } = string.Empty;
        public string?  GeneratedSql { get; set; }
        public string?  AiResponse   { get; set; }
        public bool     IsError      { get; set; }
        public DateTime CreatedDt    { get; set; }
        public int?     PerfScore    { get; set; }
    }

    /// <summary>
    /// 컬럼 메타 재분석 요청
    /// POST /api/PA999/meta/reanalyze-columns 바디
    /// </summary>
    public class PA999ReanalyzeRequest
    {
        /// <summary>
        /// 재분석할 테이블 목록. null 또는 빈 배열이면 USE_YN='Y' 전체 처리
        /// </summary>
        public List<string>? TableNames { get; set; }

        /// <summary>
        /// true이면 SRC_TYPE='M'(수동입력) 행도 덮어씀 (기본: false)
        /// </summary>
        public bool OverwriteManual { get; set; } = false;
    }

    /// <summary>
    /// PA999_FEEDBACK_PATTERN 등록 요청
    /// POST /api/PA999/log/pattern 바디
    /// </summary>
    public class PA999PatternCreateRequest
    {
        /// <summary>잘못 처리된 질문 유형 요약 (예: "공장명 서브쿼리 사용")</summary>
        public string  QueryPattern  { get; set; } = string.Empty;

        /// <summary>AI의 잘못된 접근법 설명 (선택)</summary>
        public string? WrongApproach { get; set; }

        /// <summary>올바른 SQL 예시 (선택)</summary>
        public string? CorrectSql    { get; set; }

        /// <summary>교훈 요약 — 시스템 프롬프트에 직접 주입되는 핵심 문장</summary>
        public string  Lesson        { get; set; } = string.Empty;

        /// <summary>원본 로그 참조 (선택)</summary>
        public long?   LogSeq        { get; set; }

        /// <summary>우선순위 (1=최고 ~ 10=최저, 기본값 5)</summary>
        public byte    Priority      { get; set; } = 5;

        /// <summary>등록자 ID</summary>
        public string? CreatedBy     { get; set; }
    }
}
