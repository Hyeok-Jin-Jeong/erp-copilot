using Microsoft.AspNetCore.Mvc;
using Bizentro.App.SV.PP.PA999S1_CKO087.Models;
using Bizentro.App.SV.PP.PA999S1_CKO087.Services;

namespace Bizentro.App.SV.PP.PA999S1_CKO087.Controllers
{
    /// <summary>
    /// PA999S1 - UNIERP AI 챗봇 REST API 컨트롤러
    ///
    /// PA999M1 (UNIERP UI) ↔ PA999S1 (이 서버) 간 통신 엔드포인트
    ///
    /// ▶ 엔드포인트 목록
    ///   POST   /api/PA999/ask              : AI 질문 처리 (핵심)
    ///   DELETE /api/PA999/session/{id}     : 세션(대화 맥락) 초기화
    ///   GET    /api/PA999/health           : 헬스체크
    ///   POST   /api/PA999/meta/batch       : 메타데이터 전체 배치 (DB 스캔 + AI 분석)
    ///   POST   /api/PA999/meta/analyze     : AI 분석만 실행
    ///   GET    /api/PA999/meta/status      : 메타 현황 조회
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class PA999Controller : ControllerBase
    {
        private readonly PA999ChtbotService      _chatbotService;
        private readonly PA999ChatLogService     _logService;
        private readonly ILogger<PA999Controller> _logger;

        public PA999Controller(
            PA999ChtbotService chatbotService,
            PA999ChatLogService logService,
            ILogger<PA999Controller> logger)
        {
            _chatbotService = chatbotService;
            _logService     = logService;
            _logger         = logger;
        }

        // ══════════════════════════════════════════════════════
        // POST /api/PA999/ask
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// AI 챗봇 질문 처리
        /// PA999M1 ModuleViewer.AskAI() 에서 호출
        /// </summary>
        /// <remarks>
        /// Request 예시:
        ///
        ///     POST /api/PA999/ask
        ///     {
        ///         "question"  : "이번 달 사업장별 매출 현황을 알려줘",
        ///         "sessionId" : "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
        ///         "userId"    : "USER001",
        ///         "plantCd"   : "P001"
        ///     }
        ///
        /// Response 예시:
        ///
        ///     {
        ///         "answer"         : "## 2025년 3월 사업장별 매출 현황 ...",
        ///         "sessionId"      : "xxxxxxxx-...",
        ///         "generatedSql"   : null,
        ///         "relevantTables" : ["S_SALES_ORDER", "B_BIZ_AREA"],
        ///         "isError"        : false
        ///     }
        /// </remarks>
        [HttpPost("ask")]
        [ProducesResponseType(typeof(PA999ChatResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<PA999ChatResponse>> Ask([FromBody] PA999ChatRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            _logger.LogInformation(
                "[PA999Controller] Ask | Session={S} | User={U} | Q={Q}",
                request.SessionId, request.UserId, request.Question);

            var response = await _chatbotService.AskAsync(request);
            return Ok(response);
        }

        // ══════════════════════════════════════════════════════
        // DELETE /api/PA999/session/{sessionId}
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 세션(대화 맥락) 초기화
        /// PA999M1 ModuleViewer.btnResetSession_Click() 에서 호출
        /// </summary>
        [HttpDelete("session/{sessionId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IActionResult ClearSession(string sessionId)
        {
            _chatbotService.ClearSession(sessionId);
            _logger.LogInformation("[PA999Controller] Session cleared: {S}", sessionId);
            return Ok(new { message = "세션이 초기화되었습니다.", sessionId });
        }

        // ══════════════════════════════════════════════════════
        // GET /api/PA999/health
        // ══════════════════════════════════════════════════════

        /// <summary>서비스 헬스체크 — 항상 200 OK 반환 (Railway/k8s 프로브용)</summary>
        [HttpGet("health")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IActionResult Health(
            [FromServices] Microsoft.Extensions.Options.IOptions<Models.PA999Options> options)
        {
            var cs        = options.Value.ConnectionString ?? string.Empty;
            var dbStatus  = string.IsNullOrWhiteSpace(cs) || cs.Contains("YOUR_DB")
                ? "unavailable (no db configured)"
                : "configured (not probed)";

            var apiKey    = options.Value.AnthropicApiKey ?? string.Empty;
            var keyStatus = string.IsNullOrWhiteSpace(apiKey) ? "missing" : "configured";

            return Ok(new
            {
                status       = "ok",
                service      = "Bizentro.App.SV.PP.PA999S1_CKO087",
                timestamp    = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                model        = options.Value.Model,
                dbStatus,
                apiKeyStatus = keyStatus
            });
        }


        // ══════════════════════════════════════════════════════
        // POST /api/PA999/meta/batch
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 전체 배치 실행
        /// → INFORMATION_SCHEMA 스캔 + Claude AI 분석 전체 실행
        /// </summary>
        [HttpPost("meta/batch")]
        public async Task<IActionResult> RunMetaBatch(
            [FromServices] PA999MetaBatchService batchService)
        {
            _logger.LogInformation("[PA999Controller] 메타 배치 시작");
            var result = await batchService.RunFullBatchAsync();
            return Ok(new
            {
                isSuccess     = result.IsSuccess,
                scannedCount  = result.ScannedCount,
                analyzedCount = result.AnalyzedCount,
                failedCount   = result.FailedCount,
                errorMessage  = result.ErrorMessage
            });
        }

        // ══════════════════════════════════════════════════════
        // POST /api/PA999/meta/analyze
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// AI 분석만 실행 (SQL 배치 프로시저는 별도 실행된 경우)
        /// </summary>
        [HttpPost("meta/analyze")]
        public async Task<IActionResult> RunMetaAnalyze(
            [FromServices] PA999MetaBatchService batchService)
        {
            var result = await batchService.RunAiAnalysisAsync();
            return Ok(new
            {
                isSuccess     = result.IsSuccess,
                analyzedCount = result.AnalyzedCount,
                failedCount   = result.FailedCount
            });
        }

        // ══════════════════════════════════════════════════════
        // POST /api/PA999/meta/reanalyze-columns
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 기존 USE_YN='Y' 테이블의 컬럼 메타(BIZ_RULE/CODE_VALUES/FK_REF)를 재분석합니다.
        /// 기존 배치(RunAiAnalysisAsync)는 [AI분석대기] 테이블만 처리하므로,
        /// 이미 등록된 테이블에 대해 BIZ_RULE 등을 채우려면 이 엔드포인트를 사용하세요.
        /// </summary>
        /// <remarks>
        /// Request 예시:
        ///
        ///     POST /api/PA999/meta/reanalyze-columns
        ///     {
        ///         "tableNames": ["P_PROD_REMICON_HDR", "P_PROD_REMICON_DTL", "B_PLANT"],
        ///         "overwriteManual": false
        ///     }
        ///
        /// tableNames: null 또는 빈 배열이면 USE_YN='Y' 전체 테이블 처리
        /// overwriteManual: true이면 SRC_TYPE='M'(수동입력) 행도 덮어씀 (기본: false)
        /// </remarks>
        [HttpPost("meta/reanalyze-columns")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> ReanalyzeColumns(
            [FromBody] PA999ReanalyzeRequest request,
            [FromServices] PA999MetaBatchService batchService)
        {
            _logger.LogInformation(
                "[PA999Controller] 컬럼 재분석 시작 | Tables={T} | OverwriteManual={O}",
                request.TableNames?.Count ?? -1, request.OverwriteManual);

            var result = await batchService.ReanalyzeColumnsAsync(
                request.TableNames, request.OverwriteManual);

            return Ok(new
            {
                isSuccess     = result.IsSuccess,
                analyzedCount = result.AnalyzedCount,
                failedCount   = result.FailedCount,
                errorMessage  = result.ErrorMessage
            });
        }

        // ══════════════════════════════════════════════════════
        // GET /api/PA999/meta/status
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// PA999_TABLE_META 현황 조회
        /// </summary>
        [HttpGet("meta/status")]
        public async Task<IActionResult> GetMetaStatus(
            [FromServices] PA999DbService dbService)
        {
            var result = await dbService.ExecuteQueryAsync(@"
                SELECT
                    COUNT(*)                                      AS TOTAL,
                    SUM(CASE WHEN USE_YN='Y' THEN 1 ELSE 0 END)  AS ACTIVE,
                    SUM(CASE WHEN TABLE_DESC LIKE '[AI분석대기]%'
                             THEN 1 ELSE 0 END)                  AS PENDING,
                    SUM(CASE WHEN AUTO_YN='Y' THEN 1 ELSE 0 END) AS AUTO_REG
                FROM PA999_TABLE_META WITH(NOLOCK)");

            if (!result.IsSuccess || result.Rows.Count == 0)
                return StatusCode(500, "메타 현황 조회 실패");

            var row = result.Rows[0];
            return Ok(new
            {
                total   = row.TryGetValue("TOTAL",    out var t) ? t : 0,
                active  = row.TryGetValue("ACTIVE",   out var a) ? a : 0,
                pending = row.TryGetValue("PENDING",  out var p) ? p : 0,
                autoReg = row.TryGetValue("AUTO_REG", out var r) ? r : 0
            });
        }
    
        // ══════════════════════════════════════════════════════
        // PATCH /api/PA999/log/{logSeq}/feedback
        // 개발자 피드백 저장
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 개발자가 AI 응답에 점수(1~5) 및 피드백을 저장합니다.
        /// PA999_CHAT_LOG의 PERF_SCORE, DEV_FEEDBACK 컬럼을 업데이트합니다.
        /// </summary>
        /// <remarks>
        /// Request 예시:
        ///
        ///     PATCH /api/PA999/log/42/feedback
        ///     {
        ///         "perfScore"   : 2,
        ///         "devFeedback" : "공장코드를 서브쿼리로 조회했음. 확정값 사용 필요.",
        ///         "feedbackBy"  : "DEV001"
        ///     }
        /// </remarks>
        [HttpPatch("log/{logSeq:long}/feedback")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> SubmitFeedback(
            long logSeq,
            [FromBody] PA999FeedbackRequest request)
        {
            if (request.PerfScore < 1 || request.PerfScore > 5)
                return BadRequest("점수(PerfScore)는 1~5 사이여야 합니다.");

            var ok = await _logService.UpdateFeedbackAsync(
                logSeq, request.PerfScore, request.DevFeedback,
                request.FeedbackBy ?? "", request.FeedbackType ?? "D");

            if (!ok)
                return NotFound($"LOG_SEQ={logSeq} 를 찾을 수 없습니다.");

            _logger.LogInformation(
                "[PA999Controller] 피드백 저장 LOG_SEQ={Seq} Score={S} By={B} Type={T}",
                logSeq, request.PerfScore, request.FeedbackBy, request.FeedbackType ?? "D");

            return Ok(new { message = "피드백이 저장되었습니다.", logSeq, score = request.PerfScore });
        }

        // ══════════════════════════════════════════════════════
        // GET /api/PA999/log/review
        // 미평가 로그 목록 조회 (피드백 리뷰 화면용)
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// PERF_SCORE가 없는 미평가 로그를 최신순으로 반환합니다.
        /// 개발자 피드백 리뷰 화면에서 사용합니다.
        /// </summary>
        [HttpGet("log/review")]
        [ProducesResponseType(typeof(IEnumerable<PA999LogReviewItem>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetPendingReviews([FromQuery] int top = 50)
        {
            top = Math.Clamp(top, 1, 200);
            var items = await _logService.GetPendingReviewsAsync(top);
            return Ok(new { count = items.Count, items });
        }

        // ══════════════════════════════════════════════════════
        // POST /api/PA999/log/pattern
        // 피드백 패턴 등록 (개발자 전용)
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 개발자가 교정 패턴을 PA999_FEEDBACK_PATTERN에 등록합니다.
        /// 등록 즉시 캐시가 만료되어 다음 질문부터 시스템 프롬프트에 반영됩니다.
        /// </summary>
        /// <remarks>
        /// Request 예시:
        ///
        ///     POST /api/PA999/log/pattern
        ///     {
        ///         "queryPattern"  : "공장명 서브쿼리 사용",
        ///         "wrongApproach" : "WHERE PLANT_CD = (SELECT ... FROM B_PLANT) 형태 → 다중행 오류",
        ///         "correctSql"    : "WHERE PLANT_CD = 'P001'  -- 시스템이 제공한 확정값 사용",
        ///         "lesson"        : "공장코드 확정값이 시스템 프롬프트에 이미 제공됨. B_PLANT 서브쿼리 금지.",
        ///         "logSeq"        : 42,
        ///         "priority"      : 1,
        ///         "createdBy"     : "DEV001"
        ///     }
        /// </remarks>
        [HttpPost("log/pattern")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreatePattern(
            [FromBody] PA999PatternCreateRequest request,
            [FromServices] PA999DbService dbService)
        {
            if (string.IsNullOrWhiteSpace(request.QueryPattern))
                return BadRequest("QueryPattern은 필수입니다.");
            if (string.IsNullOrWhiteSpace(request.Lesson))
                return BadRequest("Lesson은 필수입니다.");

            var sql = $@"
                INSERT INTO PA999_FEEDBACK_PATTERN
                    (LOG_SEQ, QUERY_PATTERN, WRONG_APPROACH, CORRECT_SQL,
                     LESSON, PRIORITY, APPLY_YN, CREATED_BY,
                     UPDT_USER_ID, UPDT_DT)
                OUTPUT INSERTED.PATTERN_SEQ
                VALUES
                    ({(request.LogSeq.HasValue ? request.LogSeq.Value.ToString() : "NULL")},
                     N'{EscSql(request.QueryPattern)}',
                     {NullOrNStr(request.WrongApproach)},
                     {NullOrNStr(request.CorrectSql)},
                     N'{EscSql(request.Lesson)}',
                     {request.Priority},
                     'Y',
                     {NullOrNStr(request.CreatedBy)},
                     ISNULL({NullOrNStr(request.CreatedBy)}, N'SYSTEM'),
                     GETDATE())";

            var result = await dbService.ExecuteQueryAsync(sql);
            if (!result.IsSuccess || result.Rows.Count == 0)
                return StatusCode(500, "패턴 등록 실패: " + result.ErrorMessage);

            var patternSeq = result.Rows[0].TryGetValue("PATTERN_SEQ", out var v)
                 && v is not null and not DBNull
                ? Convert.ToInt32(v) : 0;

            // 캐시 즉시 만료 → 다음 질문부터 반영
            _logService.InvalidatePatternCache();

            _logger.LogInformation(
                "[PA999Controller] 패턴 등록 PATTERN_SEQ={Seq} By={B}",
                patternSeq, request.CreatedBy);

            return CreatedAtAction(nameof(CreatePattern),
                new { patternSeq },
                new { message = "패턴이 등록되었습니다. 다음 질문부터 시스템 프롬프트에 반영됩니다.", patternSeq });
        }

        // ══════════════════════════════════════════════════════
        // DELETE /api/PA999/log/pattern/cache
        // 피드백 패턴 캐시 강제 무효화 (CB990M1에서 SP로 패턴 등록 후 호출)
        // ══════════════════════════════════════════════════════
        [HttpDelete("log/pattern/cache")]
        public IActionResult InvalidatePatternCache()
        {
            _logService.InvalidatePatternCache();
            _logger.LogInformation("[PA999Controller] 피드백 패턴 캐시 강제 무효화 (외부 요청)");
            return Ok(new { message = "패턴 캐시가 무효화되었습니다. 다음 질문부터 최신 패턴이 반영됩니다." });
        }

        // ── 내부 유틸 (Controller 범위) ─────────────────────────
        private static string EscSql(string? s) => (s ?? string.Empty).Replace("'", "''");
        private static string NullOrNStr(string? s)
            => string.IsNullOrWhiteSpace(s) ? "NULL" : $"N'{EscSql(s)}'";

        // ══════════════════════════════════════════════════════
        // POST /api/PA999/admin/query
        // 관리자 전용 SQL 직접 실행 (분석/학습용)
        // ※ 운영 배포 시 반드시 제거 또는 IP 제한 필요
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 관리자 전용 SQL 실행 — SELECT / WITH / EXEC 만 허용
        /// </summary>
        [HttpPost("admin/query")]
        public async Task<IActionResult> AdminQuery(
            [FromBody] PA999AdminQueryRequest request,
            [FromServices] PA999DbService dbService)
        {
            if (string.IsNullOrWhiteSpace(request?.Sql))
                return BadRequest("SQL을 입력하세요.");

            var upper = request.Sql.Trim().ToUpperInvariant();
            if (!upper.StartsWith("SELECT") && !upper.StartsWith("WITH") && !upper.StartsWith("EXEC"))
                return BadRequest("SELECT / WITH / EXEC 만 허용됩니다.");

            var result = await dbService.ExecuteUnlimitedQueryAsync(request.Sql);

            if (!result.IsSuccess)
                return StatusCode(500, result.ErrorMessage);

            return Ok(new
            {
                columns  = result.Columns,
                rows     = result.Rows,
                rowCount = result.Rows.Count
            });
        }

        // ══════════════════════════════════════════════════════
        // ★ DELETE /api/PA999/admin/cache/{tableName}
        // CUD 작업 완료 후 관련 테이블 캐시 즉시 무효화
        // 예) 생산실적 저장 후 → DELETE /api/PA999/admin/cache/M_PROD_RESULT
        // ══════════════════════════════════════════════════════

        /// <summary>특정 테이블 관련 캐시 즉시 삭제 (데이터 변경 시 호출)</summary>
        [HttpDelete("admin/cache/{tableName}")]
        public IActionResult InvalidateCache(
            string tableName,
            [FromServices] PA999QueryCacheService queryCache)
        {
            queryCache.InvalidateByTable(tableName);
            _logger.LogInformation("[PA999][Cache] 수동 무효화 | Table={T}", tableName);
            return Ok(new { message = $"[{tableName}] 관련 캐시 삭제 완료" });
        }

        /// <summary>전체 캐시 강제 초기화 (긴급 상황 시)</summary>
        [HttpDelete("admin/cache")]
        public IActionResult InvalidateAllCache(
            [FromServices] PA999QueryCacheService queryCache)
        {
            queryCache.InvalidateAll();
            _logger.LogWarning("[PA999][Cache] 전체 캐시 강제 초기화");
            return Ok(new { message = "전체 캐시 삭제 완료" });
        }
    }
}
