using Bizentro.App.SV.PP.PA999S1_CKO087.Models;

namespace Bizentro.App.SV.PP.PA999S1_CKO087.Services
{
    // ══════════════════════════════════════════════════════════
    // PA999 3분기 자동 라우팅 서비스
    //
    // 사용자 질문을 분석하여 3가지 처리 모드로 자동 분기:
    //   Mode A (SP)  : SP 카탈로그 매칭 → 검증된 SP 실행 (업무 로직, 오류 진단)
    //   Mode B (SQL) : 기존 테이블 메타 기반 SQL 생성 (단순 조회)
    //   Mode C (SOP) : SOP 가이드 테이블 직접 조회 (절차/조치 안내)
    //
    // 라우팅 우선순위: SP 카탈로그 → SOP 키워드 → SQL 폴백
    // ══════════════════════════════════════════════════════════

    public enum PA999Mode
    {
        SP,     // Mode A: SP 카탈로그 실행
        SQL,    // Mode B: AI SQL 생성 (기존 방식)
        SOP     // Mode C: SOP 가이드 조회
    }

    /// <summary>
    /// 라우팅 결과
    /// </summary>
    public class PA999RouteResult
    {
        public PA999Mode Mode { get; set; }

        /// <summary>Mode A: 매칭된 SP 카탈로그 정보</summary>
        public PA999SpCatalogEntry? SpEntry { get; set; }

        /// <summary>Mode A: 매칭된 ERROR_MAP 정보 (오류 진단 시)</summary>
        public PA999ErrorMapEntry? ErrorMapEntry { get; set; }

        /// <summary>Mode C: 매칭된 SOP 가이드 목록</summary>
        public List<PA999SopEntry>? SopEntries { get; set; }

        /// <summary>라우팅 판단 근거 (로그용)</summary>
        public string Reason { get; set; } = string.Empty;

        /// <summary>매칭 신뢰도 (0.0 ~ 1.0)</summary>
        public double Confidence { get; set; }

        /// <summary>
        /// 1차 Mode 실패(결과 0건) 시 전환할 대체 Mode.
        /// Mode A(SP) → Mode B(SQL) fallback이 대표적.
        /// null이면 fallback 없음.
        /// </summary>
        public PA999Mode? FallbackMode { get; set; }
    }

    public class PA999ModeRouter
    {
        private readonly PA999DbService _db;
        private readonly PA999ChatLogService _logService;
        private readonly ILogger<PA999ModeRouter> _logger;

        public PA999ModeRouter(PA999DbService db, PA999ChatLogService logService, ILogger<PA999ModeRouter> logger)
        {
            _db = db;
            _logService = logService;
            _logger = logger;
        }

        /// <summary>
        /// 사용자 질문을 분석하여 처리 모드를 결정
        /// </summary>
        // ══════════════════════════════════════════════════════
        // ■ SOP 우선 키워드
        //   질문에 이 단어가 포함 AND 오류/이상 키워드가 없으면 SOP를 먼저 확인.
        //   단, 오류 진단 관련 문맥("오류","실패","왜","못","안 됨")이 함께 있으면
        //   SOP 우선을 건너뛰고 정상 순서(ERROR_MAP → SP → SQL)로 처리.
        // ══════════════════════════════════════════════════════
        // 강한 SOP 키워드: 오류 문맥이 있어도 SOP 우선 시도 (사용자가 명시적으로 절차를 요청)
        private static readonly string[] _strongSopKeywords =
        {
            "절차", "가이드", "단계", "순서"
        };

        // 약한 SOP 키워드: 오류 문맥이 없을 때만 SOP 우선
        private static readonly string[] _sopPriorityKeywords =
        {
            "방법", "안내", "처리방법", "조치방법", "취소방법", "취소"
        };

        // SOP 우선을 억제하는 오류 문맥 키워드 (이 단어가 있으면 ERROR_MAP 우선)
        // 단, _strongSopKeywords와 함께 있으면 억제 안 함
        private static readonly string[] _errorContextKeywords =
        {
            "왜", "오류", "실패", "안돼", "안 돼", "안되", "안 되", "못", "에러", "문제", "이상"
        };

        // ══════════════════════════════════════════════════════════
        // ■ ERROR_MAP 오류 신호 / 조회 의도 키워드
        //   ERROR_MAP은 사용자가 오류·문제를 보고할 때만 매칭.
        //   순수 조회 의도("~현황", "~목록", "~조회")에는 매칭하지 않음.
        // ══════════════════════════════════════════════════════════

        // 오류 신호: 이 중 하나라도 있으면 ERROR_MAP 매칭 허용
        private static readonly string[] _errorReportIndicators =
        {
            "왜", "오류", "실패", "안돼", "안 돼", "안되", "안 되", "못",
            "에러", "문제", "이상", "뜹니다", "뜨는", "떠요", "나와",
            "부족", "안됐", "누락", "빠졌", "없어"
        };

        // 조회 의도 (일반): 오류 신호 없으면 ERROR_MAP 건너뜀
        private static readonly string[] _queryIntentIndicators =
        {
            "조회", "현황", "목록", "기준정보", "알려줘", "보여줘",
            "어느", "어디", "몇", "얼마", "어떻게"
        };

        // 강한 데이터 조회 의도: SP/SOP 대신 SQL을 선호하는 키워드
        // "조회","알려줘","현황"은 SP/SOP에서도 사용하므로 제외
        private static readonly string[] _strongDataQueryIndicators =
        {
            "목록", "리스트", "기준정보", "추이", "미마감",
            "가동시간", "작업장별", "호기별", "품목별", "월별", "일별",
            "합계", "집계", "비교", "상위", "하위"
        };

        // SP 전용 키워드: 이 키워드가 포함되면 SP 매칭 신뢰도 보너스
        // "일보","월보","리포트" 등은 SP(정형 보고서)에서만 의미 있음
        private static readonly string[] _spExclusiveKeywords =
        {
            "일보", "월보", "리포트", "보고서", "출력", "인쇄"
        };

        public async Task<PA999RouteResult> RouteAsync(string question)
        {
            _logger.LogInformation("[ModeRouter] 라우팅 시작 | Q={Q}", question);

            // ── SOP 우선 감지 ──
            // 강한 SOP 키워드(절차/가이드/단계/순서): 오류 문맥 있어도 SOP 먼저 확인
            // 약한 SOP 키워드(방법/안내 등): 오류 문맥 없을 때만 SOP 먼저
            bool hasStrongSopIntent = _strongSopKeywords.Any(k =>
                question.Contains(k, StringComparison.OrdinalIgnoreCase));
            bool hasWeakSopIntent   = _sopPriorityKeywords.Any(k =>
                question.Contains(k, StringComparison.OrdinalIgnoreCase));
            bool hasErrorContext    = _errorContextKeywords.Any(k =>
                question.Contains(k, StringComparison.OrdinalIgnoreCase));

            bool trySopFirst = hasStrongSopIntent || (hasWeakSopIntent && !hasErrorContext);

            if (trySopFirst)
            {
                // ★ SOP 우선 시 확장 토큰 사용: "절차를"→"절차" 매칭 가능 (C-1 해결)
                var sopFirst = await MatchSopAsync(question, useExpandedTokens: true);
                if (sopFirst.Count > 0)
                {
                    var intentType = hasStrongSopIntent ? "강한 절차 의도" : "약한 절차 의도";
                    _logger.LogInformation("[ModeRouter] → Mode C (SOP) [우선] | {N}건 매칭 | {T} 감지",
                        sopFirst.Count, intentType);
                    return new PA999RouteResult
                    {
                        Mode       = PA999Mode.SOP,
                        SopEntries = sopFirst,
                        Reason     = $"SOP 우선 매칭: {sopFirst.Count}건 ({intentType})",
                        Confidence = 0.90
                    };
                }
            }

            // ── 1순위: ERROR_MAP 매칭 (Mode A — 오류 진단) ──
            // ★ 오류 신호 필수: 오류 보고 키워드가 있어야만 ERROR_MAP 시도
            //   오류 신호 없으면 무조건 건너뜀 (B-5 "생산량 추이" 오매칭 방지)
            bool hasErrorReport  = _errorReportIndicators.Any(k =>
                question.Contains(k, StringComparison.OrdinalIgnoreCase));
            bool hasQueryIntent  = _queryIntentIndicators.Any(k =>
                question.Contains(k, StringComparison.OrdinalIgnoreCase));

            bool skipErrorMap = !hasErrorReport;  // ★ 오류 신호 없으면 항상 건너뜀

            if (skipErrorMap)
            {
                _logger.LogInformation(
                    "[ModeRouter] 순수 조회 의도 감지 → ERROR_MAP 건너뜀 (QueryIntent={QI})",
                    string.Join(",", _queryIntentIndicators.Where(k =>
                        question.Contains(k, StringComparison.OrdinalIgnoreCase))));
            }
            else
            {
                var errorMap = await MatchErrorMapAsync(question);
                if (errorMap != null)
                {
                    var spEntry = errorMap.SpId.HasValue
                        ? await GetSpCatalogByIdAsync(errorMap.SpId.Value)
                        : null;

                    _logger.LogInformation("[ModeRouter] → Mode A (SP+ERROR_MAP) | ErrorType={E} | SP={S}",
                        errorMap.ErrorType, spEntry?.SpName ?? "N/A");

                    return new PA999RouteResult
                    {
                        Mode          = PA999Mode.SP,
                        SpEntry       = spEntry,
                        ErrorMapEntry = errorMap,
                        Reason        = $"ERROR_MAP 매칭: {errorMap.ErrorType} ({errorMap.ErrorDesc})",
                        Confidence    = 0.95
                    };
                }
            }

            // ── 1.5순위: SP 전용 키워드 직접 매핑 (DIRECT_KEYWORDS) ──
            //   리포트 전용 계산식 키워드(연료원단위, 배합비 등)는 특정 SP에서만 조회 가능
            //   → 점수 계산 건너뛰고 즉시 SP 확정
            var directMatch = await MatchDirectKeywordsAsync(question);
            if (directMatch != null)
            {
                _logger.LogInformation(
                    "[ModeRouter] -> Mode A (SP) [DIRECT] | SP={S} | Keyword={K}",
                    directMatch.SpName, directMatch.SpDesc);

                return new PA999RouteResult
                {
                    Mode         = PA999Mode.SP,
                    SpEntry      = directMatch,
                    Reason       = $"DIRECT_KEYWORDS 직접 매핑: {directMatch.SpName} ({directMatch.SpDesc})",
                    Confidence   = 0.95,
                    FallbackMode = PA999Mode.SQL
                };
            }

            // ── 1.7순위: RAG 선행 실행 (DIRECT 매칭 이후) ──
            //   유사도 ≥ 0.75 + PREFERRED_MODE → 즉시 확정 (키워드 라우팅 건너뜀)
            //   유사도 0.50~0.75 → ragHint만 설정 (키워드 라우팅 보조)
            //   유사도 < 0.50 → 키워드 라우팅으로 완전 위임
            string? ragPreferredMode = null;
            double ragBestScore = 0.0;
            try
            {
                var (ragMode, ragScore) = await _logService.GetPreferredModeWithScoreAsync(question);
                ragPreferredMode = ragMode;
                ragBestScore = ragScore;

                if (ragMode != null && ragScore >= 0.75)
                {
                    // ★ RAG 신뢰도 높음 → 즉시 확정
                    var preemptMode = ragMode.ToUpper() switch
                    {
                        "SP"  => PA999Mode.SP,
                        "SOP" => PA999Mode.SOP,
                        _     => PA999Mode.SQL
                    };

                    _logger.LogInformation(
                        "[ModeRouter] → Mode {M} [RAG_PREEMPT({S:F2})] | PreferredMode={PM}",
                        preemptMode, ragScore, ragMode);

                    if (preemptMode == PA999Mode.SP)
                    {
                        // SP 모드: 키워드 매칭으로 최적 SP 선택 (RAG는 모드만 결정)
                        var spForRag = await MatchSpCatalogAsync(question);
                        return new PA999RouteResult
                        {
                            Mode         = PA999Mode.SP,
                            SpEntry      = spForRag,
                            Reason       = $"RAG_PREEMPT({ragScore:F2}): PREFERRED_MODE={ragMode}" +
                                           (spForRag != null ? $" → {spForRag.SpName}" : ""),
                            Confidence   = ragScore,
                            FallbackMode = PA999Mode.SQL
                        };
                    }
                    else if (preemptMode == PA999Mode.SOP)
                    {
                        var sopForRag = await MatchSopAsync(question, useExpandedTokens: true);
                        return new PA999RouteResult
                        {
                            Mode       = PA999Mode.SOP,
                            SopEntries = sopForRag.Count > 0 ? sopForRag : null,
                            Reason     = $"RAG_PREEMPT({ragScore:F2}): PREFERRED_MODE={ragMode}",
                            Confidence = ragScore
                        };
                    }
                    else
                    {
                        return new PA999RouteResult
                        {
                            Mode       = PA999Mode.SQL,
                            Reason     = $"RAG_PREEMPT({ragScore:F2}): PREFERRED_MODE={ragMode}",
                            Confidence = ragScore
                        };
                    }
                }

                if (ragMode != null && ragScore >= 0.50)
                {
                    _logger.LogInformation(
                        "[ModeRouter] RAG 힌트 설정: {Mode} (score={S:F2}) → 키워드 라우팅 보조",
                        ragMode, ragScore);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[ModeRouter] RAG 선행 실행 오류 — 키워드 라우팅으로 위임");
            }

            // ── 2순위: SP 카탈로그 키워드 매칭 (Mode A) ──
            var spMatch = await MatchSpCatalogAsync(question);
            if (spMatch != null && spMatch.MatchScore >= 0.55)   // ★ 임계값 0.55: 낮은 신뢰도는 SOP/SQL로 폴백
            {
                // ★ "목록", "현황", "기준정보" 등 강한 데이터 조회 + 낮은 SP 신뢰도 → SQL 선호
                //   "조회", "알려줘"는 SP 질문에서도 사용하므로 억제 안 함
                bool hasStrongDataQuery = _strongDataQueryIndicators.Any(k =>
                    question.Contains(k, StringComparison.OrdinalIgnoreCase));

                // ★ "미마감": SP에서 처리 불가 → score와 무관하게 항상 SQL
                //   기타 키워드(목록/리스트/기준정보/추이): 낮은 신뢰도일 때만 SQL
                bool forceSQL = question.Contains("미마감", StringComparison.OrdinalIgnoreCase);

                if (forceSQL || (hasStrongDataQuery && spMatch.MatchScore < 0.80))
                {
                    _logger.LogInformation(
                        "[ModeRouter] SP 매칭({SP}, {SC:F2}) 있으나 {R} → SQL 선호",
                        spMatch.SpName, spMatch.MatchScore,
                        forceSQL ? "미마감(SP 미지원)" : "강한 데이터 조회 의도");
                }
                else
                {
                    _logger.LogInformation("[ModeRouter] → Mode A (SP) | SP={S} | Score={SC:F2} | Fallback=SQL",
                        spMatch.SpName, spMatch.MatchScore);

                    return new PA999RouteResult
                    {
                        Mode         = PA999Mode.SP,
                        SpEntry      = spMatch,
                        Reason       = $"SP_CATALOG 매칭: {spMatch.SpName} ({spMatch.SpDesc})",
                        Confidence   = spMatch.MatchScore,
                        FallbackMode = PA999Mode.SQL  // ★ SP 결과 0건 시 SQL로 재시도
                    };
                }
            }

            if (spMatch != null)
                _logger.LogInformation("[ModeRouter] SP 매칭 신뢰도 부족({SC:F2}) → SOP/SQL 확인",
                    spMatch.MatchScore);

            // ── 3순위: SOP 가이드 매칭 (Mode C) ──
            // ★ 강한 데이터 조회("목록","현황","기준정보") + SOP 의도 없음 → SOP 건너뜀
            {   bool hasStrongDataQuery = _strongDataQueryIndicators.Any(k =>
                    question.Contains(k, StringComparison.OrdinalIgnoreCase));
                bool skipSop = hasStrongDataQuery && !hasWeakSopIntent && !hasStrongSopIntent;
                var sopMatches = skipSop ? new List<PA999SopEntry>() : await MatchSopAsync(question);
                if (sopMatches.Count > 0)
                {
                    _logger.LogInformation("[ModeRouter] → Mode C (SOP) | {N}건 매칭", sopMatches.Count);

                    return new PA999RouteResult
                    {
                        Mode       = PA999Mode.SOP,
                        SopEntries = sopMatches,
                        Reason     = $"SOP 매칭: {sopMatches.Count}건",
                        Confidence = 0.85
                    };
                }
            }

            // ── 4순위: SQL 폴백 (Mode B) ──
            _logger.LogInformation("[ModeRouter] → Mode B (SQL) | SP/SOP 매칭 없음");

            return new PA999RouteResult
            {
                Mode = PA999Mode.SQL,
                Reason = "SP 카탈로그/SOP 매칭 없음 → SQL 생성 모드",
                Confidence = 0.7
            };
        }

        // ══════════════════════════════════════════════════════
        // ■ ERROR_MAP 매칭
        // ══════════════════════════════════════════════════════

        private async Task<PA999ErrorMapEntry?> MatchErrorMapAsync(string question)
        {
            try
            {
                // ERROR_MAP 매칭: 조사·어미 제거 변형 포함(Expanded)으로 더 넓게 검색
                // "부족이야" → "부족", "자동마감이" → "자동마감" 등이 정확히 매칭됨
                var tokens = ExtractTokensExpanded(question);
                if (tokens.Count == 0) return null;

                var conditions = string.Join(" OR ",
                    tokens.Select(t => $"KEYWORD_LIST LIKE N'%{EscapeSql(t)}%'"));

                // ★ 최소 2토큰 이상 일치해야 ERROR_MAP 매칭 (단일 일반 키워드 과매칭 방지)
                //   예) "생산량" 단독 → NO_PROD_QTY 오매칭 방지
                //       "재고부족" + "자동마감" 2개 → STOCK_SHORTAGE 정확 매칭
                var matchCount = string.Join(" + \n                           ",
                    tokens.Select(t =>
                        $"CASE WHEN KEYWORD_LIST LIKE N'%{EscapeSql(t)}%' THEN 1 ELSE 0 END"));

                var sql = $@"
                    SELECT TOP 1 ERROR_MAP_ID, ERROR_TYPE, ERROR_DESC, BIZ_PROCESS,
                           SP_ID, DIAG_TABLES, KEYWORD_LIST, PRIORITY
                    FROM PA999_SP_ERROR_MAP
                    WHERE USE_YN = 'Y' AND ({conditions})
                      AND ({matchCount}) >= 2
                    ORDER BY ({matchCount}) DESC, PRIORITY ASC";

                var result = await _db.ExecuteQueryAsync(sql);
                if (!result.IsSuccess || result.Rows.Count == 0) return null;

                var row = result.Rows[0];
                return new PA999ErrorMapEntry
                {
                    ErrorMapId = Convert.ToInt32(row["ERROR_MAP_ID"]),
                    ErrorType  = row["ERROR_TYPE"]?.ToString() ?? "",
                    ErrorDesc  = row["ERROR_DESC"]?.ToString() ?? "",
                    BizProcess = row["BIZ_PROCESS"]?.ToString(),
                    SpId       = row["SP_ID"] != null && row["SP_ID"] is not DBNull
                        ? Convert.ToInt32(row["SP_ID"]) : (int?)null,
                    DiagTables = row["DIAG_TABLES"]?.ToString(),
                    KeywordList = row["KEYWORD_LIST"]?.ToString()
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[ModeRouter] ERROR_MAP 매칭 오류");
                return null;
            }
        }

        // ══════════════════════════════════════════════════════
        // ■ SP 전용 키워드 직접 매핑 (DIRECT_KEYWORDS)
        //   리포트 전용 계산식(연료원단위, 배합비 등)은 SQL 테이블에 컬럼이 없고
        //   특정 SP 내부에서만 계산되므로, 해당 키워드 감지 시 SP를 즉시 확정.
        //   점수 계산/RAG 보정 모두 건너뜀 (가장 높은 매칭 우선순위)
        // ══════════════════════════════════════════════════════

        private async Task<PA999SpCatalogEntry?> MatchDirectKeywordsAsync(string question)
        {
            try
            {
                var sql = @"
                    SELECT SP_ID, SP_NAME, SP_DESC, KEYWORD_LIST,
                           MODULE_CD, CATEGORY, SP_TYPE, DIRECT_KEYWORDS
                    FROM PA999_SP_CATALOG
                    WHERE USE_YN = 'Y'
                      AND DIRECT_KEYWORDS IS NOT NULL
                      AND LEN(DIRECT_KEYWORDS) > 0";

                var result = await _db.ExecuteQueryAsync(sql);
                if (!result.IsSuccess || result.Rows.Count == 0) return null;

                // 질문에서 DIRECT_KEYWORDS의 각 키워드가 포함되는지 확인
                // 가장 긴 키워드 매칭 우선 (예: "연료원단위" > "원단위")
                // ★ 공백 제거 매칭: "영월공장 생산일보" → "영월공장생산일보" → "영월생산일보" 포함 확인
                PA999SpCatalogEntry? bestMatch = null;
                string bestKeyword = "";
                int bestKeywordLen = 0;
                var questionNoSpace = question.Replace(" ", "");

                foreach (var row in result.Rows)
                {
                    var directKw = row["DIRECT_KEYWORDS"]?.ToString();
                    if (string.IsNullOrEmpty(directKw)) continue;

                    var keywords = directKw.Split(',')
                        .Select(k => k.Trim())
                        .Where(k => k.Length >= 2);

                    foreach (var kw in keywords)
                    {
                        // 원본 매칭 + 공백 제거 매칭 (둘 다 시도)
                        bool matched = question.Contains(kw, StringComparison.OrdinalIgnoreCase)
                                    || questionNoSpace.Contains(kw, StringComparison.OrdinalIgnoreCase);
                        if (matched && kw.Length > bestKeywordLen)
                        {
                            bestKeywordLen = kw.Length;
                            bestKeyword = kw;
                            bestMatch = new PA999SpCatalogEntry
                            {
                                SpId       = Convert.ToInt32(row["SP_ID"]),
                                SpName     = row["SP_NAME"]?.ToString() ?? "",
                                SpDesc     = row["SP_DESC"]?.ToString() ?? $"DIRECT: {kw}",
                                ModuleCd   = row["MODULE_CD"]?.ToString(),
                                Category   = row["CATEGORY"]?.ToString(),
                                SpType     = row["SP_TYPE"]?.ToString(),
                                MatchScore = 0.95
                            };
                        }
                    }
                }

                if (bestMatch != null)
                    _logger.LogInformation(
                        "[ModeRouter] DIRECT_KEYWORDS 매칭: '{K}' → {SP}",
                        bestKeyword, bestMatch.SpName);

                return bestMatch;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[ModeRouter] DIRECT_KEYWORDS 매칭 오류");
                return null;
            }
        }

        // ══════════════════════════════════════════════════════
        // ■ SP 카탈로그 매칭
        // ══════════════════════════════════════════════════════

        private async Task<PA999SpCatalogEntry?> MatchSpCatalogAsync(string question)
        {
            try
            {
                // ★ 확장 토큰 사용: "일일생산실적"→"일일생산"(2자 제거) → SP 키워드 매칭 가능
                var tokens = ExtractTokensExpanded(question);
                if (tokens.Count == 0) return null;

                var conditions = string.Join(" OR ",
                    tokens.Select(t => $"KEYWORD_LIST LIKE N'%{EscapeSql(t)}%'"));

                var matchScore = string.Join(" + \n                           ",
                    tokens.Select(t =>
                        $"CASE WHEN KEYWORD_LIST LIKE N'%{EscapeSql(t)}%' THEN 1 ELSE 0 END"));

                var sql = $@"
                    SELECT TOP 1
                        c.SP_ID, c.SP_NAME, c.SP_DESC, c.KEYWORD_LIST,
                        c.MODULE_CD, c.CATEGORY, c.SP_TYPE,
                        ({matchScore}) AS MATCH_SCORE
                    FROM PA999_SP_CATALOG c
                    WHERE c.USE_YN = 'Y'
                      AND ({conditions})
                      AND EXISTS (
                          SELECT 1 FROM PA999_SP_PARAMS p
                          WHERE p.SP_ID = c.SP_ID
                            AND p.USE_YN = 'Y'
                            AND p.IS_OUTPUT = 'N'
                      )
                    ORDER BY MATCH_SCORE DESC,
                             CASE WHEN c.SP_NAME LIKE '%[_]R' THEN 0 ELSE 1 END ASC,
                             c.SP_ID ASC";

                var result = await _db.ExecuteQueryAsync(sql);
                if (!result.IsSuccess || result.Rows.Count == 0) return null;

                var bestRow   = result.Rows[0];
                var dbScore   = bestRow.ContainsKey("MATCH_SCORE")
                                && bestRow["MATCH_SCORE"] is not null and not DBNull
                    ? Convert.ToInt32(bestRow["MATCH_SCORE"]) : 0;

                // ★ 점수 정규화 개선 (한계5 해결)
                //   기존: 0.5 보정 → 3/10 + 0.5 = 0.80 (너무 쉽게 SP 확정)
                //   변경: 0.3 기본 + SP 전용 키워드 보너스 0.15
                //   예) "생산일보 알려줘" → 0.3 + 매칭 + 0.15(일보) = 높은 점수
                //   예) "작업장별 생산량" → 0.3 + 매칭 + 0(보너스없음) = 낮은 점수 → SQL
                bool hasSpExclusive = _spExclusiveKeywords.Any(k =>
                    question.Contains(k, StringComparison.OrdinalIgnoreCase));
                double baseBonus = 0.30;
                double spBonus   = hasSpExclusive ? 0.15 : 0.0;
                var normScore = Math.Min((double)dbScore / Math.Max(tokens.Count, 1) + baseBonus + spBonus, 1.0);

                return new PA999SpCatalogEntry
                {
                    SpId       = Convert.ToInt32(bestRow["SP_ID"]),
                    SpName     = bestRow["SP_NAME"]?.ToString() ?? "",
                    SpDesc     = bestRow["SP_DESC"]?.ToString() ?? "",
                    ModuleCd   = bestRow["MODULE_CD"]?.ToString(),
                    Category   = bestRow["CATEGORY"]?.ToString(),
                    SpType     = bestRow["SP_TYPE"]?.ToString(),
                    MatchScore = normScore
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[ModeRouter] SP_CATALOG 매칭 오류");
                return null;
            }
        }

        private async Task<PA999SpCatalogEntry?> GetSpCatalogByIdAsync(int spId)
        {
            try
            {
                var sql = $@"
                    SELECT SP_ID, SP_NAME, SP_DESC, KEYWORD_LIST, MODULE_CD, CATEGORY, SP_TYPE
                    FROM PA999_SP_CATALOG
                    WHERE SP_ID = {spId} AND USE_YN = 'Y'";

                var result = await _db.ExecuteQueryAsync(sql);
                if (!result.IsSuccess || result.Rows.Count == 0) return null;

                var row = result.Rows[0];
                return new PA999SpCatalogEntry
                {
                    SpId     = Convert.ToInt32(row["SP_ID"]),
                    SpName   = row["SP_NAME"]?.ToString() ?? "",
                    SpDesc   = row["SP_DESC"]?.ToString() ?? "",
                    ModuleCd = row["MODULE_CD"]?.ToString(),
                    Category = row["CATEGORY"]?.ToString(),
                    SpType   = row["SP_TYPE"]?.ToString()
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[ModeRouter] SP_CATALOG ID={I} 조회 오류", spId);
                return null;
            }
        }

        // ══════════════════════════════════════════════════════
        // ■ SOP 매칭
        // ══════════════════════════════════════════════════════

        private async Task<List<PA999SopEntry>> MatchSopAsync(string question, bool useExpandedTokens = false)
        {
            var entries = new List<PA999SopEntry>();
            try
            {
                // ★ SOP 우선 감지 시 확장 토큰 사용: "절차를"→"절차" 매칭 가능
                var tokens = useExpandedTokens ? ExtractTokensExpanded(question) : ExtractTokens(question);
                if (tokens.Count == 0) return entries;

                // SOP의 KEYWORD_LIST에서 매칭 (SP_ID IS NULL인 범용 SOP 우선)
                var conditions = string.Join(" OR ",
                    tokens.Select(t => $"KEYWORD_LIST LIKE N'%{EscapeSql(t)}%'"));

                var sql = $@"
                    SELECT TOP 3 SOP_ID, SP_ID, ERROR_TYPE, CAUSE_DESC,
                           ACTION_GUIDE, MENU_PATH, KEYWORD_LIST, SEVERITY
                    FROM PA999_SP_SOP
                    WHERE USE_YN = 'Y' AND ({conditions})
                    ORDER BY
                        CASE WHEN SP_ID IS NULL THEN 0 ELSE 1 END ASC,
                        SOP_ID ASC";

                var result = await _db.ExecuteQueryAsync(sql);
                if (!result.IsSuccess) return entries;

                foreach (var row in result.Rows)
                {
                    entries.Add(new PA999SopEntry
                    {
                        SopId       = Convert.ToInt32(row["SOP_ID"]),
                        SpId        = row["SP_ID"] != null && row["SP_ID"] is not DBNull
                            ? Convert.ToInt32(row["SP_ID"]) : (int?)null,
                        ErrorType   = row["ERROR_TYPE"]?.ToString(),
                        CauseDesc   = row["CAUSE_DESC"]?.ToString() ?? "",
                        ActionGuide = row["ACTION_GUIDE"]?.ToString() ?? "",
                        MenuPath    = row["MENU_PATH"]?.ToString(),
                        Severity    = row["SEVERITY"]?.ToString()
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[ModeRouter] SOP 매칭 오류");
            }
            return entries;
        }

        // ══════════════════════════════════════════════════════
        // ■ 유틸리티
        // ══════════════════════════════════════════════════════

        /// <summary>질문에서 2자 이상 한글/영문/숫자 토큰 추출 (SP/SOP 매칭용 — 원본 토큰)</summary>
        private static List<string> ExtractTokens(string question)
        {
            return System.Text.RegularExpressions.Regex
                .Matches(question, @"[가-힣A-Za-z0-9]{2,}")
                .Select(m => m.Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// 원본 토큰 + 조사·어미 제거 변형 포함 (ERROR_MAP 전용).
        /// 예) "부족이야" → "부족이", "부족" 추가 → ERROR_MAP KEYWORD_LIST 매칭율 향상.
        /// SP/SOP에는 사용하지 않음: 짧은 루트가 SOP에 오매칭되거나 SP 점수 분모를 부풀림.
        /// </summary>
        private static List<string> ExtractTokensExpanded(string question)
        {
            var raw = System.Text.RegularExpressions.Regex
                .Matches(question, @"[가-힣A-Za-z0-9]{2,}")
                .Select(m => m.Value)
                .ToList();

            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var t in raw)
            {
                result.Add(t);
                // 순수 한글 토큰(3자 이상): 조사·어미 제거 변형 추가
                if (t.Length >= 3 &&
                    System.Text.RegularExpressions.Regex.IsMatch(t, @"^[가-힣]+$"))
                {
                    result.Add(t[..^1]);      // 1자 제거
                    if (t.Length >= 4)
                        result.Add(t[..^2]);  // 2자 제거
                }
            }
            return result
                .Where(t => t.Length >= 2)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>SQL Injection 방지용 이스케이프</summary>
        private static string EscapeSql(string value)
            => value.Replace("'", "''").Replace("%", "[%]").Replace("_", "[_]");
    }

    // ══════════════════════════════════════════════════════════
    // ■ 라우팅용 모델 (DB 행 → C# 객체)
    // ══════════════════════════════════════════════════════════

    public class PA999SpCatalogEntry
    {
        public int     SpId       { get; set; }
        public string  SpName     { get; set; } = "";
        public string  SpDesc     { get; set; } = "";
        public string? ModuleCd   { get; set; }
        public string? Category   { get; set; }
        public string? SpType     { get; set; }
        public double  MatchScore { get; set; }
    }

    public class PA999ErrorMapEntry
    {
        public int     ErrorMapId  { get; set; }
        public string  ErrorType   { get; set; } = "";
        public string  ErrorDesc   { get; set; } = "";
        public string? BizProcess  { get; set; }
        public int?    SpId        { get; set; }
        public string? DiagTables  { get; set; }
        public string? KeywordList { get; set; }
    }

    public class PA999SopEntry
    {
        public int     SopId       { get; set; }
        public int?    SpId        { get; set; }
        public string? ErrorType   { get; set; }
        public string  CauseDesc   { get; set; } = "";
        public string  ActionGuide { get; set; } = "";
        public string? MenuPath    { get; set; }
        public string? Severity    { get; set; }
    }
}
