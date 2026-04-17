using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Bizentro.App.SV.PP.PA999S1_CKO087.Models;

namespace Bizentro.App.SV.PP.PA999S1_CKO087.Services
{
    /// <summary>
    /// PA999 메타데이터 자동 분석 배치 서비스
    ///
    /// ▶ 전체 흐름
    ///   ① USP_PA999_TABLE_META_AUTO_BATCH 실행
    ///      → INFORMATION_SCHEMA에서 신규 테이블 감지
    ///      → PA999_TABLE_META에 [AI분석대기] 상태로 INSERT
    ///
    ///   ② RunAiAnalysisAsync() 실행
    ///      → [AI분석대기] 테이블 목록 조회
    ///      → 각 테이블의 컬럼 구조 + 샘플 데이터 수집
    ///      → Claude API로 TABLE_DESC, KEYWORD_LIST 자동 생성
    ///      → PA999_TABLE_META UPDATE + USE_YN = 'Y' 전환
    ///
    /// ▶ 실행 방식
    ///   - API 엔드포인트: POST /api/PA999/meta/batch
    ///   - 또는 앱 시작 시 백그라운드 서비스로 자동 실행
    ///   - 권장: SQL Agent Job에서 배치 SP 실행 후
    ///           이 서비스를 HTTP 호출로 트리거
    /// </summary>
    public class PA999MetaBatchService
    {
        private readonly PA999DbService              _dbService;
        private readonly IHttpClientFactory          _httpClientFactory;
        private readonly PA999Options                _options;
        private readonly ILogger<PA999MetaBatchService> _logger;

        // 한 번에 처리할 최대 테이블 수 (Claude API Rate Limit 고려)
        private const int BATCH_SIZE         = 10;
        private const int DELAY_MS_PER_TABLE = 1000; // 테이블 간 1초 대기

        public PA999MetaBatchService(
            PA999DbService dbService,
            IHttpClientFactory httpClientFactory,
            IOptions<PA999Options> options,
            ILogger<PA999MetaBatchService> logger)
        {
            _dbService         = dbService;
            _httpClientFactory = httpClientFactory;
            _options           = options.Value;
            _logger            = logger;
        }

        // ══════════════════════════════════════════════════════
        // ▶ 전체 배치 실행 (외부 호출 진입점)
        // ══════════════════════════════════════════════════════

        public async Task<PA999BatchResult> RunFullBatchAsync()
        {
            var result = new PA999BatchResult();

            // ── Step 1. SQL 배치: 신규 테이블 감지 및 등록 ────────────
            _logger.LogInformation("[META-BATCH] Step1: INFORMATION_SCHEMA 스캔 시작");

            var spResult = await _dbService.ExecuteQueryAsync(
                "EXEC USP_PA999_TABLE_META_AUTO_BATCH 'UNIERP60N'");

            if (!spResult.IsSuccess)
            {
                _logger.LogError("[META-BATCH] Step1 실패: {E}", spResult.ErrorMessage);
                result.ErrorMessage = spResult.ErrorMessage ?? string.Empty;
                return result;
            }

            result.ScannedCount = spResult.Rows.Count;
            _logger.LogInformation("[META-BATCH] Step1 완료: {N}개 신규 감지", result.ScannedCount);

            // ── Step 2. AI 분석: TABLE_DESC, KEYWORD_LIST 자동 생성 ───
            _logger.LogInformation("[META-BATCH] Step2: AI 분석 시작");
            var aiResult = await RunAiAnalysisAsync();

            result.AnalyzedCount = aiResult.AnalyzedCount;
            result.FailedCount   = aiResult.FailedCount;
            result.IsSuccess     = true;

            _logger.LogInformation(
                "[META-BATCH] 완료 | 분석: {A}개 | 실패: {F}개",
                result.AnalyzedCount, result.FailedCount);

            return result;
        }

        // ══════════════════════════════════════════════════════
        // ▶ 컬럼 메타 재분석 (외부 호출 진입점)
        //   기존 USE_YN='Y' 테이블의 BIZ_RULE/CODE_VALUES/FK_REF 채우기
        //   tableNames=null 이면 전체 테이블 처리 (우선순위: P_PROD_* → B_ → 기타)
        // ══════════════════════════════════════════════════════

        public async Task<PA999BatchResult> ReanalyzeColumnsAsync(
            List<string>? tableNames = null,
            bool overwriteManual     = false)
        {
            var result = new PA999BatchResult();

            // 분석 대상 테이블 목록 확정
            IEnumerable<string> targets;

            if (tableNames != null && tableNames.Count > 0)
            {
                targets = tableNames;
            }
            else
            {
                // USE_YN='Y' 전체 조회 (우선순위: P_PROD_* → B_ → 기타)
                var listResult = await _dbService.ExecuteQueryAsync(@"
                    SELECT TABLE_NM FROM PA999_TABLE_META WITH(NOLOCK)
                    WHERE USE_YN = 'Y'
                    ORDER BY
                        CASE WHEN TABLE_NM LIKE 'P_PROD%' THEN 0
                             WHEN TABLE_NM LIKE 'B_%'     THEN 1
                             ELSE 2 END,
                        TABLE_NM");

                if (!listResult.IsSuccess || listResult.Rows.Count == 0)
                {
                    _logger.LogInformation("[META-BATCH] 재분석 대상 없음");
                    result.IsSuccess = true;
                    return result;
                }

                targets = listResult.Rows
                    .Select(r => r.TryGetValue("TABLE_NM", out var v) ? v?.ToString() ?? "" : "")
                    .Where(t => !string.IsNullOrEmpty(t));
            }

            _logger.LogInformation("[META-BATCH] 컬럼 재분석 시작");

            foreach (var tableNm in targets.Take(BATCH_SIZE))
            {
                try
                {
                    var columnInfo    = await GetColumnInfoFromSchemaAsync(tableNm);
                    var sampleData    = await GetTableSampleAsync(tableNm);
                    var distinctValues = await GetColumnDistinctValuesAsync(tableNm);

                    var meta = await AnalyzeTableWithClaudeAsync(
                        tableNm, columnInfo, sampleData, distinctValues);

                    if (meta.Columns.Count > 0)
                        await UpsertColumnMetaAsync(tableNm, meta.Columns, overwriteManual);

                    result.AnalyzedCount++;
                    _logger.LogInformation(
                        "[META-BATCH] 컬럼 재분석 완료: {T} | {C}개 컬럼",
                        tableNm, meta.Columns.Count);

                    await Task.Delay(DELAY_MS_PER_TABLE);
                }
                catch (Exception ex)
                {
                    result.FailedCount++;
                    _logger.LogError(ex, "[META-BATCH] 컬럼 재분석 실패: {T}", tableNm);
                }
            }

            result.IsSuccess = true;
            return result;
        }

        // ══════════════════════════════════════════════════════
        // ■ Step 2. AI 분석 배치
        //   [AI분석대기] 테이블을 Claude로 분석하여 메타 자동 생성
        // ══════════════════════════════════════════════════════

        public async Task<PA999BatchResult> RunAiAnalysisAsync()
        {
            var result = new PA999BatchResult();

            // 분석 대기 중인 테이블 목록 조회
            // [정규화 변경] COLUMN_DESC 컬럼 DROP → SELECT 목록에서 제거
            var pendingResult = await _dbService.ExecuteQueryAsync(@"
                SELECT TOP (@batchSize) SEQ, TABLE_NM
                FROM PA999_TABLE_META WITH(NOLOCK)
                WHERE TABLE_DESC LIKE N'[AI분석대기]%'
                  AND USE_YN = 'N'
                ORDER BY INSRT_DT ASC"
                .Replace("@batchSize", BATCH_SIZE.ToString()));

            if (!pendingResult.IsSuccess || pendingResult.Rows.Count == 0)
            {
                _logger.LogInformation("[META-BATCH] AI 분석 대기 테이블 없음");
                result.IsSuccess = true;
                return result;
            }

            _logger.LogInformation("[META-BATCH] AI 분석 대기: {N}개", pendingResult.Rows.Count);

            foreach (var row in pendingResult.Rows)
            {
                var tableNm = row.TryGetValue("TABLE_NM", out var n) ? n?.ToString() ?? "" : "";
                var seq     = row.TryGetValue("SEQ",      out var s) ? s?.ToString() ?? "" : "";

                if (string.IsNullOrEmpty(tableNm)) continue;

                try
                {
                    // [정규화 변경] COLUMN_DESC 대신 INFORMATION_SCHEMA에서 컬럼 구조 직접 조회
                    var columnInfo = await GetColumnInfoFromSchemaAsync(tableNm);

                    // 샘플 데이터 수집 (최대 3행)
                    var sampleData = await GetTableSampleAsync(tableNm);

                    // 코드성 컬럼 실제 DISTINCT 값 수집 (_YN/_FLG/_GUBUN/_TYPE 등)
                    var distinctValues = await GetColumnDistinctValuesAsync(tableNm);

                    // Claude API로 테이블 분석
                    var meta = await AnalyzeTableWithClaudeAsync(
                        tableNm, columnInfo, sampleData, distinctValues);

                    // PA999_TABLE_META 업데이트 (COLUMN_DESC 컬럼 제거됨 → 포함 금지)
                    var updateSql = $@"
                        UPDATE PA999_TABLE_META SET
                            TABLE_DESC   = N'{EscapeSql(meta.TableDesc)}',
                            KEYWORD_LIST = N'{EscapeSql(meta.KeywordList)}',
                            USE_YN       = 'Y',
                            UPDT_DT      = GETDATE()
                        WHERE SEQ = {seq}";

                    var updateResult = await _dbService.ExecuteNonQueryAsync(updateSql);

                    if (updateResult.IsSuccess)
                    {
                        // [정규화 변경] PA999_COLUMN_META에 컬럼 메타 행 단위 INSERT
                        //   기존 AI 생성 행 삭제 후 재삽입 (SRC_TYPE='AI')
                        if (meta.Columns.Count > 0)
                            await UpsertColumnMetaAsync(tableNm, meta.Columns);

                        result.AnalyzedCount++;
                        _logger.LogInformation(
                            "[META-BATCH] 분석 완료: {T} | 키워드: {K} | 컬럼수: {C}",
                            tableNm, meta.KeywordList, meta.Columns.Count);
                    }

                    // Rate Limit 방지 대기
                    await Task.Delay(DELAY_MS_PER_TABLE);
                }
                catch (Exception ex)
                {
                    result.FailedCount++;
                    _logger.LogError(ex, "[META-BATCH] 분석 실패: {T}", tableNm);
                }
            }

            result.IsSuccess = true;
            return result;
        }

        // ══════════════════════════════════════════════════════
        // ■ INFORMATION_SCHEMA 컬럼 구조 조회
        //   [정규화 변경] COLUMN_DESC 대체 - Claude에게 전달할 컬럼 목록 생성
        //   형식 예) "PLANT_CD nvarchar(10) NOT NULL, PROD_DT varchar(8) NULL, ..."
        // ══════════════════════════════════════════════════════

        private async Task<string> GetColumnInfoFromSchemaAsync(string tableName)
        {
            var sql = $@"
                SELECT COLUMN_NAME, DATA_TYPE,
                       ISNULL(CAST(CHARACTER_MAXIMUM_LENGTH AS NVARCHAR), '') AS MAX_LEN,
                       IS_NULLABLE
                FROM INFORMATION_SCHEMA.COLUMNS WITH(NOLOCK)
                WHERE TABLE_NAME = N'{EscapeSql(tableName)}'
                ORDER BY ORDINAL_POSITION";

            var result = await _dbService.ExecuteQueryAsync(sql);

            if (!result.IsSuccess || result.Rows.Count == 0)
                return string.Empty;

            var parts = result.Rows.Select(r =>
            {
                var col  = r.TryGetValue("COLUMN_NAME", out var c) ? c?.ToString() : "";
                var dt   = r.TryGetValue("DATA_TYPE",   out var d) ? d?.ToString() : "";
                var len  = r.TryGetValue("MAX_LEN",     out var l) ? l?.ToString() : "";
                var null_= r.TryGetValue("IS_NULLABLE", out var n) ? n?.ToString() : "";
                var typeStr = string.IsNullOrEmpty(len) || len == "-1"
                    ? dt : $"{dt}({len})";
                return $"{col} {typeStr} {(null_ == "YES" ? "NULL" : "NOT NULL")}";
            });

            return string.Join(", ", parts);
        }

        // ══════════════════════════════════════════════════════
        // ■ PA999_COLUMN_META Upsert
        //   [정규화 변경] AI 분석 결과를 행 단위로 INSERT
        //   기존 SRC_TYPE='AI' 행 삭제 후 재삽입 (수동 입력 SRC_TYPE='MANUAL' 보존)
        // ══════════════════════════════════════════════════════

        private async Task UpsertColumnMetaAsync(
            string tableName, List<PA999ColumnMetaRow> columns,
            bool overwriteManual = false)
        {
            if (columns.Count == 0) return;

            var tnEsc = EscapeSql(tableName);

            // ── 컬럼별 UPSERT ──────────────────────────────────────
            // PA999_COLUMN_META에 (TABLE_NM, COLUMN_NM) 유니크 제약이 있으므로
            // DELETE+INSERT 대신 IF EXISTS UPDATE / ELSE INSERT 방식 사용
            //
            // overwriteManual=false (기본):
            //   - 기존 행(SRC_TYPE='M' or 'A'): BIZ_RULE/CODE_VALUES/FK_REF만 UPDATE
            //   - 신규 컬럼: SRC_TYPE='A'로 INSERT
            //
            // overwriteManual=true:
            //   - 기존 행: KO_NM 포함 전체 UPDATE
            //   - 신규 컬럼: SRC_TYPE='A'로 INSERT

            foreach (var col in columns)
            {
                var colEsc  = EscapeSql(col.ColumnNm);
                var koEsc   = EscapeSql(col.KoNm);
                var bizEsc  = EscapeSql(col.BizRule);
                var codeEsc = EscapeSql(col.CodeValues);
                var fkEsc   = EscapeSql(col.FkRef);

                string sql;
                if (overwriteManual)
                {
                    sql = $@"
                        IF EXISTS (SELECT 1 FROM PA999_COLUMN_META WITH(NOLOCK)
                                   WHERE TABLE_NM=N'{tnEsc}' AND COLUMN_NM=N'{colEsc}')
                            UPDATE PA999_COLUMN_META
                               SET KO_NM=N'{koEsc}', BIZ_RULE=N'{bizEsc}',
                                   CODE_VALUES=N'{codeEsc}', FK_REF=N'{fkEsc}', UPDT_DT=GETDATE()
                             WHERE TABLE_NM=N'{tnEsc}' AND COLUMN_NM=N'{colEsc}'
                        ELSE
                            INSERT INTO PA999_COLUMN_META
                                (TABLE_NM, COLUMN_NM, KO_NM, BIZ_RULE, CODE_VALUES, FK_REF, SRC_TYPE)
                            VALUES (N'{tnEsc}',N'{colEsc}',N'{koEsc}',N'{bizEsc}',N'{codeEsc}',N'{fkEsc}','A')";
                }
                else
                {
                    sql = $@"
                        IF EXISTS (SELECT 1 FROM PA999_COLUMN_META WITH(NOLOCK)
                                   WHERE TABLE_NM=N'{tnEsc}' AND COLUMN_NM=N'{colEsc}')
                            UPDATE PA999_COLUMN_META
                               SET BIZ_RULE=N'{bizEsc}', CODE_VALUES=N'{codeEsc}',
                                   FK_REF=N'{fkEsc}', UPDT_DT=GETDATE()
                             WHERE TABLE_NM=N'{tnEsc}' AND COLUMN_NM=N'{colEsc}'
                        ELSE
                            INSERT INTO PA999_COLUMN_META
                                (TABLE_NM, COLUMN_NM, KO_NM, BIZ_RULE, CODE_VALUES, FK_REF, SRC_TYPE)
                            VALUES (N'{tnEsc}',N'{colEsc}',N'{koEsc}',N'{bizEsc}',N'{codeEsc}',N'{fkEsc}','A')";
                }

                var r = await _dbService.ExecuteNonQueryAsync(sql);
                if (!r.IsSuccess)
                    _logger.LogWarning("[META-BATCH] COLUMN_META UPSERT 실패: {T}.{C} | {E}",
                        tableName, col.ColumnNm, r.ErrorMessage);
            }
        }

        // ══════════════════════════════════════════════════════
        // ■ 테이블 샘플 데이터 수집
        // ══════════════════════════════════════════════════════

        private async Task<string> GetTableSampleAsync(string tableName)
        {
            var result = await _dbService.ExecuteQueryAsync(
                $"EXEC USP_PA999_GET_TABLE_SAMPLE '{EscapeSql(tableName)}'");

            if (!result.IsSuccess || result.Rows.Count == 0)
                return string.Empty;

            var sb = new StringBuilder();
            sb.AppendLine("샘플 데이터 (상위 3행):");
            sb.AppendLine(string.Join(" | ", result.Columns));
            foreach (var row in result.Rows.Take(3))
            {
                sb.AppendLine(string.Join(" | ", result.Columns.Select(c =>
                    row.TryGetValue(c, out var v) ? (v?.ToString() ?? "NULL") : "NULL")));
            }
            return sb.ToString();
        }

        // ══════════════════════════════════════════════════════
        // ■ 코드성 컬럼 DISTINCT 값 자동 수집
        //   _YN/_FLG/_GUBUN/_TYPE/_STATUS/_DIV/_GB 패턴 컬럼에서
        //   실제 DB 값을 조회하여 CODE_VALUES 추론에 활용
        // ══════════════════════════════════════════════════════

        private async Task<string> GetColumnDistinctValuesAsync(string tableName)
        {
            var tnEsc = EscapeSql(tableName);
            var colsSql = $@"
                SELECT COLUMN_NAME
                FROM INFORMATION_SCHEMA.COLUMNS WITH(NOLOCK)
                WHERE TABLE_NAME = N'{tnEsc}'
                  AND (   COLUMN_NAME LIKE '%_YN'
                       OR COLUMN_NAME LIKE '%_FLG'
                       OR COLUMN_NAME LIKE '%_GUBUN%'
                       OR COLUMN_NAME LIKE '%_TYPE%'
                       OR COLUMN_NAME LIKE '%_STATUS%'
                       OR COLUMN_NAME LIKE '%_DIV%'
                       OR COLUMN_NAME LIKE '%_GB%'
                       OR COLUMN_NAME LIKE '%_DIV_CD'
                       OR COLUMN_NAME LIKE '%_KIND%')
                  AND DATA_TYPE IN ('char','nchar','varchar','nvarchar')
                ORDER BY ORDINAL_POSITION";

            var colsResult = await _dbService.ExecuteQueryAsync(colsSql);
            if (!colsResult.IsSuccess || colsResult.Rows.Count == 0)
                return string.Empty;

            var sb = new StringBuilder();
            foreach (var row in colsResult.Rows.Take(10))
            {
                var colNm = row.TryGetValue("COLUMN_NAME", out var c) ? c?.ToString() : null;
                if (string.IsNullOrEmpty(colNm)) continue;

                // 컬럼명은 INFORMATION_SCHEMA에서 왔으므로 브래킷으로 감싸서 안전하게 사용
                var dvSql = $@"
                    SELECT TOP 20 CAST([{colNm}] AS NVARCHAR(50)) AS VAL, COUNT(*) AS CNT
                    FROM [{tnEsc}] WITH(NOLOCK)
                    WHERE [{colNm}] IS NOT NULL AND CAST([{colNm}] AS NVARCHAR(50)) <> ''
                    GROUP BY CAST([{colNm}] AS NVARCHAR(50))
                    ORDER BY CNT DESC";

                var dvResult = await _dbService.ExecuteQueryAsync(dvSql);
                if (!dvResult.IsSuccess || dvResult.Rows.Count == 0) continue;

                var vals = dvResult.Rows
                    .Select(r => r.TryGetValue("VAL", out var v) ? $"'{v}'" : null)
                    .Where(v => v != null)
                    .ToList();

                if (vals.Count > 0)
                    sb.AppendLine($"{colNm}: {string.Join(",", vals)}");
            }

            return sb.ToString().Trim();
        }

        // ══════════════════════════════════════════════════════
        // ■ Claude API로 테이블 구조 분석
        //   → TABLE_DESC, KEYWORD_LIST 자동 생성 + PA999_COLUMN_META 행 단위 INSERT
        //   → BIZ_RULE, CODE_VALUES, FK_REF 포함
        // ══════════════════════════════════════════════════════

        private async Task<PA999TableMeta> AnalyzeTableWithClaudeAsync(
            string tableName,
            string columnInfo,
            string sampleData,
            string distinctValues = "")
        {
            var distinctSection = string.IsNullOrEmpty(distinctValues) ? "" :
                $"\n[코드성 컬럼 실제값 (CODE_VALUES 추론에 활용)]\n{distinctValues}";

            var prompt = $@"당신은 UNIERP ERP 시스템 데이터베이스 분석 전문가입니다.
아래 테이블 정보를 분석해서 JSON 형식으로 반환하세요.

[테이블명]
{tableName}

[컬럼 구조]
{columnInfo}
{(string.IsNullOrEmpty(sampleData) ? "" : $"\n[샘플 데이터]\n{sampleData}")}
{distinctSection}

▶ 분석 지침:
- biz_rule: 집계 방법(SUM/COUNT), 단위(EA/TON/KG), 주의사항, 다른 컬럼과의 관계 등 SQL 생성에 필요한 업무 규칙. 없으면 빈 문자열.
- code_values: 위 [코드성 컬럼 실제값] 정보를 바탕으로 각 코드값의 의미 기술. 예: '100'=반복제조,'200'=지역공장,'300'=레미콘,'400'=레미탈. 없으면 빈 문자열.
- fk_ref: 컬럼명 패턴(_CD, _NM)과 ERP 관례를 바탕으로 참조 테이블.컬럼 추론. 예: B_PLANT.PLANT_CD, B_ITEM.ITEM_CD. 확실하지 않으면 빈 문자열.

다음 JSON 형식으로만 응답하세요 (설명 없이):
{{
  ""table_desc"": ""테이블의 업무 목적을 한국어 1~2문장으로 설명"",
  ""keyword_list"": ""이 테이블을 찾을 때 사용할 한국어 키워드를 콤마로 구분 (10개 이내)"",
  ""columns"": [
    {{
      ""column_nm"":   ""컬럼명"",
      ""ko_nm"":       ""한국어 컬럼명 (2~6자)"",
      ""biz_rule"":    ""업무 규칙 (없으면 빈 문자열)"",
      ""code_values"": ""코드값 설명 (없으면 빈 문자열)"",
      ""fk_ref"":      ""참조 테이블.컬럼 (없으면 빈 문자열)""
    }}
  ]
}}

예시:
{{
  ""table_desc"": ""일별 생산 실적 헤더 - 공장별 레미콘 생산량을 일 단위로 집계한 테이블"",
  ""keyword_list"": ""생산량,생산실적,일생산,공장생산,생산수량"",
  ""columns"": [
    {{ ""column_nm"": ""PLANT_CD"",      ""ko_nm"": ""공장코드"",   ""biz_rule"": ""WHERE 절 공장 필터링에 사용"",              ""code_values"": """",                                    ""fk_ref"": ""B_PLANT.PLANT_CD"" }},
    {{ ""column_nm"": ""PROD_DT"",       ""ko_nm"": ""생산일자"",   ""biz_rule"": ""nvarchar(8) YYYYMMDD 형식. 날짜 범위 조회 시 BETWEEN 사용"", ""code_values"": """",              ""fk_ref"": """" }},
    {{ ""column_nm"": ""PROD_QTY"",      ""ko_nm"": ""생산량"",     ""biz_rule"": ""단위:TON. 집계 시 SUM(PROD_QTY) 사용. OUT_QTY(원자재투입량)와 혼동 금지"", ""code_values"": """", ""fk_ref"": """" }},
    {{ ""column_nm"": ""PLANT_GUBUN_CD"",""ko_nm"": ""공장유형"",   ""biz_rule"": """",                                          ""code_values"": ""'100'=반복제조,'200'=지역공장,'300'=레미콘,'400'=레미탈"", ""fk_ref"": """" }}
  ]
}}";

            var client = _httpClientFactory.CreateClient("AnthropicClient");

            var requestBody = new
            {
                model      = _options.Model,
                max_tokens = 4000,
                messages   = new[]
                {
                    new { role = "user", content = prompt }
                }
            };

            var content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json");

            var response = await client.PostAsync(
                "https://api.anthropic.com/v1/messages", content);

            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync();
                throw new Exception($"Claude API 오류: {response.StatusCode} - {err}");
            }

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var text = "{}";
            if (doc.RootElement.TryGetProperty("content", out var contentArr)
                && contentArr.GetArrayLength() > 0
                && contentArr[0].TryGetProperty("text", out var textProp))
            {
                text = textProp.GetString() ?? "{}";
            }

            // JSON 파싱
            return ParseMetaJson(text, tableName);
        }

        private PA999TableMeta ParseMetaJson(string jsonText, string tableName)
        {
            try
            {
                // 마크다운 코드블록 제거: ```json ... ``` 또는 ``` ... ```
                jsonText = System.Text.RegularExpressions.Regex.Replace(
                    jsonText, @"```(?:json)?\s*", "").Trim();

                // JSON 블록 추출
                var start = jsonText.IndexOf('{');
                var end   = jsonText.LastIndexOf('}') + 1;
                if (start < 0 || end <= start)
                    throw new Exception("JSON 블록 없음");

                var json = jsonText.Substring(start, end - start);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var columns = new List<PA999ColumnMetaRow>();

                // ── 신규 형식: "columns" 배열 (BIZ_RULE/CODE_VALUES/FK_REF 포함) ──
                if (root.TryGetProperty("columns", out var colsArr) &&
                    colsArr.ValueKind == JsonValueKind.Array)
                {
                    foreach (var col in colsArr.EnumerateArray())
                    {
                        var colNm = col.TryGetProperty("column_nm",   out var p1) ? p1.GetString() ?? "" : "";
                        if (string.IsNullOrEmpty(colNm)) continue;

                        columns.Add(new PA999ColumnMetaRow
                        {
                            ColumnNm   = colNm,
                            KoNm       = col.TryGetProperty("ko_nm",       out var p2) ? p2.GetString() ?? "" : "",
                            BizRule    = col.TryGetProperty("biz_rule",    out var p3) ? p3.GetString() ?? "" : "",
                            CodeValues = col.TryGetProperty("code_values", out var p4) ? p4.GetString() ?? "" : "",
                            FkRef      = col.TryGetProperty("fk_ref",      out var p5) ? p5.GetString() ?? "" : "",
                        });
                    }
                }
                // ── 구버전 폴백: "column_desc" 단일 문자열 (PLANT_CD=공장코드, ...) ──
                else if (root.TryGetProperty("column_desc", out var colDescProp))
                {
                    var colDescRaw = colDescProp.GetString() ?? "";
                    foreach (var part in colDescRaw.Split(',',
                        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        var eqIdx = part.IndexOf('=');
                        if (eqIdx > 0)
                        {
                            columns.Add(new PA999ColumnMetaRow
                            {
                                ColumnNm = part.Substring(0, eqIdx).Trim(),
                                KoNm     = part.Substring(eqIdx + 1).Trim()
                            });
                        }
                    }
                }

                return new PA999TableMeta
                {
                    TableDesc   = root.TryGetProperty("table_desc",   out var d) ? d.GetString() ?? "" : "",
                    KeywordList = root.TryGetProperty("keyword_list", out var k) ? k.GetString() ?? "" : "",
                    Columns     = columns
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[META-BATCH] JSON 파싱 실패: {T}", tableName);
                return new PA999TableMeta
                {
                    TableDesc   = $"[자동분석] {tableName}",
                    KeywordList = tableName,
                    Columns     = new List<PA999ColumnMetaRow>()
                };
            }
        }

        // ══════════════════════════════════════════════════════
        // ■ 개발 디버그용: Claude 원본 응답 + 파싱 결과 반환
        private static string EscapeSql(string input) =>
            (input ?? "").Replace("'", "''").Replace("[AI분석대기]", "");
    }

    // ── 모델 클래스 ───────────────────────────────────────────

    public class PA999BatchResult
    {
        public bool   IsSuccess    { get; set; }
        public int    ScannedCount { get; set; }
        public int    AnalyzedCount{ get; set; }
        public int    FailedCount  { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }

    public class PA999TableMeta
    {
        public string TableDesc   { get; set; } = string.Empty;
        public string KeywordList { get; set; } = string.Empty;

        // [정규화 변경] COLUMN_DESC(단일 문자열) 제거
        //              → PA999_COLUMN_META 행 단위 목록으로 교체
        public List<PA999ColumnMetaRow> Columns { get; set; } = new();
    }

    /// <summary>
    /// PA999_COLUMN_META 1행 (배치 AI 분석 후 INSERT 대상)
    /// SRC_TYPE = 'A' (char(1), AI생성) 로 INSERT됨
    /// 수동 입력 행은 SRC_TYPE = 'M' 으로 보존
    /// </summary>
    public class PA999ColumnMetaRow
    {
        /// <summary>물리 컬럼명 (COLUMN_NM)</summary>
        public string ColumnNm   { get; set; } = string.Empty;

        /// <summary>한국어 설명 (KO_NM)</summary>
        public string KoNm       { get; set; } = string.Empty;

        /// <summary>업무 규칙 (BIZ_RULE) — 집계방법, 단위, 주의사항 등</summary>
        public string BizRule    { get; set; } = string.Empty;

        /// <summary>코드값 설명 (CODE_VALUES) — 예: 'Y'=사용,'N'=미사용</summary>
        public string CodeValues { get; set; } = string.Empty;

        /// <summary>FK 참조 (FK_REF) — 예: B_PLANT.PLANT_CD</summary>
        public string FkRef      { get; set; } = string.Empty;
    }
}
