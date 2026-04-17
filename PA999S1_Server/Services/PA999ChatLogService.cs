using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Bizentro.App.SV.PP.PA999S1_CKO087.Models;

namespace Bizentro.App.SV.PP.PA999S1_CKO087.Services
{
    /// <summary>
    /// PA999 AI 질의 로그 &amp; 피드백 패턴 관리 서비스
    ///
    /// ▶ 주요 기능
    ///   ① SaveLogAsync()              : AskAsync() 완료 시 PA999_CHAT_LOG INSERT (Fire-and-Forget)
    ///   ② UpdateFeedbackAsync()        : 개발자가 점수·피드백 저장 → PA999_CHAT_LOG UPDATE
    ///   ③ GetFeedbackPatternsAsync()   : PA999_FEEDBACK_PATTERN 조회 (10분 캐시)
    ///   ④ BuildFeedbackSectionAsync()  : Step3 시스템 프롬프트 주입용 문자열 생성
    ///   ⑤ GetRelevantPatternsAsync()   : RAG — 사용자 질문과 유사도 기반 Top-K 패턴 검색
    ///
    /// ▶ 설계 원칙
    ///   - 로그 저장 실패가 챗봇 응답에 영향을 주지 않도록 모든 예외를 내부에서 흡수
    ///   - PA999DbService 미사용 (파라미터 바인딩 필요 → SqlParameter 직접 사용)
    ///   - Singleton 등록: 패턴 캐시를 메모리에 유지
    ///   - RAG: 임베딩 API 실패 시 기존 전량 주입으로 자동 fallback
    /// </summary>
    public class PA999ChatLogService
    {
        private readonly string                    _connectionString;
        private readonly ILogger<PA999ChatLogService> _logger;
        private readonly PA999EmbeddingService      _embeddingService;
        private readonly int                        _topK;

        // ── 패턴 캐시 ──────────────────────────────────────────────
        private List<PA999FeedbackPattern> _patternCache  = new();
        private DateTime                   _cacheExpireAt = DateTime.MinValue;
        private readonly SemaphoreSlim     _cacheLock     = new(1, 1);
        private const int CacheMinutes = 10;

        private const int CommandTimeoutSeconds = 10;
        private const double MinSimilarityThreshold = 0.30;

        public PA999ChatLogService(
            IOptions<PA999Options> options,
            ILogger<PA999ChatLogService> logger,
            PA999EmbeddingService embeddingService)
        {
            // 내부망 DB 미접근 시 빠른 실패 (Connect Timeout=5)
            var cs = options.Value.ConnectionString ?? string.Empty;
            var lower = cs.ToLower();
            cs = (lower.Contains("connect timeout") || lower.Contains("connection timeout"))
                ? cs
                : cs.TrimEnd(';') + ";Connect Timeout=5";
            // Railway/클라우드 TLS 핸드셰이크 오류 방지 (Encrypt=False, TrustServerCertificate=True)
            _connectionString  = EnsureRailwayTlsSettings(cs);
            _logger            = logger;
            _embeddingService  = embeddingService;
            _topK              = options.Value.EmbeddingTopK > 0 ? options.Value.EmbeddingTopK : 5;
        }

        private static string EnsureRailwayTlsSettings(string cs)
        {
            if (string.IsNullOrWhiteSpace(cs)) return cs;
            var lower = cs.ToLower();
            if (!lower.Contains("encrypt="))
                cs = cs.TrimEnd(';') + ";Encrypt=False";
            if (!lower.Contains("trustservercertificate="))
                cs = cs.TrimEnd(';') + ";TrustServerCertificate=True";
            return cs;
        }

        // ══════════════════════════════════════════════════════
        // ▶ ① 로그 저장 (AskAsync 완료 직후 호출)
        //     반환값: 생성된 LOG_SEQ (실패 시 0)
        // ══════════════════════════════════════════════════════

        // ══════════════════════════════════════════════════════
        // ▶ PII / 영업비밀 마스킹 패턴 (SaveLogAsync 전처리)
        //   대상: USER_QUERY, AI_RESPONSE
        //   보존: GENERATED_SQL (원본 분석 필요), SESSION_ID, USER_ID
        // ══════════════════════════════════════════════════════

        // ── PII 마스킹 규칙 ────────────────────────────────────
        private static readonly System.Text.RegularExpressions.Regex _maskRrn =
            new(@"\d{6}-[1-4]\d{6}",
                System.Text.RegularExpressions.RegexOptions.Compiled);

        private static readonly System.Text.RegularExpressions.Regex _maskPhone =
            new(@"0\d{1,2}-\d{3,4}-\d{4}",
                System.Text.RegularExpressions.RegexOptions.Compiled);

        private static readonly System.Text.RegularExpressions.Regex _maskEmpNo =
            new(@"\b(EMP|USR)\d{4,}\b",
                System.Text.RegularExpressions.RegexOptions.Compiled |
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        private static readonly System.Text.RegularExpressions.Regex _maskCostCol =
            new(@"(?i)(unit_cost|sale_price|contract_amt|bid_amt|cost_amt)\s*[=:]\s*[\d,]+",
                System.Text.RegularExpressions.RegexOptions.Compiled);

        private static readonly System.Text.RegularExpressions.Regex _maskIp =
            new(@"172\.31\.\d{1,3}\.\d{1,3}",
                System.Text.RegularExpressions.RegexOptions.Compiled);

        private static readonly System.Text.RegularExpressions.Regex _maskCostNum =
            new(@"[\d,]+", System.Text.RegularExpressions.RegexOptions.Compiled);

        private static string MaskPii(string? text)
        {
            if (string.IsNullOrEmpty(text)) return text ?? string.Empty;

            // 주민등록번호  900101-1234567 → 900101-*******
            text = _maskRrn.Replace(text,
                m => m.Value[..6] + "-*******");

            // 전화번호  010-1234-5678 → ***-****-****
            text = _maskPhone.Replace(text, "***-****-****");

            // 사원번호  EMP2041 → EMP****
            text = _maskEmpNo.Replace(text,
                m => m.Value[..3] + new string('*', m.Value.Length - 3));

            // 생산원가/단가/계약금액 컬럼 뒤 숫자
            text = _maskCostCol.Replace(text,
                m => _maskCostNum.Replace(m.Value, "[금액마스킹]"));

            // 내부 IP 주소
            text = _maskIp.Replace(text, "172.31.x.x");

            return text;
        }

        public async Task<long> SaveLogAsync(PA999ChatLogEntry entry)
        {
            const string sql = @"
                INSERT INTO PA999_CHAT_LOG
                    (SESSION_ID, USER_ID, USER_QUERY,
                     AI_RESPONSE, GENERATED_SQL, RELATED_TABLES, IS_ERROR)
                OUTPUT INSERTED.LOG_SEQ
                VALUES
                    (@SESSION_ID, @USER_ID, @USER_QUERY,
                     @AI_RESPONSE, @GENERATED_SQL, @RELATED_TABLES, @IS_ERROR)";

            // PII 마스킹 적용 (질문/응답만 — SQL 원본은 분석용으로 보존)
            var maskedQuery    = MaskPii(entry.UserQuery);
            var maskedResponse = MaskPii(entry.AiResponse);

            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                await using var cmd = new SqlCommand(sql, conn)
                {
                    CommandTimeout = CommandTimeoutSeconds
                };

                cmd.Parameters.AddWithValue("@SESSION_ID",     TruncOrNull(entry.SessionId, 100)   ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@USER_ID",        TruncOrNull(entry.UserId, 50)        ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@USER_QUERY",     Trunc(maskedQuery, 2000));
                cmd.Parameters.AddWithValue("@AI_RESPONSE",    (object?)maskedResponse              ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@GENERATED_SQL",  (object?)entry.GeneratedSql          ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@RELATED_TABLES", TruncOrNull(entry.RelatedTables, 1000) ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@IS_ERROR",       entry.IsError ? 1 : 0);

                var result = await cmd.ExecuteScalarAsync();
                var logSeq = result != null && result != DBNull.Value ? Convert.ToInt64(result) : 0L;

                _logger.LogDebug("[PA999Log] 로그 저장 완료 LOG_SEQ={Seq}", logSeq);
                return logSeq;
            }
            catch (Exception ex)
            {
                // 로그 저장 실패는 챗봇 응답에 영향을 주지 않음 (무시)
                _logger.LogWarning(ex, "[PA999Log] 로그 저장 실패 (무시) Session={S}", entry.SessionId);
                return 0L;
            }
        }

        // ══════════════════════════════════════════════════════
        // ▶ ② 개발자 피드백 저장
        //     PA999_CHAT_LOG의 PERF_SCORE, DEV_FEEDBACK, FEEDBACK_DT, FEEDBACK_BY 업데이트
        // ══════════════════════════════════════════════════════

        /// <param name="feedbackType">"U"=사용자(현업), "D"=개발자. 기본값 "D"</param>
        public async Task<bool> UpdateFeedbackAsync(
            long logSeq, int perfScore, string devFeedback, string feedbackBy,
            string feedbackType = "D")
        {
            if (perfScore < 1 || perfScore > 5)
                throw new ArgumentOutOfRangeException(nameof(perfScore), "점수는 1~5 사이여야 합니다.");

            const string sql = @"
                UPDATE PA999_CHAT_LOG
                SET    PERF_SCORE    = @PERF_SCORE,
                       DEV_FEEDBACK  = @DEV_FEEDBACK,
                       FEEDBACK_DT   = GETDATE(),
                       FEEDBACK_BY   = @FEEDBACK_BY,
                       FEEDBACK_TYPE = @FEEDBACK_TYPE
                WHERE  LOG_SEQ = @LOG_SEQ";

            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                await using var cmd = new SqlCommand(sql, conn)
                {
                    CommandTimeout = CommandTimeoutSeconds
                };

                cmd.Parameters.AddWithValue("@LOG_SEQ",        logSeq);
                cmd.Parameters.AddWithValue("@PERF_SCORE",     perfScore);
                cmd.Parameters.AddWithValue("@DEV_FEEDBACK",   Trunc(devFeedback, 2000));
                cmd.Parameters.AddWithValue("@FEEDBACK_BY",    TruncOrNull(feedbackBy, 50) ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@FEEDBACK_TYPE",  TruncOrNull(feedbackType, 1) ?? "D");

                var affected = await cmd.ExecuteNonQueryAsync();
                _logger.LogInformation("[PA999Log] 피드백 저장 LOG_SEQ={Seq} Score={S} Type={T}",
                    logSeq, perfScore, feedbackType);
                return affected > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PA999Log] 피드백 저장 실패 LOG_SEQ={Seq}", logSeq);
                return false;
            }
        }

        // ══════════════════════════════════════════════════════
        // ▶ ③ 피드백 패턴 조회 (10분 메모리 캐시)
        //     PA999_FEEDBACK_PATTERN WHERE APPLY_YN='Y' ORDER BY PRIORITY
        // ══════════════════════════════════════════════════════

        public async Task<List<PA999FeedbackPattern>> GetFeedbackPatternsAsync()
        {
            if (DateTime.Now < _cacheExpireAt)
                return _patternCache;

            await _cacheLock.WaitAsync();
            try
            {
                // 캐시 재진입 방지 (double-check)
                if (DateTime.Now < _cacheExpireAt)
                    return _patternCache;

                var patterns = await FetchPatternsFromDbAsync();
                _patternCache  = patterns;
                _cacheExpireAt = DateTime.Now.AddMinutes(CacheMinutes);
                _logger.LogDebug("[PA999Log] 피드백 패턴 캐시 갱신 {Count}건", patterns.Count);
                return _patternCache;
            }
            finally
            {
                _cacheLock.Release();
            }
        }

        private async Task<List<PA999FeedbackPattern>> FetchPatternsFromDbAsync()
        {
            // ★ PRIORITY DESC: 에러 교정(P10/P8)이 GOLD(P5)보다 먼저 로드
            //   → TOP 30으로 확장: 패턴 누적 시에도 교정 데이터 유실 방지
            //   → EMBEDDING 컬럼 추가: RAG 벡터 임베딩 (JSON float[])
            const string sql = @"
                SELECT TOP 30
                    PATTERN_SEQ, QUERY_PATTERN,
                    WRONG_APPROACH, CORRECT_SQL, LESSON, PRIORITY, EMBEDDING, PREFERRED_MODE
                FROM PA999_FEEDBACK_PATTERN WITH(NOLOCK)
                WHERE APPLY_YN = 'Y'
                ORDER BY PRIORITY DESC, PATTERN_SEQ DESC";

            var list = new List<PA999FeedbackPattern>();

            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                await using var cmd    = new SqlCommand(sql, conn) { CommandTimeout = CommandTimeoutSeconds };
                await using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var embOrd = reader.GetOrdinal("EMBEDDING");
                    string? embJson = reader.IsDBNull(embOrd) ? null : reader.GetString(embOrd);

                    var modeOrd = reader.GetOrdinal("PREFERRED_MODE");

                    list.Add(new PA999FeedbackPattern
                    {
                        PatternSeq    = reader.GetInt32(reader.GetOrdinal("PATTERN_SEQ")),
                        QueryPattern  = reader.IsDBNull(reader.GetOrdinal("QUERY_PATTERN"))  ? string.Empty : reader.GetString(reader.GetOrdinal("QUERY_PATTERN")),
                        WrongApproach = reader.IsDBNull(reader.GetOrdinal("WRONG_APPROACH")) ? null         : reader.GetString(reader.GetOrdinal("WRONG_APPROACH")),
                        CorrectSql    = reader.IsDBNull(reader.GetOrdinal("CORRECT_SQL"))    ? null         : reader.GetString(reader.GetOrdinal("CORRECT_SQL")),
                        Lesson        = reader.IsDBNull(reader.GetOrdinal("LESSON"))         ? string.Empty : reader.GetString(reader.GetOrdinal("LESSON")),
                        Priority      = reader.GetByte(reader.GetOrdinal("PRIORITY")),
                        Embedding     = PA999EmbeddingService.DeserializeEmbedding(embJson),
                        PreferredMode = reader.IsDBNull(modeOrd) ? null : reader.GetString(modeOrd)
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[PA999Log] 피드백 패턴 DB 조회 실패 — 빈 목록 반환");
            }

            return list;
        }

        // ══════════════════════════════════════════════════════
        // ▶ ④ Step3 시스템 프롬프트 주입용 피드백 섹션 생성
        //     패턴이 없으면 빈 문자열 반환 (프롬프트 미오염)
        // ══════════════════════════════════════════════════════

        // ══════════════════════════════════════════════════════
        // ▶ ⑤ RAG — 사용자 질문과 유사도 기반 Top-K 패턴 검색
        //     임베딩 API 실패 시 전량 반환 (기존 동작 유지)
        // ══════════════════════════════════════════════════════

        public async Task<List<PA999FeedbackPattern>> GetRelevantPatternsAsync(string userQuestion)
        {
            var allPatterns = await GetFeedbackPatternsAsync();

            // 임베딩 서비스 미설정 → 전량 반환 (fallback)
            if (!_embeddingService.IsConfigured)
                return allPatterns;

            // 임베딩이 있는 패턴이 없으면 전량 반환
            var embeddedPatterns = allPatterns.Where(p => p.Embedding != null).ToList();
            if (embeddedPatterns.Count == 0)
            {
                _logger.LogDebug("[RAG] 임베딩된 패턴 0건 — 전량 주입 fallback");
                return allPatterns;
            }

            // 사용자 질문 임베딩
            var questionEmbedding = await _embeddingService.GetEmbeddingAsync(userQuestion);
            if (questionEmbedding == null)
            {
                _logger.LogDebug("[RAG] 질문 임베딩 실패 — 전량 주입 fallback");
                return allPatterns;
            }

            // 코사인 유사도 계산 → Top-K 필터링
            var scored = embeddedPatterns
                .Select(p => (pattern: p, score: PA999EmbeddingService.CosineSimilarity(questionEmbedding, p.Embedding!)))
                .OrderByDescending(x => x.score)
                .ToList();

            var relevant = scored
                .Take(_topK)
                .Where(x => x.score >= MinSimilarityThreshold)
                .Select(x => x.pattern)
                .ToList();

            _logger.LogInformation(
                "[RAG] 패턴 유사도 검색 | 전체={All} | 임베딩={Emb} | Top-K={K} | 결과={R} | 최고={Best:F3}",
                allPatterns.Count, embeddedPatterns.Count, _topK, relevant.Count,
                scored.Count > 0 ? scored[0].score : 0.0);

            // Top-K 결과가 0건이면 fallback (임계값 이하만 존재)
            if (relevant.Count == 0)
            {
                _logger.LogDebug("[RAG] 유사도 임계값({T}) 이상 패턴 없음 — 전량 주입 fallback",
                    MinSimilarityThreshold);
                return allPatterns;
            }

            return relevant;
        }

        public async Task<string> BuildFeedbackSectionAsync(string? userQuestion = null)
        {
            List<PA999FeedbackPattern> patterns;

            if (!string.IsNullOrWhiteSpace(userQuestion) && _embeddingService.IsConfigured)
            {
                // RAG: 유사도 기반 Top-K 패턴만 주입
                patterns = await GetRelevantPatternsAsync(userQuestion);
            }
            else
            {
                // 기존 방식: 전량 주입
                patterns = await GetFeedbackPatternsAsync();
            }

            if (patterns.Count == 0)
                return string.Empty;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine();
            sb.AppendLine("## 과거 피드백 교정 패턴 (아래 실수를 반복하지 말 것)");
            sb.AppendLine("다음은 실제 사용자 질의에서 발생한 오류를 개발자가 직접 교정한 패턴입니다.");
            sb.AppendLine("동일한 실수를 반복하지 마세요.");

            for (int i = 0; i < patterns.Count; i++)
            {
                var p = patterns[i];
                sb.AppendLine();
                sb.AppendLine($"### [교정패턴 {i + 1}] {p.QueryPattern}");
                sb.AppendLine($"- **교훈**: {p.Lesson}");

                if (!string.IsNullOrWhiteSpace(p.WrongApproach))
                    sb.AppendLine($"- **잘못된 접근**: {p.WrongApproach}");

                if (!string.IsNullOrWhiteSpace(p.CorrectSql))
                {
                    sb.AppendLine("- **올바른 SQL 예시**:");
                    sb.AppendLine("```sql");
                    sb.AppendLine(p.CorrectSql.Trim());
                    sb.AppendLine("```");
                }
            }

            return sb.ToString();
        }

        // ══════════════════════════════════════════════════════
        // ▶ ⑤ 미평가 로그 조회 (피드백 리뷰 화면용)
        // ══════════════════════════════════════════════════════

        public async Task<List<PA999LogReviewItem>> GetPendingReviewsAsync(int topN = 50)
        {
            var sql = $@"
                SELECT TOP {topN}
                    LOG_SEQ, SESSION_ID, USER_ID, USER_QUERY,
                    GENERATED_SQL, AI_RESPONSE, IS_ERROR, CREATED_DT, PERF_SCORE
                FROM PA999_CHAT_LOG WITH(NOLOCK)
                WHERE PERF_SCORE IS NULL
                ORDER BY CREATED_DT DESC";

            var list = new List<PA999LogReviewItem>();

            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                await using var cmd    = new SqlCommand(sql, conn) { CommandTimeout = CommandTimeoutSeconds };
                await using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    list.Add(new PA999LogReviewItem
                    {
                        LogSeq       = reader.GetInt64(reader.GetOrdinal("LOG_SEQ")),
                        SessionId    = reader.IsDBNull(reader.GetOrdinal("SESSION_ID")) ? string.Empty : reader.GetString(reader.GetOrdinal("SESSION_ID")),
                        UserId       = reader.IsDBNull(reader.GetOrdinal("USER_ID"))    ? null         : reader.GetString(reader.GetOrdinal("USER_ID")),
                        UserQuery    = reader.IsDBNull(reader.GetOrdinal("USER_QUERY")) ? string.Empty : reader.GetString(reader.GetOrdinal("USER_QUERY")),
                        GeneratedSql = reader.IsDBNull(reader.GetOrdinal("GENERATED_SQL")) ? null     : reader.GetString(reader.GetOrdinal("GENERATED_SQL")),
                        AiResponse   = reader.IsDBNull(reader.GetOrdinal("AI_RESPONSE"))   ? null     : reader.GetString(reader.GetOrdinal("AI_RESPONSE")),
                        IsError      = reader.GetBoolean(reader.GetOrdinal("IS_ERROR")),
                        CreatedDt    = reader.GetDateTime(reader.GetOrdinal("CREATED_DT")),
                        PerfScore    = reader.IsDBNull(reader.GetOrdinal("PERF_SCORE")) ? null : (int?)reader.GetByte(reader.GetOrdinal("PERF_SCORE"))
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PA999Log] 미평가 로그 조회 실패");
            }

            return list;
        }

        // ══════════════════════════════════════════════════════
        // ▶ ⑤-b 피드백 패턴 자동 UPSERT (저장 시 자동 등록/수정)
        //     QUERY_PATTERN 기준 중복 체크: 있으면 UPDATE, 없으면 INSERT
        // ══════════════════════════════════════════════════════

        public async Task<bool> UpsertFeedbackPatternAsync(
            long logSeq, string queryPattern, string? wrongApproach,
            string? correctSql, string? lesson, byte priority,
            string? preferredMode, string userId)
        {
            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                // UPSERT: QUERY_PATTERN 기준 (동일 질문 패턴이면 UPDATE)
                const string sql = @"
                    IF EXISTS (SELECT 1 FROM PA999_FEEDBACK_PATTERN WHERE QUERY_PATTERN = @QP AND APPLY_YN = 'Y')
                    BEGIN
                        UPDATE PA999_FEEDBACK_PATTERN SET
                            WRONG_APPROACH  = @WA,
                            CORRECT_SQL     = @CS,
                            LESSON          = @LS,
                            PRIORITY        = @PR,
                            PREFERRED_MODE  = @PM,
                            UPDT_USER_ID    = @UID,
                            UPDT_DT         = GETDATE()
                        WHERE QUERY_PATTERN = @QP AND APPLY_YN = 'Y'
                    END
                    ELSE
                    BEGIN
                        INSERT INTO PA999_FEEDBACK_PATTERN
                            (LOG_SEQ, QUERY_PATTERN, WRONG_APPROACH, CORRECT_SQL,
                             LESSON, PRIORITY, APPLY_YN, PREFERRED_MODE,
                             INSRT_USER_ID, INSRT_DT, UPDT_USER_ID, UPDT_DT)
                        VALUES
                            (@SEQ, @QP, @WA, @CS, @LS, @PR, 'Y', @PM,
                             @UID, GETDATE(), @UID, GETDATE())
                    END";

                await using var cmd = new SqlCommand(sql, conn) { CommandTimeout = CommandTimeoutSeconds };
                cmd.Parameters.AddWithValue("@SEQ", logSeq);
                cmd.Parameters.AddWithValue("@QP",  queryPattern ?? "");
                cmd.Parameters.AddWithValue("@WA",  (object?)wrongApproach ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@CS",  (object?)correctSql ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@LS",  lesson ?? "");
                cmd.Parameters.AddWithValue("@PR",  priority);
                cmd.Parameters.AddWithValue("@PM",  (object?)preferredMode ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@UID", userId ?? "SYSTEM");

                await cmd.ExecuteNonQueryAsync();
                _logger.LogInformation("[PA999Log] 패턴 UPSERT 완료 | QP={QP} | Priority={P}", queryPattern, priority);

                InvalidatePatternCache();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[PA999Log] 패턴 UPSERT 실패");
                return false;
            }
        }

        /// <summary>LOG_SEQ로 원본 질문/SQL 조회 (패턴 UPSERT용)</summary>
        public async Task<(string UserQuery, string? GeneratedSql)?> GetChatLogBasicAsync(long logSeq)
        {
            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                var sql = "SELECT USER_QUERY, GENERATED_SQL FROM PA999_CHAT_LOG WITH(NOLOCK) WHERE LOG_SEQ = @SEQ";
                await using var cmd = new SqlCommand(sql, conn) { CommandTimeout = CommandTimeoutSeconds };
                cmd.Parameters.AddWithValue("@SEQ", logSeq);

                await using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return (
                        reader.IsDBNull(0) ? "" : reader.GetString(0),
                        reader.IsDBNull(1) ? null : reader.GetString(1)
                    );
                }
                return null;
            }
            catch { return null; }
        }

        // ══════════════════════════════════════════════════════
        // ▶ ⑥ 패턴 임베딩 일괄 생성/갱신 (관리자 API용)
        // ══════════════════════════════════════════════════════

        public async Task<int> EmbedAllPatternsAsync()
        {
            // ★ TOP 30 캐시가 아닌 DB 전체 조회 (65건+ 모두 임베딩)
            var patterns = new List<PA999FeedbackPattern>();
            const string sqlAll = @"
                SELECT PATTERN_SEQ, QUERY_PATTERN, LESSON
                FROM PA999_FEEDBACK_PATTERN WITH(NOLOCK)
                WHERE APPLY_YN = 'Y'
                ORDER BY PATTERN_SEQ";

            try
            {
                await using var connQ = new SqlConnection(_connectionString);
                await connQ.OpenAsync();
                await using var cmdQ = new SqlCommand(sqlAll, connQ) { CommandTimeout = CommandTimeoutSeconds };
                await using var reader = await cmdQ.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    patterns.Add(new PA999FeedbackPattern
                    {
                        PatternSeq   = reader.GetInt32(0),
                        QueryPattern = reader.IsDBNull(1) ? "" : reader.GetString(1),
                        Lesson       = reader.IsDBNull(2) ? "" : reader.GetString(2)
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[RAG] EmbedAll 패턴 전체 조회 실패");
                return 0;
            }

            var textsToEmbed = patterns
                .Select(p => $"{p.QueryPattern} {p.Lesson}".Trim())
                .ToList();

            if (textsToEmbed.Count == 0) return 0;

            var embeddings = await _embeddingService.GetBatchEmbeddingsAsync(textsToEmbed);
            if (embeddings == null || embeddings.Count != patterns.Count)
            {
                _logger.LogWarning("[RAG] 배치 임베딩 실패 또는 개수 불일치 (요청={Req} 응답={Resp})",
                    patterns.Count, embeddings?.Count ?? 0);
                return 0;
            }

            int updated = 0;
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            for (int i = 0; i < patterns.Count; i++)
            {
                var json = PA999EmbeddingService.SerializeEmbedding(embeddings[i]);
                var sql  = "UPDATE PA999_FEEDBACK_PATTERN SET EMBEDDING = @EMB WHERE PATTERN_SEQ = @SEQ";

                await using var cmd = new SqlCommand(sql, conn) { CommandTimeout = CommandTimeoutSeconds };
                cmd.Parameters.AddWithValue("@EMB", json);
                cmd.Parameters.AddWithValue("@SEQ", patterns[i].PatternSeq);

                updated += await cmd.ExecuteNonQueryAsync();
            }

            // 캐시 무효화 → 다음 조회 시 임베딩 포함 재로드
            InvalidatePatternCache();

            _logger.LogInformation("[RAG] 패턴 임베딩 일괄 완료 {Updated}/{Total}건", updated, patterns.Count);
            return updated;
        }

        /// <summary>임베딩이 없는 패턴만 선별하여 임베딩 생성 (CB990M1 캐시 무효화 시 호출)</summary>
        public async Task<int> EmbedMissingPatternsAsync()
        {
            if (!_embeddingService.IsConfigured) return 0;

            // ★ TOP 30 캐시가 아닌 DB에서 EMBEDDING IS NULL인 패턴만 직접 조회
            var missing = new List<PA999FeedbackPattern>();
            try
            {
                const string sql = @"
                    SELECT PATTERN_SEQ, QUERY_PATTERN, LESSON
                    FROM PA999_FEEDBACK_PATTERN WITH(NOLOCK)
                    WHERE APPLY_YN = 'Y' AND EMBEDDING IS NULL
                    ORDER BY PATTERN_SEQ";

                await using var connM = new SqlConnection(_connectionString);
                await connM.OpenAsync();
                await using var cmdM = new SqlCommand(sql, connM) { CommandTimeout = CommandTimeoutSeconds };
                await using var reader = await cmdM.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    missing.Add(new PA999FeedbackPattern
                    {
                        PatternSeq   = reader.GetInt32(0),
                        QueryPattern = reader.IsDBNull(1) ? "" : reader.GetString(1),
                        Lesson       = reader.IsDBNull(2) ? "" : reader.GetString(2)
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[RAG] EmbedMissing 미생성 패턴 조회 실패");
                return 0;
            }

            if (missing.Count == 0)
            {
                _logger.LogDebug("[RAG] 임베딩 미생성 패턴 없음 — 스킵");
                return 0;
            }

            var texts = missing.Select(p => $"{p.QueryPattern} {p.Lesson}".Trim()).ToList();
            var embeddings = await _embeddingService.GetBatchEmbeddingsAsync(texts);
            if (embeddings == null || embeddings.Count != missing.Count) return 0;

            int updated = 0;
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            for (int i = 0; i < missing.Count; i++)
            {
                var json = PA999EmbeddingService.SerializeEmbedding(embeddings[i]);
                var sql  = "UPDATE PA999_FEEDBACK_PATTERN SET EMBEDDING = @EMB WHERE PATTERN_SEQ = @SEQ";
                await using var cmd = new SqlCommand(sql, conn) { CommandTimeout = CommandTimeoutSeconds };
                cmd.Parameters.AddWithValue("@EMB", json);
                cmd.Parameters.AddWithValue("@SEQ", missing[i].PatternSeq);
                updated += await cmd.ExecuteNonQueryAsync();
            }

            InvalidatePatternCache(); // 임베딩 포함 재로드
            _logger.LogInformation("[RAG] 미생성 패턴 임베딩 보충 완료 {Updated}/{Missing}건", updated, missing.Count);
            return updated;
        }

        /// <summary>단일 패턴 임베딩 생성 후 DB 저장 (패턴 등록/수정 시 호출)</summary>
        public async Task EmbedSinglePatternAsync(int patternSeq, string queryPattern, string lesson)
        {
            if (!_embeddingService.IsConfigured) return;

            var text = $"{queryPattern} {lesson}".Trim();
            var embedding = await _embeddingService.GetEmbeddingAsync(text);
            if (embedding == null) return;

            var json = PA999EmbeddingService.SerializeEmbedding(embedding);

            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                var sql = "UPDATE PA999_FEEDBACK_PATTERN SET EMBEDDING = @EMB WHERE PATTERN_SEQ = @SEQ";
                await using var cmd = new SqlCommand(sql, conn) { CommandTimeout = CommandTimeoutSeconds };
                cmd.Parameters.AddWithValue("@EMB", json);
                cmd.Parameters.AddWithValue("@SEQ", patternSeq);
                await cmd.ExecuteNonQueryAsync();

                _logger.LogDebug("[RAG] 패턴 임베딩 저장 PATTERN_SEQ={Seq}", patternSeq);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[RAG] 패턴 임베딩 저장 실패 PATTERN_SEQ={Seq}", patternSeq);
            }
        }

        // ══════════════════════════════════════════════════════
        // ▶ 패턴 캐시 강제 만료 (패턴 등록 직후 즉시 반영용)
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 질문과 유사한 패턴들의 PREFERRED_MODE를 조회하여
        /// 가장 많이 권장되는 모드를 반환. 라우팅 보정에 사용.
        /// null이면 패턴이 모드 제약을 두지 않음.
        /// </summary>
        public async Task<string?> GetPreferredModeAsync(string userQuestion)
        {
            var result = await GetPreferredModeWithScoreAsync(userQuestion);
            return result.Mode;
        }

        /// <summary>
        /// RAG 선행 라우팅용: PREFERRED_MODE + 최고 유사도 점수를 함께 반환.
        /// ModeRouter에서 신뢰도 기반 분기에 사용.
        /// </summary>
        public async Task<(string? Mode, double BestScore)> GetPreferredModeWithScoreAsync(string userQuestion)
        {
            if (!_embeddingService.IsConfigured) return (null, 0.0);

            var allPatterns = await GetFeedbackPatternsAsync();
            var embeddedPatterns = allPatterns.Where(p => p.Embedding != null).ToList();
            if (embeddedPatterns.Count == 0) return (null, 0.0);

            var questionEmbedding = await _embeddingService.GetEmbeddingAsync(userQuestion);
            if (questionEmbedding == null) return (null, 0.0);

            // 코사인 유사도 계산 → Top-K
            var scored = embeddedPatterns
                .Select(p => (pattern: p, score: PA999EmbeddingService.CosineSimilarity(questionEmbedding, p.Embedding!)))
                .OrderByDescending(x => x.score)
                .Take(_topK)
                .Where(x => x.score >= MinSimilarityThreshold)
                .ToList();

            double bestScore = scored.Count > 0 ? scored[0].score : 0.0;

            // ★ RAG 선행 모드 결정 전략:
            //   - 최고 유사도 ≥ 0.75: Top-1 패턴의 PREFERRED_MODE 직접 사용 (다수결 무시)
            //     → "영월 생산일보 보여줘"(0.82 SP) vs 나머지 SQL이라도 SP 확정
            //   - 최고 유사도 < 0.75: 기존 다수결 방식 유지
            //     → 낮은 유사도에서는 Top-1만 신뢰하기 어려우므로 다수결이 안전

            // ① Top-1 고신뢰도 직접 결정
            if (bestScore >= 0.75)
            {
                var bestPattern = scored[0].pattern;
                if (!string.IsNullOrWhiteSpace(bestPattern.PreferredMode))
                {
                    var mode = bestPattern.PreferredMode!.ToUpper();
                    _logger.LogInformation(
                        "[RAG] PREFERRED_MODE 직접 결정(Top-1): {Mode} | Score={Score:F3} | Pattern={Pat}",
                        mode, bestScore, bestPattern.QueryPattern?[..Math.Min(50, bestPattern.QueryPattern.Length)]);
                    return (mode, bestScore);
                }
            }

            // ② 낮은 유사도: 다수결 방식
            var modesWithPref = scored
                .Where(x => !string.IsNullOrWhiteSpace(x.pattern.PreferredMode))
                .Select(x => x.pattern.PreferredMode!.ToUpper())
                .ToList();

            if (modesWithPref.Count == 0) return (null, bestScore);

            var majority = modesWithPref
                .GroupBy(m => m)
                .OrderByDescending(g => g.Count())
                .First();

            if (majority.Count() >= Math.Ceiling(modesWithPref.Count / 2.0))
            {
                _logger.LogInformation(
                    "[RAG] PREFERRED_MODE 다수결: {Mode} ({Count}/{Total}) | BestScore={Score:F3}",
                    majority.Key, majority.Count(), modesWithPref.Count, bestScore);
                return (majority.Key, bestScore);
            }
            return (null, bestScore);
        }

        public void InvalidatePatternCache()
            => _cacheExpireAt = DateTime.MinValue;

        // ── 내부 유틸 ──────────────────────────────────────────

        private static string Trunc(string? s, int maxLen)
            => s is null ? string.Empty : (s.Length > maxLen ? s[..maxLen] : s);

        private static string? TruncOrNull(string? s, int maxLen)
            => s is null ? null : (s.Length > maxLen ? s[..maxLen] : s);
    }
}
