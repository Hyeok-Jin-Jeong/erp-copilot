using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Bizentro.App.SV.PP.PA999S1_CKO087.Models;

namespace Bizentro.App.SV.PP.PA999S1_CKO087.Services
{
    /// <summary>
    /// PA999S1 - UNIERP AI 챗봇 핵심 서비스 v2
    ///
    /// ▶ 처리 흐름 (6단계)
    ///   Step 1. 사용자 질문 → PA999_TABLE_META DB LIKE 검색 → 후보 테이블 추출
    ///           (CS 하드코딩 제거 → DB 메타테이블 기반으로 전환)
    ///   Step 2. 식별된 테이블 → INFORMATION_SCHEMA: 스키마 조회
    ///   Step 3. 스키마 + 질문 → Claude: SELECT SQL 생성
    ///   Step 4. 생성된 SQL 안전성 검증 (SELECT만 허용)
    ///   Step 5. MSSQL 실행 → 결과 반환
    ///   Step 6. 질문 + 결과 → Claude: 한국어 최종 답변 생성
    ///
    /// ▶ 토큰 최적화 전략
    ///   ① DB LIKE 검색으로 500개 테이블 → 3~5개로 1차 필터 (Claude 호출 없음)
    ///   ② 필터된 소수 테이블 설명만 프롬프트 포함 (~500 토큰)
    ///   ③ 키워드 미매칭 시 Claude에게 핵심 테이블 목록(이름+설명 1줄)만 전달 (~3,000 토큰)
    /// </summary>
    public class PA999ChtbotService
    {
        private readonly IHttpClientFactory          _httpClientFactory;
        private readonly PA999DbService              _dbService;
        private readonly PA999SchemaService          _schemaService;
        private readonly PA999ChatLogService         _logService;
        private readonly PA999QueryCacheService      _queryCache;       // ★ 쿼리 캐시 서비스
        private readonly PA999ModeRouter             _modeRouter;       // ★ 3분기 자동 라우팅
        private readonly PA999SpCatalogService       _spCatalog;        // ★ SP 카탈로그 실행
        private readonly PA999Options                _options;
        private readonly ILogger<PA999ChtbotService> _logger;

        // 세션별 대화 히스토리 (Singleton 유지, 최대 10턴)
        private readonly Dictionary<string, List<PA999ConversationMessage>> _sessions = new();
        private readonly object _sessionLock = new();

        public PA999ChtbotService(
            IHttpClientFactory httpClientFactory,
            PA999DbService dbService,
            PA999SchemaService schemaService,
            PA999ChatLogService logService,
            PA999QueryCacheService queryCache,           // ★ 추가
            PA999ModeRouter modeRouter,                  // ★ 3분기 라우팅
            PA999SpCatalogService spCatalog,              // ★ SP 카탈로그 실행
            IOptions<PA999Options> options,
            ILogger<PA999ChtbotService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _dbService         = dbService;
            _schemaService     = schemaService;
            _logService        = logService;
            _queryCache        = queryCache;             // ★ 추가
            _modeRouter        = modeRouter;             // ★ 추가
            _spCatalog         = spCatalog;              // ★ 추가
            _options           = options.Value;
            _logger            = logger;
        }

        // ══════════════════════════════════════════════════════
        // ▶ Public : 챗봇 질문 처리 메인 진입점
        // ══════════════════════════════════════════════════════

        public async Task<PA999ChatResponse> AskAsync(PA999ChatRequest request)
        {
            var sessionId = request.SessionId;
            var history   = GetOrCreateSession(sessionId);

            try
            {
                _logger.LogInformation("[PA999] 질문 수신 | Session={S} | Q={Q}",
                    sessionId, request.Question);

                // ══════════════════════════════════════════════════════
                // ★ 3분기 자동 라우팅: SP(Mode A) / SOP(Mode C) / SQL(Mode B)
                // ══════════════════════════════════════════════════════
                var routeResult = await _modeRouter.RouteAsync(request.Question);
                _logger.LogInformation("[PA999][Router] Mode={M} | Confidence={C:F2} | Reason={R}",
                    routeResult.Mode, routeResult.Confidence, routeResult.Reason);

                // ★ RAG PREFERRED_MODE 보정 → ModeRouter 1.7순위로 이동 완료
                //   이전: ChtbotService에서 후처리로 Override
                //   현재: ModeRouter.RouteAsync 내부에서 선행 실행 (RAG_PREEMPT)
                //   DIRECT_KEYWORDS(Confidence=0.95)는 ModeRouter에서 1.5순위로 먼저 확정되므로
                //   RAG_PREEMPT가 DIRECT를 덮어쓰지 않음 (기존 보호 로직 유지)

                // ── Mode A: SP 카탈로그 (업무 로직 + 오류 진단) ──
                if (routeResult.Mode == PA999Mode.SP)
                {
                    var spResponse = await HandleModeA_SpAsync(request, routeResult, history);

                    // ★ SP Fallback Chain 보완
                    //   SP 실행 성공 + 데이터 0건 → SP 라우팅 자체는 정확했으므로 processMode=SP 유지
                    //   SP 실행 실패(IsError) → SQL Fallback 발동
                    bool spResultEmpty = spResponse.GridData == null || spResponse.GridData.Count == 0;
                    bool spExecFailed = spResponse.IsError;

                    if (spExecFailed && routeResult.FallbackMode == PA999Mode.SQL)
                    {
                        _logger.LogInformation(
                            "[PA999][Fallback] SP 실행 실패 → Mode B(SQL) 재시도 | SP={SP}",
                            routeResult.SpEntry?.SpName ?? "N/A");
                        // Mode B 흐름으로 진입 (아래 코드로 fall-through)
                    }
                    else
                    {
                        // SP 실행 성공 (데이터 0건 포함) → SP 응답 그대로 반환
                        if (spResultEmpty)
                            _logger.LogInformation(
                                "[PA999][ModeA] SP 결과 0건 — processMode=SP 유지 | SP={SP}",
                                routeResult.SpEntry?.SpName ?? "N/A");
                        return spResponse;
                    }
                }

                // ── Mode C: SOP 가이드 (절차/조치 안내) ──
                if (routeResult.Mode == PA999Mode.SOP && routeResult.SopEntries?.Count > 0)
                {
                    var sopResponse = HandleModeC_Sop(request, routeResult, history);
                    return sopResponse;
                }

                // ── Mode B: SQL 생성 (기존 흐름 유지) ──
                _logger.LogInformation("[PA999] Mode B (SQL) 진입 — 기존 흐름 실행");

                // ── Step 1. PA999_TABLE_META에서 관련 테이블 식별 ──
                var (relevantTables, metaContext) =
                    await Step1_IdentifyTablesFromMetaAsync(request.Question);

                _logger.LogInformation("[PA999] 식별 테이블: {T} | 방식: {M}",
                    string.Join(", ", relevantTables),
                    metaContext.SearchMethod);

                // ── Step 2. 스키마 조회 ────────────────────────────
                var schemaCtx = await _schemaService.GetSchemaContextAsync(relevantTables);

                // ── Step 2.5. 공장명 → 공장코드 사전 조회 ────────────
                var resolveResult = await ResolveKoreanNamesToCodesAsync(request.Question);

                // 다중 매칭 → 사용자에게 되묻기 응답 즉시 반환
                if (!string.IsNullOrEmpty(resolveResult.ClarificationMessage))
                {
                    return new PA999ChatResponse
                    {
                        Answer         = resolveResult.ClarificationMessage,
                        SessionId      = sessionId,
                        RelevantTables = relevantTables,
                        IsError        = false
                    };
                }

                var plantCdMap = resolveResult.Codes;

                // ── Step 2.7. 품목명 → B_ITEM.ITEM_CD 해석 ────────────────────
                // 질문의 한글 토큰을 B_ITEM.ITEM_NM LIKE 검색 → ITEM_CD 확정값 생성
                // 결과는 Step3 시스템 프롬프트에 주입되어 Claude의 B_ITEM 서브쿼리 의존 제거
                var itemCdSection = await ResolveItemNamesAsync(request.Question);

                // ── [RBAC Layer-2] Step 2.6. 공장 접근 사전 차단 ────────────────
                // 질문에서 해석된 공장코드(plantCdMap)가 사용자의 인가 OrgCd 와 다르면
                // Claude 호출 없이 즉시 거부 — 불필요한 LLM 비용 차단
                if (!string.IsNullOrWhiteSpace(request.OrgCd) &&
                    string.Equals(request.OrgType, "PL", StringComparison.OrdinalIgnoreCase) &&
                    plantCdMap.Count > 0)
                {
                    var unauthorizedPlants = plantCdMap
                        .Where(kv => !string.Equals(kv.Value, request.OrgCd,
                                        StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    if (unauthorizedPlants.Count > 0)
                    {
                        var denied = string.Join(", ",
                            unauthorizedPlants.Select(kv => $"'{kv.Key}'(코드: {kv.Value})"));

                        _logger.LogWarning(
                            "[PA999][RBAC] 공장 접근 차단 | User={U} | AuthorizedCd={A} | DeniedPlants={D}",
                            request.UserId, request.OrgCd, denied);

                        return new PA999ChatResponse
                        {
                            Answer    = $"접근 권한이 없습니다.\n"
                                      + $"요청하신 공장 {denied} 의 데이터는 조회할 수 없습니다.\n"
                                      + $"허가된 공장코드: '{request.OrgCd}'",
                            SessionId = sessionId,
                            IsError   = false
                        };
                    }
                }
                // ────────────────────────────────────────────────────────────────

                // ── Step 3. SQL 생성 ───────────────────────────────
                var sqlResult = await Step3_GenerateSqlAsync(
                    request.Question, schemaCtx, metaContext, relevantTables, plantCdMap, history,
                    request.OrgType, request.OrgCd,    // ← [RBAC Layer-2] 조직 제약 전달
                    itemCdSection);                     // ← [신규] 품목코드 확정값 전달

                string generatedSql = string.Empty;
                string dataCtx      = string.Empty;
                PA999QueryResult? queryResult = null;   // ← Dual-Channel: GridData 원본

                if (sqlResult.HasSql)
                {
                    generatedSql = sqlResult.Sql;
                    _logger.LogInformation("[PA999] 생성 SQL:\n{S}", generatedSql);

                    // ── Step 4. 안전성 검증 ──────────────────────────
                    if (!IsSafeQuery(generatedSql))
                    {
                        return ErrorResponse(sessionId,
                            "보안 정책상 해당 쿼리는 실행할 수 없습니다. SELECT 조회 질문만 가능합니다.");
                    }

                    // ── [RBAC Layer-2] Step 4.5. 생성 SQL 조직 범위 사후 검증 ──
                    // Layer-1(pre-flight)·Layer-2(prompt) 를 우회한 경우를 대비한 최종 방어선
                    if (!string.IsNullOrWhiteSpace(request.OrgCd) &&
                        string.Equals(request.OrgType, "PL", StringComparison.OrdinalIgnoreCase) &&
                        !ValidateSqlOrgCd(generatedSql, "PLANT_CD", request.OrgCd))
                    {
                        _logger.LogWarning(
                            "[PA999][RBAC] SQL 사후검증 실패 — 비인가 PLANT_CD 포함 | User={U} | AuthCd={A} | SQL={S}",
                            request.UserId, request.OrgCd, generatedSql);

                        return new PA999ChatResponse
                        {
                            Answer    = "보안 정책상 허가되지 않은 공장 데이터가 쿼리에 포함되어 실행할 수 없습니다.\n"
                                      + $"허가된 공장코드: '{request.OrgCd}'",
                            SessionId = sessionId,
                            IsError   = false
                        };
                    }
                    // ──────────────────────────────────────────────────────────

                    // ── Step 5. MSSQL 실행 (캐시 우선) ──────────────
                    var qr = await _queryCache.ExecuteWithCacheAsync(generatedSql); // ★ 변경
                    if (!qr.IsSuccess)
                        _logger.LogWarning("[PA999] 쿼리 실행 실패: {E}", qr.ErrorMessage);
                    else
                    {
                        dataCtx     = FormatQueryResult(qr);
                        queryResult = qr;   // ← Dual-Channel: 그리드용 원시 데이터 캡처
                    }
                }

                // ── Step 6. 최종 답변 생성 ────────────────────────────
                var answer = await Step6_GenerateAnswerAsync(request.Question, dataCtx, history);

                // ── Dual-Channel: [Text Response] 헤더 제거 (txtAiAnswer 에는 순수 텍스트만 표시)
                // Claude 가 AnswerGenerationSystemPrompt 지시에 따라 "[Text Response]\n..." 형식으로 반환
                // 클라이언트 txtAiAnswer 에는 헤더 없이 텍스트만 표시하고,
                // GridData 에는 Step 5 qr.Rows 를 그대로 전달 (Claude JSON 재생성 없음)
                if (answer.StartsWith("[Text Response]", StringComparison.OrdinalIgnoreCase))
                    answer = answer.Substring("[Text Response]".Length).TrimStart('\r', '\n', ' ');

                UpdateSession(sessionId, request.Question, answer);

                // ── Step 7. 로그 저장 (Fire-and-Forget: 실패해도 응답에 영향 없음) ──
                long logSeq = 0;
                try
                {
                    logSeq = await _logService.SaveLogAsync(new PA999ChatLogEntry
                    {
                        SessionId     = sessionId,
                        UserId        = request.UserId,
                        UserQuery     = request.Question,
                        AiResponse    = answer,
                        GeneratedSql  = generatedSql,
                        RelatedTables = relevantTables.Count > 0
                            ? string.Join(", ", relevantTables) : null,
                        IsError       = false
                    });
                }
                catch (Exception logEx)
                {
                    _logger.LogWarning(logEx, "[PA999] 로그 저장 실패 (무시) Session={S}", sessionId);
                }

                return new PA999ChatResponse
                {
                    Answer         = answer,
                    SessionId      = sessionId,
                    GeneratedSql   = _options.ShowSqlInResponse ? generatedSql : null,
                    RelevantTables = relevantTables,
                    IsError        = false,
                    ProcessMode    = "SQL",
                    LogSeq         = logSeq > 0 ? logSeq : null,
                    // ── Dual-Channel: DB 조회 결과 직접 전달 (Zero Hallucination 보장)
                    // null if no SQL executed or query failed
                    GridData       = queryResult?.Rows.Count > 0 ? queryResult.Rows : null
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PA999] 처리 중 오류 | Session={S}", sessionId);

                // 오류 응답도 로그 저장 시도
                _ = _logService.SaveLogAsync(new PA999ChatLogEntry
                {
                    SessionId  = sessionId,
                    UserId     = request.UserId,
                    UserQuery  = request.Question,
                    AiResponse = "처리 중 오류가 발생했습니다.",
                    IsError    = true
                });

                return ErrorResponse(sessionId, "처리 중 오류가 발생했습니다. 잠시 후 다시 시도해 주세요.");
            }
        }

        // ══════════════════════════════════════════════════════
        // ■ Step 1. PA999_TABLE_META 기반 테이블 식별
        //
        //   ① 1차: 질문 키워드로 KEYWORD_LIST LIKE 검색 (가장 정확, 토큰 0)
        //   ② 2차: KEYWORD_LIST 미매칭 시 TABLE_DESC LIKE 검색
        //   ③ 3차: 그래도 없으면 USE_YN=Y 전체 목록(이름+설명 1줄)을
        //          Claude에게 전달해 선택 요청 (~3,000 토큰 상한)
        // ══════════════════════════════════════════════════════

        private async Task<(List<string> Tables, PA999MetaContext MetaCtx)>
            Step1_IdentifyTablesFromMetaAsync(string question)
        {
            // 질문에서 2자 이상 한글/영문 토큰 추출
            var tokens = ExtractTokens(question);

            // ── Demo 폴백: DB 불가 시 하드코딩 메타데이터 사용 ──────
            if (!_dbService.IsDatabaseAvailable)
            {
                _logger.LogWarning("[PA999] DB 불가 — Demo 메타 폴백으로 Step1 실행");
                return Step1_DemoFallback(question, tokens);
            }

            // ── 1차: KEYWORD_LIST LIKE 검색 ──────────────────────
            var keywordSql = BuildKeywordSearchSql(tokens, searchColumn: "KEYWORD_LIST");
            var keywordResult = await _dbService.ExecuteQueryAsync(keywordSql);

            if (keywordResult.IsSuccess && keywordResult.Rows.Count > 0)
            {
                var tables = keywordResult.Rows
                    .Select(r => r.TryGetValue("TABLE_NM", out var v) ? v?.ToString() : null)
                    .Where(t => !string.IsNullOrEmpty(t))
                    .Select(t => t!)
                    .Distinct()
                    .Take(3)
                    .ToList();

                var metaDesc = BuildMetaDescription(keywordResult.Rows);

                _logger.LogInformation("[PA999] 1차(KEYWORD) 매칭: {T}", string.Join(",", tables));
                return (tables, new PA999MetaContext
                {
                    SearchMethod = "KEYWORD_MATCH",
                    MetaDescription = metaDesc
                });
            }

            // ── 2차: TABLE_DESC LIKE 검색 ─────────────────────────
            var descSql = BuildKeywordSearchSql(tokens, searchColumn: "TABLE_DESC");
            var descResult = await _dbService.ExecuteQueryAsync(descSql);

            if (descResult.IsSuccess && descResult.Rows.Count > 0)
            {
                var tables = descResult.Rows
                    .Select(r => r.TryGetValue("TABLE_NM", out var v) ? v?.ToString() : null)
                    .Where(t => !string.IsNullOrEmpty(t))
                    .Select(t => t!)
                    .Distinct()
                    .Take(3)
                    .ToList();

                var metaDesc = BuildMetaDescription(descResult.Rows);

                _logger.LogInformation("[PA999] 2차(DESC) 매칭: {T}", string.Join(",", tables));
                return (tables, new PA999MetaContext
                {
                    SearchMethod = "DESC_MATCH",
                    MetaDescription = metaDesc
                });
            }

            // ── 2.5차: INFORMATION_SCHEMA 전체 DB 테이블명 LIKE 검색 ────
            //   PA999_TABLE_META 미등록 테이블도 탐색 (UNIERP60N 전체 대상)
            _logger.LogInformation("[PA999] 2.5차: INFORMATION_SCHEMA 전체 검색");
            var schemaTables = await SearchFromInformationSchemaAsync(tokens);

            if (schemaTables.Count > 0)
            {
                _logger.LogInformation("[PA999] 2.5차(SCHEMA) 매칭: {T}", string.Join(",", schemaTables));
                return (schemaTables, new PA999MetaContext
                {
                    SearchMethod    = "SCHEMA_MATCH",
                    MetaDescription = string.Empty
                });
            }

            // ── 3차: Claude에게 전체 목록 전달 (최대 50개, USE_YN=Y만) ──
            // [정규화 변경] COLUMN_DESC DROP → PA999_COLUMN_META 서브쿼리로 COLUMN_LIST 조회
            _logger.LogInformation("[PA999] 3차: Claude에게 테이블 목록 전달");
            var allMetaSql = @"
                SELECT TOP 50
                    t.TABLE_NM,
                    t.TABLE_DESC,
                    t.KEYWORD_LIST,
                    (SELECT STUFF((
                        SELECT ', ' + c.COLUMN_NM + '=' + ISNULL(c.KO_NM, c.COLUMN_NM)
                        FROM PA999_COLUMN_META c WITH(NOLOCK)
                        WHERE c.TABLE_NM = t.TABLE_NM
                        FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 2, ''))
                    AS COLUMN_LIST
                FROM PA999_TABLE_META t WITH(NOLOCK)
                WHERE t.USE_YN = 'Y'
                ORDER BY t.TABLE_NM";

            var allResult = await _dbService.ExecuteQueryAsync(allMetaSql);
            var allMeta   = BuildMetaDescription(allResult.IsSuccess ? allResult.Rows : new());

            var allTableList = allResult.IsSuccess
                ? allResult.Rows
                    .Select(r => r.TryGetValue("TABLE_NM", out var v) ? v?.ToString() : null)
                    .Where(t => !string.IsNullOrEmpty(t))
                    .Distinct()
                    .ToList()!
                : new List<string> { "A_ACCT", "B_PLANT" };

            // Claude에게 테이블 목록 전달해 후보 선택
            var selectedTables = await AskClaudeToSelectTablesAsync(question, allMeta);

            return (selectedTables, new PA999MetaContext
            {
                SearchMethod    = "CLAUDE_SELECT",
                MetaDescription = allMeta
            });
        }

        // ── Demo 폴백: DB 불가 시 하드코딩 테이블 메타데이터 사용 ──
        private (List<string> Tables, PA999MetaContext MetaCtx) Step1_DemoFallback(
            string question, string[] tokens)
        {
            // 키워드로 관련 테이블 필터링
            var metaResult = PA999DemoDataService.AllTableMeta;
            var matched    = PA999DemoDataService.TableMeta
                .Where(t =>
                {
                    var kw   = (t["KEYWORD_LIST"] as string ?? "").ToLower();
                    var desc = (t["TABLE_DESC"]   as string ?? "").ToLower();
                    var nm   = (t["TABLE_NM"]     as string ?? "").ToLower();
                    return tokens.Any(tok =>
                        kw.Contains(tok.ToLower())   ||
                        desc.Contains(tok.ToLower()) ||
                        nm.Contains(tok.ToLower()));
                })
                .Select(t => t["TABLE_NM"] as string ?? "")
                .Where(s => !string.IsNullOrEmpty(s))
                .Distinct()
                .Take(3)
                .ToList();

            if (matched.Count == 0)
                matched = new List<string> { "B_PLANT", "P_PROD_DAILY_HDR" };

            var metaDesc = string.Join("\n", PA999DemoDataService.TableMeta
                .Where(t => matched.Contains(t["TABLE_NM"] as string ?? ""))
                .Select(t => $"- {t["TABLE_NM"]}: {t["TABLE_DESC"]} (키워드: {t["KEYWORD_LIST"]})"));

            _logger.LogInformation("[PA999] Demo Step1 결과: {T}", string.Join(",", matched));
            return (matched, new PA999MetaContext
            {
                SearchMethod    = "DEMO_KEYWORD_MATCH",
                MetaDescription = metaDesc
            });
        }

        // ── SQL 빌더: KEYWORD_LIST 또는 TABLE_DESC LIKE 검색 ─────

        private string BuildKeywordSearchSql(string[] tokens, string searchColumn)
        {
            // [정규화 변경] COLUMN_DESC 컬럼 DROP → PA999_COLUMN_META 서브쿼리로 COLUMN_LIST 조회
            //   FROM 절에 테이블 alias 't' 추가 (서브쿼리 상관 조건 t.TABLE_NM 필요)
            if (tokens.Length == 0)
                return @"
                SELECT TOP 0
                    TABLE_NM, TABLE_DESC, KEYWORD_LIST,
                    CAST('' AS NVARCHAR(MAX)) AS COLUMN_LIST
                FROM PA999_TABLE_META WHERE 1=0";

            var conditions = tokens
                .Select(t => $"t.{searchColumn} LIKE N'%{t.Replace("'", "''")}%'")
                .ToList();

            var whereClause = string.Join(" OR ", conditions);

            var matchScore = string.Join(" + ", tokens.Select(t =>
                $"CASE WHEN t.{searchColumn} LIKE N'%{t.Replace("'", "''")}%' THEN 1 ELSE 0 END"));

            // PA999_COLUMN_META에서 컬럼 목록을 "COLUMN_NM=KO_NM, ..." 형식으로 집계
            const string colListSubQuery = @"(
                    SELECT STUFF((
                        SELECT ', ' + c.COLUMN_NM + '=' + ISNULL(c.KO_NM, c.COLUMN_NM)
                        FROM PA999_COLUMN_META c WITH(NOLOCK)
                        WHERE c.TABLE_NM = t.TABLE_NM
                        FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 2, ''))";

            return $@"
                SELECT TOP 5
                    t.TABLE_NM,
                    t.TABLE_DESC,
                    t.KEYWORD_LIST,
                    {colListSubQuery} AS COLUMN_LIST,
                    -- 매칭 점수: 키워드 많이 포함할수록 높음
                    ({matchScore}) AS MATCH_SCORE
                FROM PA999_TABLE_META t WITH(NOLOCK)
                WHERE t.USE_YN = 'Y'
                  AND ({whereClause})
                ORDER BY MATCH_SCORE DESC,
                         -- HDR 우선, DTL 차순, 나머지 후순위 (생산실적 등 포괄 키워드 질문에서 핵심 테이블 우선 선택)
                         CASE WHEN t.TABLE_NM LIKE '%_HDR_%' THEN 0
                              WHEN t.TABLE_NM LIKE '%_DTL_%' THEN 1
                              ELSE 2 END,
                         t.TABLE_NM";
        }

        private string[] ExtractTokens(string question)
        {
            var tokens = new HashSet<string>();

            // ① 한글/영문 연속 토큰 추출
            foreach (Match m in Regex.Matches(question, @"[가-힣]{2,}|[A-Za-z0-9]{2,}"))
            {
                var val = m.Value.ToUpper();
                tokens.Add(val);

                // ② 한글 토큰: 조사/어미 제거를 위해 2자·3자 n-gram 서브스트링 추가
                //    예) "단양공장" → "단양","양공","공장" / "생산량을" → "생산량","산량을","생산","산량","량을"
                if (Regex.IsMatch(m.Value, @"^[가-힣]+$"))
                {
                    for (int len = 2; len < m.Value.Length; len++)
                    {
                        for (int i = 0; i <= m.Value.Length - len; i++)
                            tokens.Add(m.Value.Substring(i, len).ToUpper());
                    }
                }
            }

            // ③ 4자리 숫자(연도 등) 별도 추출
            foreach (Match m in Regex.Matches(question, @"[0-9]{4}"))
                tokens.Add(m.Value);

            return tokens.Where(t => t.Length >= 2).Distinct().ToArray();
        }

        private string BuildMetaDescription(List<Dictionary<string, object?>> rows)
        {
            if (rows.Count == 0) return string.Empty;

            var sb = new StringBuilder();
            foreach (var row in rows)
            {
                var nm   = row.TryGetValue("TABLE_NM",     out var n) ? n?.ToString() : "";
                var desc = row.TryGetValue("TABLE_DESC",   out var d) ? d?.ToString() : "";
                // [정규화 변경] COLUMN_DESC → COLUMN_LIST (PA999_COLUMN_META JOIN 결과)
                var col  = row.TryGetValue("COLUMN_LIST",  out var c) ? c?.ToString() : "";
                var kw   = row.TryGetValue("KEYWORD_LIST", out var k) ? k?.ToString() : "";

                sb.AppendLine($"[{nm}]");
                sb.AppendLine($"  설명: {desc}");
                if (!string.IsNullOrEmpty(col))
                    sb.AppendLine($"  컬럼: {col}");
                if (!string.IsNullOrEmpty(kw))
                    sb.AppendLine($"  키워드: {kw}");
            }
            return sb.ToString();
        }

        // ── 3차: Claude에게 테이블 선택 요청 ─────────────────────

        private async Task<List<string>> AskClaudeToSelectTablesAsync(
            string question, string tableListDesc)
        {
            var prompt = $@"아래 테이블 목록에서 질문에 필요한 테이블을 JSON 배열로 반환하세요.
최대 3개, JSON 배열만 반환 (설명 없이).

질문: ""{question}""

[사용 가능한 테이블 목록]
{tableListDesc}

예시: [""A_ACCT"", ""B_PLANT""]";

            var response = await CallClaudeAsync(
                new List<PA999ConversationMessage> {
                    new() { Role = "user", Content = prompt }
                },
                maxTokens: 200);

            try
            {
                var json   = ExtractJsonArray(response);
                var tables = JsonSerializer.Deserialize<List<string>>(json) ?? new();

                // 실제 PA999_TABLE_META에 등록된 테이블만 허용
                var validTablesSql = "SELECT TABLE_NM FROM PA999_TABLE_META WITH(NOLOCK) WHERE USE_YN='Y'";
                var validResult    = await _dbService.ExecuteQueryAsync(validTablesSql);
                var validSet       = validResult.IsSuccess
                    ? validResult.Rows
                        .Select(r => r.TryGetValue("TABLE_NM", out var v) ? v?.ToString()?.ToUpper() : null)
                        .Where(t => t != null)
                        .ToHashSet()!
                    : new HashSet<string>();

                return tables
                    .Where(t => validSet.Contains(t.ToUpper()))
                    .Take(3)
                    .ToList();
            }
            catch
            {
                return new List<string> { "A_ACCT" };
            }
        }

        // ══════════════════════════════════════════════════════
        // ■ Step 1 보조. INFORMATION_SCHEMA 전체 테이블명 LIKE 검색
        //   PA999_TABLE_META 미등록 테이블 포함, UNIERP60N 전체 대상
        // ══════════════════════════════════════════════════════

        private async Task<List<string>> SearchFromInformationSchemaAsync(string[] tokens)
        {
            if (tokens.Length == 0)
                return new List<string>();

            var conditions = tokens
                .Select(t => $"TABLE_NAME LIKE N'%{t.Replace("'", "''")}%'")
                .ToList();

            var whereClause = string.Join(" OR ", conditions);

            var sql = $@"
                SELECT TOP 5 TABLE_NAME
                FROM INFORMATION_SCHEMA.TABLES WITH(NOLOCK)
                WHERE TABLE_TYPE = 'BASE TABLE'
                  AND ({whereClause})
                ORDER BY TABLE_NAME";

            var result = await _dbService.ExecuteQueryAsync(sql);

            if (!result.IsSuccess || result.Rows.Count == 0)
                return new List<string>();

            return result.Rows
                .Select(r => r.TryGetValue("TABLE_NAME", out var v) ? v?.ToString() : null)
                .Where(t => !string.IsNullOrEmpty(t))
                .Select(t => t!)
                .ToList();
        }

        // ══════════════════════════════════════════════════════
        // ■ Step 3. 자연어 → SQL 생성
        //   메타 컨텍스트(TABLE_DESC, PA999_COLUMN_META → COLUMN_LIST)를 시스템 프롬프트에 포함
        // ══════════════════════════════════════════════════════

        // ══════════════════════════════════════════════════════
        // ■ Step 2.5. 질문에서 한국어 명칭 → 코드 사전 조회
        //   공장명, 품목명 등 사용자가 한국어로 입력한 값을
        //   DB에서 실제 코드로 변환하여 Claude에게 확정값 전달
        //   → B_PLANT 서브쿼리 다중결과 오류 원인 해소
        // ══════════════════════════════════════════════════════

        // ══════════════════════════════════════════════════════
        // ■ Step 2.5 반환 타입: 코드 확정 결과 + 되묻기 메시지
        // ══════════════════════════════════════════════════════

        private record ResolveResult(
            Dictionary<string, string> Codes,
            string? ClarificationMessage = null);

        private async Task<ResolveResult> ResolveKoreanNamesToCodesAsync(string question)
        {
            var codes = new Dictionary<string, string>();

            var plantKeywords = new[] { "단양", "영월", "삼곡", "장성", "옥천", "부산", "광양" };
            string? plantKeyword = null;
            foreach (var kw in plantKeywords)
            {
                if (question.Contains(kw))
                {
                    plantKeyword = kw;
                    break;
                }
            }

            if (string.IsNullOrEmpty(plantKeyword))
            {
                // ── 0차(폴백): 공장 유형 키워드 검색 ──────────────────────────────
                // 특정 공장명(단양/영월 등) 미매칭 시 업종 유형 키워드(레미콘/레미탈 등)로
                // B_PLANT.PLANT_GUBUN_CD 기준 전체 공장 조회
                return await ResolveByGubunTypeAsync(question, codes);
            }

            // ── 1차: 완전일치 조회 ("단양공장", "단양광업소" 등 정확한 명칭) ────
            var suffixes = new[] { "공장", "광업소", "사이로", "공장(주)", "지점" };
            foreach (var suffix in suffixes)
            {
                var candidate = plantKeyword + suffix;
                if (!question.Contains(candidate)) continue;

                var esc = candidate.Replace("'", "''");
                var exactSql = $"SELECT TOP 1 PLANT_CD, PLANT_NM FROM B_PLANT WITH(NOLOCK) WHERE PLANT_NM = N'{esc}'";
                var exactQr  = await _dbService.ExecuteQueryAsync(exactSql);

                if (exactQr.IsSuccess && exactQr.Rows.Count == 1)
                {
                    var pcd = exactQr.Rows[0].TryGetValue("PLANT_CD", out var c) ? c?.ToString() : null;
                    if (!string.IsNullOrEmpty(pcd))
                    {
                        codes[$"{candidate}(공장)"] = pcd!;
                        _logger.LogInformation("[PA999] 공장코드 확정(완전일치): {K} -> {V}", candidate, pcd);
                        return new ResolveResult(codes);
                    }
                }
            }

            // ── 2차: LIKE 검색 (단순 "단양" 키워드) ─────────────────────
            var kwEsc  = plantKeyword.Replace("'", "''");
            var likeSql = $"SELECT PLANT_CD, PLANT_NM FROM B_PLANT WITH(NOLOCK) WHERE PLANT_NM LIKE N'%{kwEsc}%' ORDER BY PLANT_CD";
            var qr = await _dbService.ExecuteQueryAsync(likeSql);

            if (!qr.IsSuccess || qr.Rows.Count == 0)
                return new ResolveResult(codes);

            if (qr.Rows.Count == 1)
            {
                var pcd = qr.Rows[0].TryGetValue("PLANT_CD", out var c) ? c?.ToString() : null;
                var pnm = qr.Rows[0].TryGetValue("PLANT_NM", out var n) ? n?.ToString() : null;
                if (!string.IsNullOrEmpty(pcd))
                {
                    codes[$"{plantKeyword}(공장)"] = pcd!;
                    _logger.LogInformation("[PA999] 공장코드 확정(LIKE): {K} -> {V} ({N})", plantKeyword, pcd, pnm);
                }
                return new ResolveResult(codes);
            }

            // ── 2.5차: 다중 결과 중 정확명("단양공장" 등) 자동 선택 ─────
            //   LIKE '%단양%' 이 과매칭(콜레이션/유니코드 이슈)으로 여러 행을 반환해도
            //   그중 PLANT_NM이 plantKeyword+접미사 형태로 정확히 일치하는 행이 딱 하나면
            //   되묻기 없이 해당 공장으로 확정. (사용자: "단일 매칭 시 재질문 분기 타지 않도록")
            var autoSuffixes = new[] { "공장", "광업소", "사이로", "지점" };
            foreach (var suffix in autoSuffixes)
            {
                var target = plantKeyword + suffix;
                var exactRows = qr.Rows.Where(r =>
                    r.TryGetValue("PLANT_NM", out var n) &&
                    string.Equals(n?.ToString(), target, StringComparison.Ordinal)).ToList();
                if (exactRows.Count == 1)
                {
                    var pcd = exactRows[0].TryGetValue("PLANT_CD", out var c) ? c?.ToString() : null;
                    if (!string.IsNullOrEmpty(pcd))
                    {
                        codes[$"{plantKeyword}(공장)"] = pcd!;
                        _logger.LogInformation(
                            "[PA999] 공장코드 확정(다중중 정확명 자동선택): {K} -> {V} ({N})",
                            target, pcd, target);
                        return new ResolveResult(codes);
                    }
                }
            }

            // ── 다중 결과: 사용자에게 되묻기 ────────────────────────────
            _logger.LogInformation("[PA999] 공장명 '{K}' 다중 매칭 {N}건 -> 사용자 재확인", plantKeyword, qr.Rows.Count);
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"'{plantKeyword}'에 해당하는 공장이 여러 개입니다. 어느 공장을 조회하시겠습니까?");
            sb.AppendLine();
            sb.AppendLine("| 공장코드 | 공장명 |");
            sb.AppendLine("|----------|--------|");
            foreach (var row in qr.Rows)
            {
                var cd = row.TryGetValue("PLANT_CD", out var c) ? c?.ToString() : "";
                var nm = row.TryGetValue("PLANT_NM", out var n) ? n?.ToString() : "";
                sb.AppendLine($"| {cd} | {nm} |");
            }
            sb.AppendLine();
            sb.AppendLine("공장코드(예: P001) 또는 정확한 공장명(예: 단양공장)으로 다시 질문해 주세요.");
            return new ResolveResult(codes, sb.ToString());
        }

        // ══════════════════════════════════════════════════════
        // ■ Step 2.5 보조. 공장 유형 키워드 → B_PLANT 전체 코드 조회
        //
        //   트리거 조건: 질문에 특정 공장명(단양/영월 등)이 없고
        //               업종 유형 키워드(레미콘/레미탈/반복제조/지역공장)가 있을 때
        //
        //   처리 결과: plantCdMap 에 해당 업종 전체 공장 추가
        //             → STEP A-2 에서 PLANT_GUBUN_CD 조회 → plantGubunMap 구성
        //             → STEP A-3 에서 올바른 생산 테이블(P_PROD_REMICON 등)로 자동 교정
        // ══════════════════════════════════════════════════════

        // 공장 유형 키워드 → (PLANT_GUBUN_CD, 표시명) 매핑 (정적 상수)
        private static readonly (string Keyword, string GubunCd, string GubunNm)[] _plantTypeKeywords =
        {
            ("레미콘",   "300", "레미콘"),
            ("remicon",  "300", "레미콘"),
            ("레미탈",   "400", "레미탈"),
            ("remital",  "400", "레미탈"),
            ("반복제조", "100", "반복제조"),
            ("지역공장", "200", "지역공장"),
        };

        private async Task<ResolveResult> ResolveByGubunTypeAsync(
            string question, Dictionary<string, string> codes)
        {
            foreach (var (kw, gubunCd, gubunNm) in _plantTypeKeywords)
            {
                if (!question.Contains(kw, StringComparison.OrdinalIgnoreCase))
                    continue;

                // B_PLANT 에서 해당 PLANT_GUBUN_CD 에 속하는 공장 전체 조회
                var typeSql = $@"
                    SELECT PLANT_CD, PLANT_NM
                    FROM B_PLANT (NOLOCK)
                    WHERE PLANT_GUBUN_CD = N'{gubunCd}'
                    ORDER BY PLANT_CD";

                var qr = await _dbService.ExecuteQueryAsync(typeSql);

                if (!qr.IsSuccess || qr.Rows.Count == 0)
                {
                    _logger.LogWarning("[PA999] 공장유형 '{K}'(GUBUN={G}) 공장 없음 또는 조회 실패",
                        kw, gubunCd);
                    continue;
                }

                foreach (var row in qr.Rows)
                {
                    var pcd = row.TryGetValue("PLANT_CD", out var pc) ? pc?.ToString() : null;
                    var pnm = row.TryGetValue("PLANT_NM", out var pn) ? pn?.ToString() : null;
                    if (!string.IsNullOrEmpty(pcd))
                        codes[$"{gubunNm}({pnm ?? pcd})"] = pcd!;
                }

                _logger.LogInformation(
                    "[PA999] 공장유형 매칭: '{K}'(GUBUN_CD={G}) → {N}개 공장 [{P}]",
                    kw, gubunCd, codes.Count,
                    string.Join(",", codes.Values));

                return new ResolveResult(codes);
            }

            // 유형 키워드도 없음 → 빈 결과 (공장 범위 제한 없음)
            return new ResolveResult(codes);
        }

        // ══════════════════════════════════════════════════════
        // ■ Step 2.7. 품목명 → B_ITEM.ITEM_CD 해석
        //
        //   트리거 조건: 질문에서 추출한 3자 이상 한글 토큰이 B_ITEM.ITEM_NM 에 매칭될 때
        //
        //   처리 결과: itemCdSection 문자열 → Step3 시스템 프롬프트에 주입
        //             Claude 가 B_ITEM 서브쿼리 없이 확정 ITEM_CD 직접 사용
        //
        //   필터링:
        //     - VALID_FLG = 'Y'  (유효 품목만)
        //     - LEN(ITEM_NM) ASC (짧은 이름 = 정확 매칭 우선)
        //     - TOP 20           (과다 노이즈 방지)
        //     - 3자 이상 한글 토큰만 검색 (조사/어미 등 단순 음절 제외)
        // ══════════════════════════════════════════════════════

        private async Task<string> ResolveItemNamesAsync(string question)
        {
            // ── 3자 이상 한글 토큰 추출 ──────────────────────────────
            var tokens = ExtractTokens(question)
                .Where(t => System.Text.RegularExpressions.Regex.IsMatch(t, @"^[가-힣]{3,}$"))
                .Distinct()
                .Take(4)   // 최대 4개 토큰 검색 (DB 쿼리 횟수 제한)
                .ToArray();

            if (tokens.Length == 0) return string.Empty;

            // ── B_ITEM LIKE 검색 (토큰별, 결과 합산 후 중복 제거) ────
            var seen    = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var matched = new List<(string ItemCd, string ItemNm, string BaseCd, string ItemAcct)>();

            foreach (var token in tokens)
            {
                var esc = token.Replace("'", "''");
                var sql = $@"
                    SELECT TOP 20
                        ITEM_CD,
                        ITEM_NM,
                        ISNULL(BASE_ITEM_CD, '') AS BASE_ITEM_CD,
                        ITEM_ACCT
                    FROM B_ITEM (NOLOCK)
                    WHERE ITEM_NM  LIKE N'%{esc}%'
                      AND VALID_FLG = 'Y'
                    ORDER BY LEN(ITEM_NM), ITEM_CD";

                var qr = await _dbService.ExecuteQueryAsync(sql);
                if (!qr.IsSuccess) continue;

                foreach (var row in qr.Rows)
                {
                    var cd   = row.TryGetValue("ITEM_CD",      out var c) ? c?.ToString() ?? "" : "";
                    var nm   = row.TryGetValue("ITEM_NM",      out var n) ? n?.ToString() ?? "" : "";
                    var base_ = row.TryGetValue("BASE_ITEM_CD", out var b) ? b?.ToString() ?? "" : "";
                    var acct = row.TryGetValue("ITEM_ACCT",    out var a) ? a?.ToString() ?? "" : "";

                    if (string.IsNullOrEmpty(cd) || !seen.Add(cd)) continue;
                    matched.Add((cd, nm, base_, acct));
                }
            }

            if (matched.Count == 0) return string.Empty;

            _logger.LogInformation("[PA999] 품목코드 해석: 토큰={T} → {N}건 매칭",
                string.Join(",", tokens), matched.Count);

            // ── 프롬프트 섹션 생성 ──────────────────────────────────
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("\n## 품목코드 확정값 (WHERE ITEM_CD 에 직접 사용. B_ITEM 서브쿼리 금지)");

            var display = matched.Take(15).ToList();
            foreach (var (cd, nm, baseCd, acct) in display)
            {
                var acctLabel = acct switch
                {
                    "10" => "원자재",
                    "20" => "제품",
                    "30" => "상품",
                    "50" => "반제품",
                    _    => acct
                };
                var baseHint = !string.IsNullOrEmpty(baseCd) ? $" (기본품목: {baseCd})" : "";
                sb.AppendLine($"  {nm} → ITEM_CD = '{cd}' [{acctLabel}{baseHint}]");
            }

            // 다수 품목 IN 절 힌트
            if (display.Count > 1)
            {
                var inClause = string.Join("', '",
                    display.Select(m => m.ItemCd));
                sb.AppendLine($"  ※ 다수 품목 일괄 조회: WHERE ITEM_CD IN ('{inClause}')");
            }

            // 결과 초과 안내
            if (matched.Count > 15)
                sb.AppendLine($"  ※ 총 {matched.Count}건 매칭 (상위 15건만 표시) — 더 구체적인 품목명으로 질문 시 정확도 향상");

            return sb.ToString();
        }

        private async Task<PA999SqlResult> Step3_GenerateSqlAsync(
            string question,
            string schemaCtx,
            PA999MetaContext metaCtx,
            List<string> relevantTables,
            Dictionary<string, string> plantCdMap,
            List<PA999ConversationMessage> history,
            string? orgType       = null,   // ← [RBAC Layer-2] 조직 유형 (BA/BU/PL)
            string? orgCd         = null,   // ← [RBAC Layer-2] 허가 조직 코드 (예: P031)
            string  itemCdSection = "")     // ← [신규] 품목코드 확정값 (Step2.7 B_ITEM 해석 결과)
        {
            // 메타 설명을 추가로 제공해 SQL 정확도 향상
            var metaSection = string.IsNullOrEmpty(metaCtx.MetaDescription)
                ? string.Empty
                : $"\n## 테이블 업무 설명 (PA999_TABLE_META 기준)\n{metaCtx.MetaDescription}";

            // ══════════════════════════════════════════════════════════════
            // ★ STEP A. 공장코드 + PLANT_GUBUN_CD 사전 조회
            //
            //   순서 변경 이유:
            //     allowedSection(우선 사용 테이블) 을 빌드하기 전에
            //     PLANT_GUBUN_CD 를 먼저 파악해야 relevantTables 를 교정할 수 있음.
            //     이전 구조(allowedSection 먼저 빌드 후 PLANT_GUBUN_CD 조회)에서는
            //     Step1 키워드 매칭이 잘못된 생산 테이블(예: P_PROD_ORDER)을
            //     allowedSection 에 먼저 고정시켜 PLANT_GUBUN_CD 규칙을 덮어쓰는 문제 발생.
            // ══════════════════════════════════════════════════════════════

            // 공장 구분 코드 → 올바른 생산 테이블 전체 정의
            // (plantCd → gubunCd) 캐시: relevantTables 교정 및 plantCdSection 주입에 공용 사용
            var plantGubunMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var allProdTableFamilies = new Dictionary<string, (string Hdr, string Dtl)>(StringComparer.OrdinalIgnoreCase)
            {
                { "100", ("P_PROD_DAILY_HDR_CKO087",   "P_PROD_DAILY_DTL_CKO087")   },
                { "200", ("P_PROD_ORDER_HDR_CKO087",   "P_PROD_ORDER_DTL_CKO087")   },
                { "300", ("P_PROD_REMICON_HDR_CKO087", "P_PROD_REMICON_DTL_CKO087") },
                { "400", ("P_PROD_REMITAL_HDR_CKO087", "P_PROD_REMITAL_DTL_CKO087") }
            };

            // ── STEP A-1. 공장코드 섹션 기본 구성 ──────────────────────────────
            var plantCdSection = string.Empty;
            if (plantCdMap.Count > 0)
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("\n## 공장코드 확정값 (아래 값을 WHERE 절에 직접 사용)");
                foreach (var kv in plantCdMap)
                    sb.AppendLine($"  {kv.Key} → PLANT_CD = '{kv.Value}'");

                // 다수 공장 조회 시 IN 절 힌트 자동 생성
                var distinctCds = plantCdMap.Values.Distinct().ToList();
                if (distinctCds.Count > 1)
                {
                    var inClause = string.Join("', '", distinctCds);
                    sb.AppendLine($"  ※ 다수 공장 일괄 조회 시: WHERE PLANT_CD IN ('{inClause}')");
                    sb.AppendLine("  ※ 단일 공장 지정 시: WHERE PLANT_CD = '공장코드'");
                }

                sb.AppendLine("  ※ B_PLANT 서브쿼리 사용 금지. 위 확정 코드를 직접 사용할 것.");

                // ── STEP A-2. PLANT_GUBUN_CD 조회 → plantGubunMap 채우기 ──────────
                try
                {
                    var plantCodes = string.Join(",",
                        plantCdMap.Values.Select(v => $"N'{v.Replace("'", "''")}'"));

                    var gubunSql = $@"
                        SELECT
                             PLANT_CD
                            ,PLANT_GUBUN_CD
                            ,CASE PLANT_GUBUN_CD
                                WHEN '100' THEN '반복제조'
                                WHEN '200' THEN '지역공장'
                                WHEN '300' THEN '레미콘'
                                WHEN '400' THEN '레미탈'
                                ELSE '기타(' + ISNULL(PLANT_GUBUN_CD,'?') + ')'
                             END AS PLANT_GUBUN_NM
                        FROM B_PLANT (NOLOCK)
                        WHERE PLANT_CD IN ({plantCodes})";

                    var gubunResult = await _dbService.ExecuteQueryAsync(gubunSql);

                    if (gubunResult.IsSuccess && gubunResult.Rows.Count > 0)
                    {
                        sb.AppendLine();
                        sb.AppendLine("## 공장 구분 코드 (생산 테이블 선택 기준 — 반드시 준수)");
                        foreach (var row in gubunResult.Rows)
                        {
                            var pcd = row.TryGetValue("PLANT_CD",       out var pc) ? pc?.ToString() ?? "" : "";
                            var gcd = row.TryGetValue("PLANT_GUBUN_CD", out var gc) ? gc?.ToString() ?? "" : "";
                            var gnm = row.TryGetValue("PLANT_GUBUN_NM", out var gn) ? gn?.ToString() ?? "" : "";
                            sb.AppendLine($"  {pcd}: PLANT_GUBUN_CD = '{gcd}' → {gnm}");

                            if (!string.IsNullOrEmpty(pcd) && !string.IsNullOrEmpty(gcd))
                                plantGubunMap[pcd] = gcd;  // 교정 단계에서 사용
                        }
                        sb.AppendLine("  ※ 위 PLANT_GUBUN_CD 에 따라 생산 테이블을 선택할 것 (업무 규칙 참조)");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[PA999] PLANT_GUBUN_CD 조회 실패 (무시) PlantCodes={P}",
                        string.Join(",", plantCdMap.Values));
                }

                plantCdSection = sb.ToString();
            }

            // ── STEP A-3. relevantTables 교정 ──────────────────────────────────
            // Step1 키워드 매칭이 잘못된 생산 테이블군을 선택한 경우를 PLANT_GUBUN_CD 기준으로 수정.
            // (예) 영월(P031, PLANT_GUBUN_CD=100/반복제조) 질문에 Step1이 P_PROD_ORDER(200/지역공장)를
            //      relevantTables에 올린 경우 → P_PROD_DAILY 로 교체
            if (plantGubunMap.Count > 0)
            {
                var wrongProdTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var correctProdTables = new List<string>();

                foreach (var gubunCd in plantGubunMap.Values.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    if (!allProdTableFamilies.TryGetValue(gubunCd, out var correct)) continue;

                    // 이 PLANT_GUBUN_CD 에 맞지 않는 나머지 생산 테이블군 수집
                    foreach (var kv in allProdTableFamilies)
                    {
                        if (!string.Equals(kv.Key, gubunCd, StringComparison.OrdinalIgnoreCase))
                        {
                            wrongProdTables.Add(kv.Value.Hdr);
                            wrongProdTables.Add(kv.Value.Dtl);
                        }
                    }

                    // 올바른 생산 테이블군 수집 (중복 제거)
                    if (!correctProdTables.Contains(correct.Hdr, StringComparer.OrdinalIgnoreCase))
                        correctProdTables.Add(correct.Hdr);
                    if (!correctProdTables.Contains(correct.Dtl, StringComparer.OrdinalIgnoreCase))
                        correctProdTables.Add(correct.Dtl);
                }

                // 잘못된 생산 테이블 제거
                int removedCount = relevantTables.RemoveAll(t => wrongProdTables.Contains(t));

                // 올바른 생산 테이블 추가 (없으면)
                foreach (var tbl in correctProdTables)
                {
                    if (!relevantTables.Contains(tbl, StringComparer.OrdinalIgnoreCase))
                        relevantTables.Add(tbl);
                }

                if (removedCount > 0 || correctProdTables.Any())
                    _logger.LogInformation(
                        "[PA999] 생산 테이블 교정 | 제거={R}건 | 추가={A} | GUBUN={G}",
                        removedCount,
                        string.Join(",", correctProdTables),
                        string.Join(",", plantGubunMap.Select(kv => $"{kv.Key}={kv.Value}")));
            }

            // ── STEP A-4. allowedSection 빌드 (교정된 relevantTables 기준) ──────
            // ★ 반드시 PLANT_GUBUN_CD 교정(STEP A-3) 이후에 빌드해야 올바른 테이블 목록 반영
            var allowedTables = relevantTables.Count > 0
                ? string.Join(", ", relevantTables)
                : "없음";
            var allowedSection = $"\n## 우선 사용 테이블 (아래 테이블을 우선 참고. DB 스키마에 실제 존재하는 테이블이면 추가 사용 가능)\n{allowedTables}";

            // ★ 피드백 패턴 조회 → 시스템 프롬프트에 주입 (RAG 기반 Top-K)
            //   OpenAI 임베딩 API 설정 시: 질문과 유사한 패턴만 선별 주입
            //   미설정 시: 기존 전량 주입 (fallback)
            var feedbackSection = await _logService.BuildFeedbackSectionAsync(question);

            // ★ 사용자 부서/역할 컨텍스트 → 접근 제한 섹션 생성
            var userContextSection = PA999SystemPrompt.BuildUserContextSection(
                string.Empty, string.Empty, string.Empty);

            // ★ [RBAC Layer-2] 조직 범위 하드 제약 섹션 → 프롬프트 최상단에 배치
            //   OrgCd(예: P031) 가 있으면 Claude 가 생성하는 SQL 에 해당 코드만 사용하도록 강제
            var orgConstraintSection = PA999SystemPrompt.BuildOrgConstraintSection(
                orgType ?? string.Empty, orgCd ?? string.Empty);

            // ★ 시스템 프롬프트는 PA999SystemPrompt.cs 에서 중앙 관리
            //   규칙 추가/변경 시 PA999SystemPrompt.cs 만 수정할 것
            var todaySection = PA999SystemPrompt.BuildTodaySection();  // ← 오늘 날짜/이번 달 기간 주입
            var systemPrompt = PA999SystemPrompt.BuildSqlSystemPrompt(
                allowedSection, plantCdSection, metaSection, schemaCtx,
                feedbackSection, userContextSection, orgConstraintSection,
                itemCdSection, todaySection);  // ← 오늘 날짜 섹션 추가

            var messages = new List<PA999ConversationMessage>();
            messages.AddRange(history.TakeLast(6));
            messages.Add(new PA999ConversationMessage { Role = "user", Content = question });

            var response = await CallClaudeAsync(messages, systemPrompt, maxTokens: 1500);

            if (response.Contains("<SQL>") && response.Contains("</SQL>"))
            {
                var start = response.IndexOf("<SQL>") + 5;
                var end   = response.IndexOf("</SQL>");
                return new PA999SqlResult
                {
                    HasSql = true,
                    Sql    = response.Substring(start, end - start).Trim()
                };
            }

            return new PA999SqlResult { HasSql = false };
        }

        // ══════════════════════════════════════════════════════
        // ■ Step 6. 최종 한국어 답변 생성
        // ══════════════════════════════════════════════════════

        private async Task<string> Step6_GenerateAnswerAsync(
            string question,
            string dataCtx,
            List<PA999ConversationMessage> history)
        {
            // ★ 시스템 프롬프트는 PA999SystemPrompt.cs 에서 중앙 관리
            var systemPrompt = PA999SystemPrompt.AnswerGenerationSystemPrompt;

            // ★ 피드백 LESSON 섹션을 답변 프롬프트에도 주입 (Mode A/B 모두 혜택)
            //   → SQL 예시는 Step3에서, 교훈/비즈니스 규칙은 Step6에서 활용
            //   → RAG 활성화 시 질문과 유사한 교훈만 선별 주입
            var feedbackLessons = await BuildAnswerFeedbackSectionAsync(question);
            if (!string.IsNullOrEmpty(feedbackLessons))
                systemPrompt += feedbackLessons;

            var userContent = string.IsNullOrEmpty(dataCtx)
                ? $"질문: {question}\n\n(조회된 데이터 없음 - 일반 지식 기반 답변)"
                : $"질문: {question}\n\n[조회 결과]\n{dataCtx}";

            var messages = new List<PA999ConversationMessage>();
            messages.AddRange(history.TakeLast(4));
            messages.Add(new PA999ConversationMessage { Role = "user", Content = userContent });

            return await CallClaudeAsync(messages, systemPrompt, maxTokens: 2000);
        }

        /// <summary>
        /// Step6 답변 생성용 피드백 섹션 — LESSON(교훈) 위주로 주입.
        /// Mode A(SP) + Mode B(SQL) 모두 답변 품질 향상에 기여.
        /// </summary>
        private async Task<string> BuildAnswerFeedbackSectionAsync(string? userQuestion = null)
        {
            List<PA999FeedbackPattern> patterns;
            if (!string.IsNullOrWhiteSpace(userQuestion))
            {
                // RAG: 유사도 기반 Top-K 패턴
                patterns = await _logService.GetRelevantPatternsAsync(userQuestion!);
            }
            else
            {
                patterns = await _logService.GetFeedbackPatternsAsync();
            }
            if (patterns.Count == 0)
                return string.Empty;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine();
            sb.AppendLine("## 업무 도메인 교훈 (답변 시 참고)");
            sb.AppendLine("아래는 개발자가 등록한 비즈니스 규칙·교훈입니다. 답변 작성 시 참고하세요.");

            int count = 0;
            foreach (var p in patterns)
            {
                // LESSON이 QUERY_PATTERN과 동일한 경우(자동 복사된 것) 건너뜀
                if (string.IsNullOrWhiteSpace(p.Lesson) ||
                    p.Lesson.Trim() == p.QueryPattern?.Trim())
                    continue;

                count++;
                sb.AppendLine($"- {p.Lesson.Trim()}");

                if (count >= 15) break;  // 답변 프롬프트에는 최대 15개 교훈
            }

            return count > 0 ? sb.ToString() : string.Empty;
        }

        // ══════════════════════════════════════════════════════
        // ■ Claude API 호출
        // ══════════════════════════════════════════════════════

        // ══════════════════════════════════════════════════════
        // ■ Claude API 호출 (429 Rate Limit Retry 포함)
        //   최대 3회 재시도 / Exponential Backoff: 2s → 4s → 8s
        //   Retry-After 헤더가 있으면 해당 값(초) 우선 적용
        // ══════════════════════════════════════════════════════

        private const int    MaxRetry      = 3;
        private const double RetryBaseMs   = 2000; // 첫 재시도 2초

        private async Task<string> CallClaudeAsync(
            List<PA999ConversationMessage> messages,
            string? systemPrompt = null,
            int maxTokens = 1000)
        {
            var client = _httpClientFactory.CreateClient("AnthropicClient");

            var requestBody = new
            {
                model      = _options.Model,
                max_tokens = maxTokens,
                system     = systemPrompt,
                messages   = messages.Select(m => new { role = m.Role, content = m.Content })
            };

            var jsonOpts = new JsonSerializerOptions
            {
                DefaultIgnoreCondition =
                    System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };

            var bodyJson = JsonSerializer.Serialize(requestBody, jsonOpts);

            for (int attempt = 0; attempt <= MaxRetry; attempt++)
            {
                var content = new StringContent(bodyJson, Encoding.UTF8, "application/json");

                var response = await client.PostAsync(
                    "https://api.anthropic.com/v1/messages", content);

                // ── 성공 ──────────────────────────────────────────
                if (response.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(
                        await response.Content.ReadAsStringAsync());
                    var text = string.Empty;
                    if (doc.RootElement.TryGetProperty("content", out var contentArr)
                        && contentArr.GetArrayLength() > 0
                        && contentArr[0].TryGetProperty("text", out var textProp))
                    {
                        text = textProp.GetString() ?? string.Empty;
                    }
                    return text;
                }

                // ── 429 Rate Limit / 529 Overloaded → Retry ─────────
                //   429: 사용자 요청 빈도 초과 → 재시도
                //   529: Anthropic 서버 과부하 → 동일하게 재시도 (공식 권장)
                int statusCode = (int)response.StatusCode;
                if ((statusCode == 429 || statusCode == 529) && attempt < MaxRetry)
                {
                    // Retry-After 헤더가 있으면 해당 시간(초) 대기, 없으면 Exponential Backoff
                    // 529(Overloaded)는 429보다 더 긴 대기 적용 (서버 회복 시간 확보)
                    int waitMs;
                    if (response.Headers.TryGetValues("Retry-After", out var retryAfterVals)
                        && int.TryParse(retryAfterVals.FirstOrDefault(), out var retryAfterSec))
                    {
                        waitMs = retryAfterSec * 1000;
                    }
                    else
                    {
                        double baseMs = statusCode == 529 ? RetryBaseMs * 2 : RetryBaseMs;
                        waitMs = (int)(baseMs * Math.Pow(2, attempt)); // 529: 4s,8s,16s / 429: 2s,4s,8s
                    }

                    _logger.LogWarning(
                        "[PA999] Claude {S} {R} | attempt={A}/{M} | 대기 {W}ms 후 재시도",
                        statusCode, statusCode == 429 ? "Rate Limit" : "Overloaded",
                        attempt + 1, MaxRetry, waitMs);

                    await Task.Delay(waitMs);
                    continue;
                }

                // ── 그 외 오류 → 즉시 throw ───────────────────────
                var errBody = await response.Content.ReadAsStringAsync();
                _logger.LogError("[PA999] Claude API 오류: {Status} | {Body}",
                    response.StatusCode, errBody);
                throw new InvalidOperationException(
                    $"Claude API 호출 실패: HTTP {(int)response.StatusCode} {response.StatusCode} | {errBody}");
            }

            // 최대 재시도 초과
            throw new InvalidOperationException(
                $"Claude API Rate Limit: {MaxRetry}회 재시도 후에도 응답 없음. 잠시 후 다시 시도하세요.");
        }

        // ══════════════════════════════════════════════════════
        // ■ SQL 안전성 검증 (강화 버전)
        //   1단계: 주석 제거 후 검사 (/* */ 우회 차단)
        //   2단계: SELECT/WITH 시작 강제
        //   3단계: DML/DDL/시스템 키워드 차단 (단어 경계 기준)
        //   4단계: 서브쿼리 내 DML 중첩 탐지
        //   5단계: SQL 길이 상한 (8KB)
        // ══════════════════════════════════════════════════════

        private static readonly string[] _blockedKeywords =
        {
            // DML
            "INSERT", "UPDATE", "DELETE", "MERGE",
            // DDL
            "DROP", "TRUNCATE", "ALTER", "CREATE", "RENAME",
            // 실행
            "EXEC", "EXECUTE", "SP_EXECUTESQL",
            // 시스템 프로시저/함수
            "XP_", "SP_", "OPENROWSET", "OPENDATASOURCE", "OPENQUERY",
            // 인프라 공격
            "SHUTDOWN", "BACKUP", "RESTORE", "DBCC", "BULK INSERT",
            // 정보 탈취
            "@@VERSION", "@@SERVERNAME", "SYS.DATABASES", "SYS.TABLES",
            "SYS.COLUMNS", "SYS.OBJECTS", "SYS.SERVER_PRINCIPALS",
            // 시간지연 공격
            "WAITFOR", "DELAY"
        };

        private static readonly Regex _sqlCommentPattern = new(
            @"/\*[\s\S]*?\*/|--[^\r\n]*",
            RegexOptions.Compiled);

        private static readonly Regex _nestedDmlPattern = new(
            @"\b(INSERT|UPDATE|DELETE|MERGE)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private (bool IsValid, string Reason) ValidateSqlSecurity(string sql)
        {
            if (string.IsNullOrWhiteSpace(sql))
                return (false, "SQL이 비어있습니다.");

            // 1단계: 주석 제거 후 검사 (/* comment */ 및 -- 우회 차단)
            var stripped = _sqlCommentPattern.Replace(sql, " ");

            // 2단계: 길이 상한
            if (stripped.Length > 8000)
                return (false, "SQL 길이가 허용 한도(8000자)를 초과했습니다.");

            // 3단계: SELECT / WITH 시작 강제 (주석 제거 후 앞뒤 공백·줄바꿈 제거)
            var upper = stripped.ToUpperInvariant().Trim().TrimStart('\r', '\n', ' ', '\t');
            if (!upper.StartsWith("SELECT") && !upper.StartsWith("WITH"))
                return (false, "SELECT 또는 WITH(CTE)로 시작해야 합니다.");

            // 4단계: 차단 키워드 스캔 (단어 경계)
            foreach (var kw in _blockedKeywords)
            {
                // 모든 키워드를 단어 경계(\b)로 검사 — 컬럼명 내 부분 매칭 방지
                // 예: STK_ON_INSP_QTY 안의 "SP_"에 오탐하지 않도록
                bool found = Regex.IsMatch(upper, $@"(?<![A-Z0-9_]){Regex.Escape(kw)}");

                if (found)
                    return (false, $"허용되지 않는 키워드: [{kw}]");
            }

            // 5단계: 서브쿼리 내 DML 중첩 탐지
            //   SELECT 이후 등장하는 INSERT/UPDATE/DELETE/MERGE 패턴 검사
            //   (3단계에서 이미 차단되지만 이중 검증)
            var afterSelect = upper.Length > 6 ? upper[6..] : string.Empty;
            if (_nestedDmlPattern.IsMatch(afterSelect))
                return (false, "서브쿼리 내 데이터 변경 구문이 감지됐습니다.");

            return (true, string.Empty);
        }

        private bool IsSafeQuery(string sql)
        {
            var (isValid, reason) = ValidateSqlSecurity(sql);
            if (!isValid)
                _logger.LogWarning("[PA999][Security] SQL 검증 실패: {Reason} | SQL(전체)={Sql}",
                    reason, sql);  // ★ 전체 SQL 출력하여 원인 파악
            return isValid;
        }

        // ══════════════════════════════════════════════════════
        // ■ [RBAC Layer-2] 생성 SQL 조직 범위 사후 검증
        //
        //   colName  : "PLANT_CD" / "BU_CD" / "BA_CD"
        //   authCd   : 인가된 코드값 (예: "P031")
        //
        //   검증 로직
        //     · SQL 내 colName = 'xxx' 패턴을 모두 추출
        //     · 추출된 코드 중 authCd 와 다른 값이 하나라도 있으면 false
        //     · colName 조건이 아예 없는 경우(전체 조회 의도) → true 허용
        //       (pre-flight 에서 이미 검사했으므로 여기서는 이중 방어만 담당)
        // ══════════════════════════════════════════════════════
        private static bool ValidateSqlOrgCd(string sql, string colName, string authCd)
        {
            // PLANT_CD = 'P001' 또는 PLANT_CD= 'P001' 형태 추출
            var pattern = $@"{Regex.Escape(colName)}\s*=\s*'([^']+)'";
            var matches = Regex.Matches(sql, pattern, RegexOptions.IgnoreCase);

            foreach (Match m in matches)
            {
                var foundCd = m.Groups[1].Value.Trim();
                if (!string.Equals(foundCd, authCd, StringComparison.OrdinalIgnoreCase))
                    return false;  // 비인가 코드 발견 → 차단
            }
            return true;  // 모두 인가 코드이거나 해당 컬럼 조건 없음
        }

        // ══════════════════════════════════════════════════════
        // ■ 쿼리 결과 → 텍스트 포맷
        // ══════════════════════════════════════════════════════

        private string FormatQueryResult(PA999QueryResult result)
        {
            if (result.Rows.Count == 0) return "조회된 데이터가 없습니다.";

            var sb = new StringBuilder();
            sb.AppendLine($"총 {result.Rows.Count}건 조회");
            sb.AppendLine();
            sb.AppendLine(string.Join(" | ", result.Columns));
            sb.AppendLine(string.Join("-+-",
                result.Columns.Select(c => new string('-', Math.Max(c.Length, 4)))));

            foreach (var row in result.Rows.Take(50))
            {
                sb.AppendLine(string.Join(" | ", result.Columns.Select(c =>
                    row.TryGetValue(c, out var v) ? (v?.ToString() ?? "NULL") : "NULL")));
            }
            if (result.Rows.Count > 50)
                sb.AppendLine($"... 외 {result.Rows.Count - 50}건 (상위 50건만 표시)");

            return sb.ToString();
        }

        // ══════════════════════════════════════════════════════
        // ■ 세션 관리
        // ══════════════════════════════════════════════════════

        private List<PA999ConversationMessage> GetOrCreateSession(string sessionId)
        {
            lock (_sessionLock)
            {
                if (!_sessions.ContainsKey(sessionId))
                    _sessions[sessionId] = new List<PA999ConversationMessage>();
                return _sessions[sessionId];
            }
        }

        private void UpdateSession(string sessionId, string question, string answer)
        {
            lock (_sessionLock)
            {
                if (!_sessions.TryGetValue(sessionId, out var history)) return;
                history.Add(new PA999ConversationMessage { Role = "user",      Content = question });
                history.Add(new PA999ConversationMessage { Role = "assistant", Content = answer   });
                if (history.Count > 20)
                    history.RemoveRange(0, 2);
            }
        }

        public void ClearSession(string sessionId)
        {
            lock (_sessionLock)
            {
                _sessions.Remove(sessionId);
                _logger.LogInformation("[PA999] 세션 삭제: {S}", sessionId);
            }
        }

        // ── 유틸리티 ──────────────────────────────────────────

        private string ExtractJsonArray(string text)
        {
            var s = text.IndexOf('[');
            var e = text.LastIndexOf(']') + 1;
            return (s >= 0 && e > s) ? text.Substring(s, e - s) : "[]";
        }

        // ══════════════════════════════════════════════════════
        // ■ Mode A: SP 카탈로그 처리 (업무 로직 + 오류 진단)
        // ══════════════════════════════════════════════════════

        private async Task<PA999ChatResponse> HandleModeA_SpAsync(
            PA999ChatRequest request, PA999RouteResult route,
            List<PA999ConversationMessage> history)
        {
            var sessionId = request.SessionId;
            _logger.LogInformation("[PA999][ModeA] SP 모드 진입 | SP={S} | ErrorMap={E}",
                route.SpEntry?.SpName ?? "N/A",
                route.ErrorMapEntry?.ErrorType ?? "N/A");

            try
            {
                // ── Step A-1. 공장코드 해석 ──
                var resolveResult = await ResolveKoreanNamesToCodesAsync(request.Question);
                var plantCd = request.PlantCd ?? request.OrgCd ?? "";
                if (resolveResult.Codes.Count > 0)
                    plantCd = resolveResult.Codes.Values.First();

                // ── Step A-2. 질문에서 파라미터 추출 (AI 사용) ──
                var paramDefs = route.SpEntry != null
                    ? await _spCatalog.GetParamDefsAsync(route.SpEntry.SpId)
                    : new List<PA999SpParamDef>();

                var paramValues = await ExtractSpParamsWithAiAsync(
                    request.Question, paramDefs, plantCd);

                // ── Step A-3. 오류 진단 모드 (ERROR_MAP 매칭 시) ──
                if (route.ErrorMapEntry != null)
                {
                    var diagResult = await _spCatalog.ExecuteDiagQueryAsync(
                        route.ErrorMapEntry, paramValues);

                    // 진단 데이터를 Claude에게 전달하여 자연어 답변 생성
                    var diagCtx = FormatDiagResult(diagResult, route.ErrorMapEntry);
                    var answer = await Step6_GenerateAnswerAsync(request.Question, diagCtx, history);

                    if (answer.StartsWith("[Text Response]", StringComparison.OrdinalIgnoreCase))
                        answer = answer.Substring("[Text Response]".Length).TrimStart('\r', '\n', ' ');

                    UpdateSession(sessionId, request.Question, answer);

                    // 로그 저장
                    long logSeq = 0;
                    try
                    {
                        logSeq = await _logService.SaveLogAsync(new PA999ChatLogEntry
                        {
                            SessionId     = sessionId,
                            UserId        = request.UserId,
                            UserQuery     = request.Question,
                            AiResponse    = answer,
                            GeneratedSql  = $"[SP_DIAG] {route.ErrorMapEntry.ErrorType}",
                            RelatedTables = route.ErrorMapEntry.DiagTables,
                            IsError       = false
                        });
                    }
                    catch { }

                    return new PA999ChatResponse
                    {
                        Answer       = answer,
                        SessionId    = sessionId,
                        IsError      = false,
                        ProcessMode  = "SP_DIAG",
                        SopGuide     = diagResult.SopActionGuide,
                        MenuPath     = diagResult.SopMenuPath,
                        LogSeq       = logSeq > 0 ? logSeq : null,
                        GridData     = diagResult.AutoCloseDetails?.Count > 0
                            ? diagResult.AutoCloseDetails : null
                    };
                }

                // ── Step A-3.5. 필수 파라미터 누락 체크 → 재질문 ──
                if (route.SpEntry != null && route.ErrorMapEntry == null)
                {
                    var inputDefs = paramDefs.Where(d => !d.IsOutput).ToList();
                    var missingRequired = inputDefs
                        .Where(p => p.IsRequired
                            && (!paramValues.ContainsKey(p.ParamName)
                                || string.IsNullOrEmpty(paramValues[p.ParamName])))
                        .ToList();

                    if (missingRequired.Count > 0)
                    {
                        _logger.LogInformation(
                            "[PA999][ModeA] 필수 파라미터 누락 → 재질문 | SP={SP} | Missing={M}",
                            route.SpEntry.SpName,
                            string.Join(",", missingRequired.Select(p => p.ParamName)));

                        var reQueryAnswer = BuildSpReQueryResponse(
                            route.SpEntry, missingRequired, paramValues, inputDefs);

                        UpdateSession(sessionId, request.Question, reQueryAnswer);

                        long logSeq = 0;
                        try
                        {
                            logSeq = await _logService.SaveLogAsync(new PA999ChatLogEntry
                            {
                                SessionId    = sessionId,
                                UserId       = request.UserId,
                                UserQuery    = request.Question,
                                AiResponse   = reQueryAnswer,
                                GeneratedSql = $"[SP_REQUERY] {route.SpEntry.SpName} | missing: {string.Join(",", missingRequired.Select(p => p.ParamName))} | values: {string.Join(",", paramValues.Select(kv => $"{kv.Key}={kv.Value ?? "NULL"}"))}",
                                IsError      = false
                            });
                        }
                        catch { }

                        return new PA999ChatResponse
                        {
                            Answer      = reQueryAnswer,
                            SessionId   = sessionId,
                            IsError     = false,
                            ProcessMode = "SP",
                            LogSeq      = logSeq > 0 ? logSeq : null
                        };
                    }
                }

                // ── Step A-3.9. 재질문 통과 → 필수 파라미터 기본값 최종 적용 ──
                //   재질문 체크에서 Claude가 추출한 필수 값은 유지
                //   IS_REQUIRED=Y인데 값이 여전히 null인 파라미터에 기본값 적용
                //   (이 시점에 도달하면 모든 필수 파라미터에 값이 있음)
                var nowFill = DateTime.Now;
                foreach (var p in paramDefs.Where(d => !d.IsOutput && d.IsRequired))
                {
                    if (paramValues.ContainsKey(p.ParamName) && !string.IsNullOrEmpty(paramValues[p.ParamName]))
                        continue;
                    var defVal = p.DefaultVal;
                    if (defVal == "사용자소속공장") defVal = plantCd;
                    else if (defVal == "오늘") defVal = nowFill.ToString("yyyyMMdd");
                    else if (defVal == "이번달1일") defVal = nowFill.ToString("yyyyMM") + "01";
                    else if (defVal == "이번달말일") defVal = new DateTime(nowFill.Year, nowFill.Month,
                        DateTime.DaysInMonth(nowFill.Year, nowFill.Month)).ToString("yyyyMMdd");
                    paramValues[p.ParamName] = defVal;
                }

                // ── Step A-4. 일반 SP 실행 (파생 지표 조회 등) ──
                if (route.SpEntry != null)
                {
                    var execResult = await _spCatalog.ExecuteSpAsync(
                        route.SpEntry.SpName, paramValues, paramDefs);

                    // ★ SP 실행 실패 시 Mode B(SQL 생성)로 폴백
                    //   파라미터 추출 실패 또는 잘못된 SP 매칭 방어
                    if (!execResult.IsSuccess)
                    {
                        _logger.LogWarning(
                            "[PA999][ModeA] SP 실행 실패 → Mode B(SQL) 폴백 | SP={SP} | Error={E}",
                            route.SpEntry.SpName, execResult.ErrorMessage);

                        // Mode B 폴백: AskAsync의 SQL 생성 흐름을 재진입하기 위해
                        // request에 SpFallback 플래그를 세팅하고 null 반환 → 호출측에서 처리
                        // ※ 단순 구조 유지를 위해 SQL 폴백 대신 안내 메시지 반환
                        //   (Mode B 전체 재진입은 리팩토링 필요 — 추후 개선 예정)
                        var fallbackMsg = $"'{route.SpEntry.SpDesc}' 조회를 처리하는 중 오류가 발생했습니다.\n"
                                        + $"다음과 같이 질문을 구체화해 주시면 더 정확하게 답변드릴 수 있습니다:\n"
                                        + $"- 공장명(예: 단양공장, P001)\n"
                                        + $"- 조회 기간(예: 2026년 3월)\n"
                                        + $"- 작업장 구분(예: 킬른, 시멘트밀, 원료밀)";
                        return ErrorResponse(sessionId, fallbackMsg);
                    }

                    var dataCtx = FormatSpResult(execResult);
                    var answer = await Step6_GenerateAnswerAsync(request.Question, dataCtx, history);

                    if (answer.StartsWith("[Text Response]", StringComparison.OrdinalIgnoreCase))
                        answer = answer.Substring("[Text Response]".Length).TrimStart('\r', '\n', ' ');

                    UpdateSession(sessionId, request.Question, answer);

                    long logSeq = 0;
                    try
                    {
                        logSeq = await _logService.SaveLogAsync(new PA999ChatLogEntry
                        {
                            SessionId     = sessionId,
                            UserId        = request.UserId,
                            UserQuery     = request.Question,
                            AiResponse    = answer,
                            GeneratedSql  = $"[SP] EXEC {route.SpEntry.SpName} {string.Join(", ", paramValues.Select(kv => $"{kv.Key}='{kv.Value}'"))}",
                            RelatedTables = route.SpEntry.SpName,
                            IsError       = false
                        });
                    }
                    catch { }

                    return new PA999ChatResponse
                    {
                        Answer      = answer,
                        SessionId   = sessionId,
                        IsError     = false,
                        ProcessMode = "SP",
                        LogSeq      = logSeq > 0 ? logSeq : null,
                        GridData    = execResult.Rows.Count > 0 ? execResult.Rows : null
                    };
                }

                // SP 없으면 SQL 폴백
                return ErrorResponse(sessionId, "SP 매칭은 되었으나 실행 정보가 부족합니다.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PA999][ModeA] 처리 오류");
                return ErrorResponse(sessionId, "SP 처리 중 오류가 발생했습니다.");
            }
        }

        // ══════════════════════════════════════════════════════
        // ■ Mode C: SOP 가이드 처리 (절차/조치 안내)
        // ══════════════════════════════════════════════════════

        private PA999ChatResponse HandleModeC_Sop(
            PA999ChatRequest request, PA999RouteResult route,
            List<PA999ConversationMessage> history)
        {
            var sessionId = request.SessionId;
            _logger.LogInformation("[PA999][ModeC] SOP 모드 진입 | {N}건 매칭",
                route.SopEntries?.Count ?? 0);

            var sb = new StringBuilder();
            foreach (var sop in route.SopEntries!)
            {
                if (!string.IsNullOrEmpty(sop.CauseDesc))
                    sb.AppendLine(sop.CauseDesc).AppendLine();
                if (!string.IsNullOrEmpty(sop.ActionGuide))
                    sb.AppendLine(sop.ActionGuide).AppendLine();
                if (!string.IsNullOrEmpty(sop.MenuPath))
                    sb.AppendLine($"메뉴 경로: {sop.MenuPath}").AppendLine();
            }

            var answer = sb.ToString().Trim();
            UpdateSession(sessionId, request.Question, answer);

            // 로그 저장 (Fire-and-Forget)
            _ = _logService.SaveLogAsync(new PA999ChatLogEntry
            {
                SessionId  = sessionId,
                UserId     = request.UserId,
                UserQuery  = request.Question,
                AiResponse = answer,
                GeneratedSql = "[SOP]",
                IsError    = false
            });

            return new PA999ChatResponse
            {
                Answer      = answer,
                SessionId   = sessionId,
                IsError     = false,
                ProcessMode = "SOP",
                SopGuide    = route.SopEntries.FirstOrDefault()?.ActionGuide,
                MenuPath    = route.SopEntries.FirstOrDefault()?.MenuPath
            };
        }

        // ══════════════════════════════════════════════════════
        // ■ Mode A 헬퍼: AI로 SP 파라미터 추출
        // ══════════════════════════════════════════════════════

        private async Task<Dictionary<string, string?>> ExtractSpParamsWithAiAsync(
            string question, List<PA999SpParamDef> paramDefs, string defaultPlantCd)
        {
            var result = new Dictionary<string, string?>();

            if (paramDefs.Count == 0) return result;

            // 파라미터 매핑 힌트를 Claude에게 전달
            var now = DateTime.Now;
            var paramGuide = new StringBuilder();
            paramGuide.AppendLine("사용자 질문에서 아래 SP 파라미터 값을 추출하라.");
            paramGuide.AppendLine("JSON 형식으로만 반환: {\"@P_PARAM1\": \"값1\", ...}");
            paramGuide.AppendLine("설명 텍스트 없이 JSON만 출력.\n");

            paramGuide.AppendLine("## 파라미터 목록:");
            foreach (var p in paramDefs.Where(d => !d.IsOutput))
            {
                paramGuide.Append($"- {p.ParamName} ({p.DataType}): {p.ParamDesc}");
                if (!string.IsNullOrEmpty(p.MappingHint))
                    paramGuide.Append($"  [매핑: {p.MappingHint}]");
                if (!string.IsNullOrEmpty(p.DefaultVal))
                    paramGuide.Append($"  [기본값: {p.DefaultVal}]");
                paramGuide.AppendLine();
            }

            paramGuide.AppendLine($"\n## 날짜 변환 규칙 (YYYYMMDD 8자리, 하이픈 없음):");
            paramGuide.AppendLine($"- 오늘: {now:yyyyMMdd}");
            paramGuide.AppendLine($"- '2026년 3월' → FROM=20260301, TO=20260331");
            paramGuide.AppendLine($"- '2025년 10월' → FROM=20251001, TO=20251031");
            paramGuide.AppendLine($"- '6월' → FROM={now.Year}0601, TO={now.Year}0630");
            paramGuide.AppendLine($"- '이번 달' → FROM={now:yyyyMM}01, TO={new DateTime(now.Year, now.Month, DateTime.DaysInMonth(now.Year, now.Month)):yyyyMMdd}");
            paramGuide.AppendLine($"- '지난 달' → 전월 1일~전월 말일");
            paramGuide.AppendLine($"- '오늘' → FROM={now:yyyyMMdd}, TO={now:yyyyMMdd}");
            paramGuide.AppendLine($"- 날짜 미지정 → null (추출하지 마라)");
            paramGuide.AppendLine($"★ 중요: 질문에 연도+월이 있으면 반드시 FROM/TO를 해당 월의 1일~말일로 변환하라.");
            paramGuide.AppendLine($"\n## 매핑 규칙:");
            paramGuide.AppendLine($"- 화살표(→) 왼쪽이 질문에 포함되면 오른쪽 코드 사용");
            paramGuide.AppendLine($"- 예: '영월→P031' = 질문에 '영월'이 있으면 P031");
            paramGuide.AppendLine($"- 기본 공장코드: {defaultPlantCd} (질문에 공장명 없을 때)");

            var messages = new List<PA999ConversationMessage>
            {
                new() { Role = "user", Content = $"질문: {question}\n\n{paramGuide}" }
            };

            try
            {
                var aiResponse = await CallClaudeAsync(messages,
                    "SP 파라미터 추출 전문가. JSON만 반환하라. 설명 없이 JSON만.",
                    maxTokens: 500);

                // JSON 파싱
                var jsonMatch = System.Text.RegularExpressions.Regex.Match(
                    aiResponse, @"\{[^}]+\}");
                if (jsonMatch.Success)
                {
                    var parsed = System.Text.Json.JsonSerializer.Deserialize<
                        Dictionary<string, System.Text.Json.JsonElement>>(jsonMatch.Value);

                    if (parsed != null)
                    {
                        foreach (var kv in parsed)
                        {
                            var val = kv.Value.ValueKind == System.Text.Json.JsonValueKind.Null
                                ? null : kv.Value.ToString();
                            result[kv.Key] = val;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[PA999][ModeA] AI 파라미터 추출 실패 — 기본값 사용");
            }

            // ★ DEBUG: Claude 추출 결과 (기본값 적용 전)
            _logger.LogInformation("[PA999][ModeA] Claude 추출 결과(raw): {P}",
                string.Join(", ", result.Select(kv => $"{kv.Key}={kv.Value ?? "NULL"}")));

            // ★ 기본값 보충
            //   - 필수 날짜 파라미터(DT_FROM, DT_TO): 기본값 적용 안 함 → 재질문 유도
            //   - 필수 공장 파라미터(PLANT_CD): defaultPlantCd가 있으면 적용
            //   - 비필수: 기본값 적용
            var nowDef = DateTime.Now;
            foreach (var p in paramDefs.Where(d => !d.IsOutput))
            {
                if (!result.ContainsKey(p.ParamName) || string.IsNullOrEmpty(result[p.ParamName]))
                {
                    // 필수 파라미터 중 날짜: 기본값 적용하지 않음 (재질문 유도)
                    bool isDateParam = p.ParamName.Contains("_DT") || p.ParamName.Contains("DATE");
                    if (p.IsRequired && isDateParam)
                    {
                        if (!result.ContainsKey(p.ParamName))
                            result[p.ParamName] = null;
                        continue;
                    }

                    // 필수 공장코드: defaultPlantCd 적용
                    if (p.IsRequired && p.ParamName.Contains("PLANT") && !string.IsNullOrEmpty(defaultPlantCd))
                    {
                        result[p.ParamName] = defaultPlantCd;
                        continue;
                    }

                    var defVal = p.DefaultVal;
                    if (defVal == "사용자소속공장") defVal = defaultPlantCd;
                    else if (defVal == "오늘") defVal = nowDef.ToString("yyyyMMdd");
                    else if (defVal == "이번달1일") defVal = nowDef.ToString("yyyyMM") + "01";
                    else if (defVal == "이번달말일") defVal = new DateTime(nowDef.Year, nowDef.Month,
                        DateTime.DaysInMonth(nowDef.Year, nowDef.Month)).ToString("yyyyMMdd");
                    else if (defVal == "전월1일") defVal = new DateTime(nowDef.Year, nowDef.Month, 1)
                        .AddMonths(-1).ToString("yyyyMMdd");
                    else if (defVal == "전월말일") defVal = new DateTime(nowDef.Year, nowDef.Month, 1)
                        .AddDays(-1).ToString("yyyyMMdd");
                    result[p.ParamName] = defVal;
                }
            }

            _logger.LogInformation("[PA999][ModeA] 파라미터 추출: {P}",
                string.Join(", ", result.Select(kv => $"{kv.Key}={kv.Value}")));

            return result;
        }

        // ══════════════════════════════════════════════════════
        // ■ Mode A 헬퍼: 진단 결과 포맷팅
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// SP 필수 파라미터 누락 시 재질문 안내 메시지 생성
        /// </summary>
        private string BuildSpReQueryResponse(
            PA999SpCatalogEntry sp,
            List<PA999SpParamDef> missingParams,
            Dictionary<string, string?> extractedParams,
            List<PA999SpParamDef> allInputParams)
        {
            var sb = new StringBuilder();
            // SP_DESC에서 사용자 친화적 이름 추출 (예: "PC551Q1 시멘트(반복제조) 생산일보 출력물 — 단양공장" → "시멘트 생산일보")
            var spTitle = sp.SpDesc?.Split('—')[0]?.Trim() ?? sp.SpName;
            // "PC551Q1 " 접두사 제거
            if (spTitle.Length > 8 && spTitle[6] == ' ')
                spTitle = spTitle.Substring(7).Trim();
            // "출력물" 제거
            spTitle = spTitle.Replace("출력물", "").Replace("  ", " ").Trim();
            sb.AppendLine($"**{spTitle}**를 조회하려면 추가 정보가 필요합니다.\n");

            // 누락된 정보
            sb.AppendLine("**필요한 정보:**");
            foreach (var p in missingParams)
            {
                var desc = !string.IsNullOrEmpty(p.ParamDesc) ? p.ParamDesc : p.ParamName;
                sb.Append($"- {desc}");
                // MAPPING_HINT에서 선택지 표시
                if (!string.IsNullOrEmpty(p.MappingHint))
                {
                    var choices = p.MappingHint.Split(',')
                        .Where(h => h.Contains('→'))
                        .Select(h => h.Split('→')[0].Trim())
                        .Take(5)
                        .ToList();
                    if (choices.Count > 0)
                        sb.Append($" (예: {string.Join(", ", choices)})");
                }
                sb.AppendLine();
            }

            // 이미 파악된 정보
            var known = extractedParams
                .Where(kv => !string.IsNullOrEmpty(kv.Value))
                .ToList();
            if (known.Count > 0)
            {
                sb.AppendLine("\n**파악된 정보:**");
                foreach (var kv in known)
                {
                    var def = allInputParams.FirstOrDefault(d => d.ParamName == kv.Key);
                    var desc = def?.ParamDesc ?? kv.Key;
                    sb.AppendLine($"- {desc}: {kv.Value}");
                }
            }

            // 예시 질문 — 파악된 공장명 활용
            var knownPlant = extractedParams
                .Where(kv => kv.Key.Contains("PLANT") && !string.IsNullOrEmpty(kv.Value))
                .Select(kv => kv.Value)
                .FirstOrDefault();
            var plantLabel = !string.IsNullOrEmpty(knownPlant) ? $"({knownPlant}) " : "";

            sb.AppendLine("\n**이렇게 질문해 보세요:**");
            sb.AppendLine($"- \"{spTitle} {plantLabel}2026년 3월\"");
            sb.AppendLine($"- \"{spTitle} {plantLabel}오늘\"");
            sb.AppendLine($"- \"{spTitle} {plantLabel}이번 달\"");

            return sb.ToString();
        }

        private string FormatDiagResult(PA999DiagResult diag, PA999ErrorMapEntry errorMap)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[오류 유형] {errorMap.ErrorType}: {errorMap.ErrorDesc}");
            sb.AppendLine();

            if (diag.AutoCloseHeaders?.Count > 0)
            {
                sb.AppendLine("[자동마감 상태 (P_PROD_AUTO_CLOSE_CKO087)]");
                foreach (var h in diag.AutoCloseHeaders)
                {
                    var status = h.TryGetValue("STATUS_CD", out var s) ? s?.ToString() : "";
                    var txStatus = h.TryGetValue("TX_STATUS", out var t) ? t?.ToString() : "";
                    var wc = h.TryGetValue("WC_CD", out var w) ? w?.ToString() : "";
                    sb.AppendLine($"  작업장={wc} | STATUS_CD={status} | {txStatus}");
                }
                sb.AppendLine();
            }

            if (diag.AutoCloseDetails?.Count > 0)
            {
                sb.AppendLine("[자재별 마감 상세 (P_PROD_AUTO_CLOSE_DTL_CKO087)]");
                sb.AppendLine("  작업장 | 자재코드 | 자재명 | 구분 | 소요량 | 재고량 | 저장위치 | 마감");
                foreach (var d in diag.AutoCloseDetails)
                {
                    sb.AppendLine(string.Format("  {0} | {1} | {2} | {3} | {4} | {5} | {6} | {7}",
                        d.TryGetValue("WC_CD", out var v1) ? v1 : "",
                        d.TryGetValue("CHILD_ITEM_CD", out var v2) ? v2 : "",
                        d.TryGetValue("ITEM_NM", out var v3) ? v3 : "",
                        d.TryGetValue("TRNS_TYPE_NM", out var v4) ? v4 : "",
                        d.TryGetValue("SUBUL_QTY", out var v5) ? v5 : "",
                        d.TryGetValue("CUR_JAEGO_QTY", out var v6) ? v6 : "",
                        d.TryGetValue("SL_CD", out var v7) ? v7 : "",
                        d.TryGetValue("AUTO_MAGAM_GUBUN", out var v8) ? v8 : ""));
                }
                sb.AppendLine();
            }

            if (diag.StockStatus?.Count > 0)
            {
                sb.AppendLine("[부족 자재의 저장위치별 현재 재고 (I_ONHAND_STOCK)]");
                sb.AppendLine("  품목코드 | 품목명 | 저장위치 | 저장위치명 | 양품재고");
                foreach (var s in diag.StockStatus)
                {
                    sb.AppendLine(string.Format("  {0} | {1} | {2} | {3} | {4}",
                        s.TryGetValue("ITEM_CD", out var v1) ? v1 : "",
                        s.TryGetValue("ITEM_NM", out var v2) ? v2 : "",
                        s.TryGetValue("SL_CD", out var v3) ? v3 : "",
                        s.TryGetValue("SL_NM", out var v4) ? v4 : "",
                        s.TryGetValue("GOOD_ON_HAND_QTY", out var v5) ? v5 : ""));
                }
                sb.AppendLine();
            }

            if (!string.IsNullOrEmpty(diag.SopCauseDesc))
            {
                sb.AppendLine("[SOP 원인 설명]");
                sb.AppendLine(diag.SopCauseDesc);
                sb.AppendLine();
            }

            if (!string.IsNullOrEmpty(diag.SopActionGuide))
            {
                sb.AppendLine("[SOP 조치 가이드]");
                sb.AppendLine(diag.SopActionGuide);
                sb.AppendLine();
            }

            if (!string.IsNullOrEmpty(diag.SopMenuPath))
                sb.AppendLine($"[메뉴 경로] {diag.SopMenuPath}");

            return sb.ToString();
        }

        private string FormatSpResult(PA999SpExecutionResult exec)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[SP 실행 결과] {exec.SpName}");

            if (exec.OutputValues.Count > 0)
            {
                sb.AppendLine("[OUTPUT 파라미터]");
                foreach (var kv in exec.OutputValues)
                    sb.AppendLine($"  {kv.Key} = {kv.Value ?? "NULL"}");
                sb.AppendLine();
            }

            if (exec.Rows.Count > 0)
            {
                sb.AppendLine($"[결과 데이터] ({exec.Rows.Count}행)");
                sb.AppendLine(string.Join(" | ", exec.Columns));
                foreach (var row in exec.Rows.Take(50))
                {
                    sb.AppendLine(string.Join(" | ",
                        exec.Columns.Select(c => row.TryGetValue(c, out var v) ? v?.ToString() ?? "" : "")));
                }
            }
            else
            {
                sb.AppendLine("(결과 데이터 없음)");
            }

            return sb.ToString();
        }

        private PA999ChatResponse ErrorResponse(string sessionId, string message) =>
            new() { Answer = message, SessionId = sessionId, IsError = true };
    }

    /// <summary>Step1에서 수집한 메타 컨텍스트 (SQL 생성에 활용)</summary>
    public class PA999MetaContext
    {
        public string SearchMethod    { get; set; } = string.Empty; // KEYWORD_MATCH | DESC_MATCH | SCHEMA_MATCH | CLAUDE_SELECT
        public string MetaDescription { get; set; } = string.Empty; // TABLE_DESC + COLUMN_LIST(PA999_COLUMN_META JOIN) 조합
    }
}
