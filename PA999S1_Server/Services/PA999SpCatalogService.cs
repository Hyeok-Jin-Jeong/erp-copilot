using System.Data;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Bizentro.App.SV.PP.PA999S1_CKO087.Models;

namespace Bizentro.App.SV.PP.PA999S1_CKO087.Services
{
    // ══════════════════════════════════════════════════════════
    // PA999 SP 카탈로그 실행 서비스 (Mode A)
    //
    // 역할:
    //   1. PA999_SP_PARAMS에서 파라미터 정의 조회
    //   2. AI가 추출한 엔티티로 파라미터 값 매핑
    //   3. 파라미터화된 SP 실행 (SQL Injection 원천 차단)
    //   4. PA999_SP_SOP에서 SOP 가이드 조회 → 결과와 결합
    //
    // 보안:
    //   - PA999_SP_CATALOG에 등록된 SP만 실행 가능 (화이트리스트)
    //   - SqlParameter 사용 → SQL Injection 불가
    //   - Read-only 계정 사용 권장
    // ══════════════════════════════════════════════════════════

    public class PA999SpCatalogService
    {
        private readonly PA999DbService _db;
        private readonly PA999Options   _options;
        private readonly ILogger<PA999SpCatalogService> _logger;

        private const int SpTimeoutSeconds = 60;   // ★ 30→60: CLOSECHECK 등 무거운 SP 타임아웃 방지
        private const int MaxRows = 100;

        public PA999SpCatalogService(
            PA999DbService db,
            IOptions<PA999Options> options,
            ILogger<PA999SpCatalogService> logger)
        {
            _db      = db;
            _options = options.Value;
            _logger  = logger;
        }

        // ══════════════════════════════════════════════════════
        // ■ SP 파라미터 정의 조회
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// SP_ID에 해당하는 파라미터 정의 목록 반환
        /// AI 프롬프트에 주입하여 파라미터 매핑 가이드로 활용
        /// </summary>
        public async Task<List<PA999SpParamDef>> GetParamDefsAsync(int spId)
        {
            var defs = new List<PA999SpParamDef>();
            try
            {
                var sql = $@"
                    SELECT PARAM_SEQ, PARAM_NAME, PARAM_DESC, DATA_TYPE, MAX_LENGTH,
                           IS_REQUIRED, IS_OUTPUT, DEFAULT_VAL, MAPPING_HINT
                    FROM PA999_SP_PARAMS
                    WHERE SP_ID = {spId} AND USE_YN = 'Y'
                    ORDER BY PARAM_SEQ";

                var result = await _db.ExecuteQueryAsync(sql);
                if (!result.IsSuccess) return defs;

                foreach (var row in result.Rows)
                {
                    defs.Add(new PA999SpParamDef
                    {
                        ParamSeq    = Convert.ToInt32(row["PARAM_SEQ"]),
                        ParamName   = row["PARAM_NAME"]?.ToString() ?? "",
                        ParamDesc   = row["PARAM_DESC"]?.ToString() ?? "",
                        DataType    = row["DATA_TYPE"]?.ToString() ?? "NVARCHAR",
                        MaxLength   = row["MAX_LENGTH"] is not null and not DBNull
                            ? Convert.ToInt32(row["MAX_LENGTH"]) : 50,
                        IsRequired  = row["IS_REQUIRED"]?.ToString() == "Y",
                        IsOutput    = row["IS_OUTPUT"]?.ToString() == "Y",
                        DefaultVal  = row["DEFAULT_VAL"]?.ToString(),
                        MappingHint = row["MAPPING_HINT"]?.ToString()
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[SpCatalog] SP_ID={I} 파라미터 조회 오류", spId);
            }
            return defs;
        }

        // ══════════════════════════════════════════════════════
        // ■ SP 파라미터화 실행 (핵심)
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// SP를 파라미터화하여 실행 (SQL Injection 원천 차단)
        /// </summary>
        /// <param name="spName">실행할 SP명 (PA999_SP_CATALOG에 등록된 것만 허용)</param>
        /// <param name="paramValues">파라미터명 → 값 매핑 (AI가 추출한 값)</param>
        /// <param name="paramDefs">파라미터 정의 (타입/길이 정보)</param>
        public async Task<PA999SpExecutionResult> ExecuteSpAsync(
            string spName,
            Dictionary<string, string?> paramValues,
            List<PA999SpParamDef> paramDefs)
        {
            var execResult = new PA999SpExecutionResult { SpName = spName };

            try
            {
                // ── 보안: SP명 화이트리스트 검증 ──
                if (!await IsRegisteredSpAsync(spName))
                {
                    execResult.IsSuccess = false;
                    execResult.ErrorMessage = $"등록되지 않은 SP입니다: {spName}";
                    _logger.LogWarning("[SpCatalog] 미등록 SP 실행 차단: {SP}", spName);
                    return execResult;
                }

                using var conn = new SqlConnection(_options.ConnectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(spName, conn)
                {
                    CommandType = CommandType.StoredProcedure,
                    CommandTimeout = SpTimeoutSeconds
                };

                // ── 파라미터 바인딩 ──
                var outputParams = new Dictionary<string, SqlParameter>();

                foreach (var def in paramDefs)
                {
                    var sqlParam = new SqlParameter
                    {
                        ParameterName = def.ParamName,
                        SqlDbType = MapSqlDbType(def.DataType),
                        Size = def.MaxLength > 0 ? def.MaxLength : 200
                    };

                    if (def.IsOutput)
                    {
                        sqlParam.Direction = ParameterDirection.Output;
                        outputParams[def.ParamName] = sqlParam;
                    }
                    else
                    {
                        sqlParam.Direction = ParameterDirection.Input;
                        // 값이 있으면 사용, 없으면 DBNull
                        if (paramValues.TryGetValue(def.ParamName, out var val) &&
                            !string.IsNullOrEmpty(val))
                        {
                            sqlParam.Value = val;
                        }
                        else
                        {
                            sqlParam.Value = DBNull.Value;
                        }
                    }

                    cmd.Parameters.Add(sqlParam);
                }

                _logger.LogInformation("[SpCatalog] SP 실행: {SP} | Params: {P}",
                    spName, string.Join(", ", paramValues.Select(kv => $"{kv.Key}={kv.Value}")));

                // ── 실행 및 결과 수집 (다중 MULTISET 지원) ──
                //   생산일보 SP는 19~20개 결과셋(MULTISET)을 반환
                //   첫 번째는 타이틀/헤더, 이후가 실제 데이터
                //   모든 결과셋을 TABLE_NO 구분하여 수집
                using var reader = await cmd.ExecuteReaderAsync();

                int tableNo = 0;
                int totalRows = 0;
                bool isFirstResultSet = true;

                do
                {
                    // 각 결과셋의 컬럼명 수집 (첫 번째 결과셋만 Columns에 저장)
                    var currentColumns = new List<string>();
                    for (int i = 0; i < reader.FieldCount; i++)
                        currentColumns.Add(reader.GetName(i));

                    if (isFirstResultSet)
                    {
                        execResult.Columns.AddRange(currentColumns);
                        isFirstResultSet = false;
                    }

                    while (await reader.ReadAsync() && totalRows < MaxRows)
                    {
                        var row = new Dictionary<string, object?>();
                        // TABLE_NO 태그 추가 (UI에서 MULTISET 구분용)
                        row["TABLE_NO"] = tableNo.ToString("000");
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            object? value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                            if (value is DateTime dt)
                                value = dt.ToString("yyyy-MM-dd HH:mm:ss");
                            row[currentColumns[i]] = value;
                        }
                        execResult.Rows.Add(row);
                        totalRows++;
                    }

                    tableNo++;
                } while (await reader.NextResultAsync() && totalRows < MaxRows);

                // Reader 닫은 후 OUTPUT 파라미터 수집
                await reader.CloseAsync();
                foreach (var kv in outputParams)
                {
                    execResult.OutputValues[kv.Key] =
                        kv.Value.Value == DBNull.Value ? null : kv.Value.Value?.ToString();
                }

                execResult.IsSuccess = true;
                _logger.LogInformation("[SpCatalog] SP 실행 완료 | {SP} | 결과 {N}행 | OUTPUT: {O}",
                    spName, execResult.Rows.Count,
                    string.Join(", ", execResult.OutputValues.Select(kv => $"{kv.Key}={kv.Value}")));
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "[SpCatalog] SP 실행 SQL 오류 | {SP}", spName);
                execResult.IsSuccess = false;
                execResult.ErrorMessage = $"SP 실행 오류: {ex.Message}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SpCatalog] SP 실행 오류 | {SP}", spName);
                execResult.IsSuccess = false;
                execResult.ErrorMessage = "SP 실행 중 오류가 발생했습니다.";
            }

            return execResult;
        }

        // ══════════════════════════════════════════════════════
        // ■ 진단 테이블 직접 조회 (ERROR_MAP의 DIAG_TABLES)
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 오류 진단 시 관련 테이블을 직접 조회 (예: 자동마감 상태/상세)
        /// ERROR_MAP.DIAG_TABLES에 지정된 테이블에서 조건 조회
        /// </summary>
        public async Task<PA999DiagResult> ExecuteDiagQueryAsync(
            PA999ErrorMapEntry errorMap,
            Dictionary<string, string?> paramValues)
        {
            var diagResult = new PA999DiagResult();

            try
            {
                // 파라미터 값에서 공통 조건 추출
                paramValues.TryGetValue("@P_PLANT_CD", out var plantCd);
                paramValues.TryGetValue("@P_PROD_DT", out var prodDt);
                paramValues.TryGetValue("@P_WC_CD", out var wcCd);

                if (string.IsNullOrEmpty(plantCd) || string.IsNullOrEmpty(prodDt))
                {
                    diagResult.Summary = "진단에 필요한 공장코드/일자 정보가 부족합니다.";
                    return diagResult;
                }

                // ── 자동마감 헤더 조회 (STATUS_CD, TX_STATUS) ──
                var hdrSql = $@"
                    SELECT PLANT_CD, CLOSE_DT, PROD_DT, WC_GROUP_CD, WC_CD, ITEM_CD,
                           RTRIM(STATUS_CD) AS STATUS_CD, TX_STATUS
                    FROM P_PROD_AUTO_CLOSE_CKO087 WITH (NOLOCK)
                    WHERE PLANT_CD = N'{EscapeSql(plantCd)}'
                      AND PROD_DT = N'{EscapeSql(prodDt)}'
                      {(string.IsNullOrEmpty(wcCd) ? "" : $"AND WC_CD = N'{EscapeSql(wcCd)}'")}
                      AND RTRIM(STATUS_CD) = '1'
                    ORDER BY WC_CD";

                var hdrResult = await _db.ExecuteQueryAsync(hdrSql);
                if (hdrResult.IsSuccess)
                    diagResult.AutoCloseHeaders = hdrResult.Rows;

                // ── 자동마감 디테일 조회 (부족 자재 특정) ──
                var dtlSql = $@"
                    SELECT d.WC_CD, d.CHILD_ITEM_CD, b.ITEM_NM,
                           d.TRNS_TYPE_NM, d.SUBUL_QTY, d.CUR_JAEGO_QTY,
                           d.SL_CD, d.AUTO_MAGAM_GUBUN
                    FROM P_PROD_AUTO_CLOSE_DTL_CKO087 d WITH (NOLOCK)
                    LEFT JOIN B_ITEM b ON d.CHILD_ITEM_CD = b.ITEM_CD
                    WHERE d.PLANT_CD = N'{EscapeSql(plantCd)}'
                      AND d.PROD_DT = N'{EscapeSql(prodDt)}'
                      {(string.IsNullOrEmpty(wcCd) ? "" : $"AND d.WC_CD = N'{EscapeSql(wcCd)}'")}
                      AND d.AUTO_MAGAM_GUBUN = 'N'
                    ORDER BY d.WC_CD, d.SORT_NO";

                var dtlResult = await _db.ExecuteQueryAsync(dtlSql);
                if (dtlResult.IsSuccess)
                    diagResult.AutoCloseDetails = dtlResult.Rows;

                // ── 부족 자재의 전체 저장위치별 재고 조회 ──
                if (dtlResult.IsSuccess && dtlResult.Rows.Count > 0)
                {
                    var shortageItems = dtlResult.Rows
                        .Where(r => r.ContainsKey("TRNS_TYPE_NM") &&
                                    r["TRNS_TYPE_NM"]?.ToString()?.Contains("출고") == true)
                        .Select(r => r["CHILD_ITEM_CD"]?.ToString())
                        .Where(x => !string.IsNullOrEmpty(x))
                        .Distinct()
                        .ToList();

                    if (shortageItems.Count > 0)
                    {
                        var itemIn = string.Join(",",
                            shortageItems.Select(i => $"N'{EscapeSql(i!)}'"));

                        var stkSql = $@"
                            SELECT s.ITEM_CD, b.ITEM_NM, s.SL_CD, sl.SL_NM,
                                   s.GOOD_ON_HAND_QTY
                            FROM I_ONHAND_STOCK s WITH (NOLOCK)
                            LEFT JOIN B_ITEM b ON s.ITEM_CD = b.ITEM_CD
                            LEFT JOIN B_STORAGE_LOCATION sl ON s.SL_CD = sl.SL_CD AND s.PLANT_CD = sl.PLANT_CD
                            WHERE s.PLANT_CD = N'{EscapeSql(plantCd)}'
                              AND s.ITEM_CD IN ({itemIn})
                              AND s.GOOD_ON_HAND_QTY <> 0
                            ORDER BY s.ITEM_CD, s.SL_CD";

                        var stkResult = await _db.ExecuteQueryAsync(stkSql);
                        if (stkResult.IsSuccess)
                            diagResult.StockStatus = stkResult.Rows;
                    }
                }

                // ── SOP 가이드 매칭 ──
                if (!string.IsNullOrEmpty(errorMap.ErrorType))
                {
                    var sopSql = $@"
                        SELECT CAUSE_DESC, ACTION_GUIDE, MENU_PATH, SEVERITY
                        FROM PA999_SP_SOP
                        WHERE ERROR_TYPE = N'{EscapeSql(errorMap.ErrorType)}'
                          AND USE_YN = 'Y'
                        ORDER BY SOP_ID ASC";

                    var sopResult = await _db.ExecuteQueryAsync(sopSql);
                    if (sopResult.IsSuccess && sopResult.Rows.Count > 0)
                    {
                        var sop = sopResult.Rows[0];
                        diagResult.SopCauseDesc   = sop["CAUSE_DESC"]?.ToString();
                        diagResult.SopActionGuide = sop["ACTION_GUIDE"]?.ToString();
                        diagResult.SopMenuPath    = sop["MENU_PATH"]?.ToString();
                    }
                }

                diagResult.IsSuccess = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SpCatalog] 진단 쿼리 오류");
                diagResult.Summary = "진단 데이터 조회 중 오류가 발생했습니다.";
            }

            return diagResult;
        }

        // ══════════════════════════════════════════════════════
        // ■ SOP 가이드 조회 (Mode C용)
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// SOP 테이블에서 조치 가이드 직접 조회 (Mode C)
        /// </summary>
        public async Task<List<PA999SopEntry>> GetSopEntriesAsync(string errorType)
        {
            var entries = new List<PA999SopEntry>();
            try
            {
                var sql = $@"
                    SELECT SOP_ID, SP_ID, ERROR_TYPE, CAUSE_DESC,
                           ACTION_GUIDE, MENU_PATH, SEVERITY
                    FROM PA999_SP_SOP
                    WHERE ERROR_TYPE = N'{EscapeSql(errorType)}' AND USE_YN = 'Y'
                    ORDER BY SOP_ID";

                var result = await _db.ExecuteQueryAsync(sql);
                if (!result.IsSuccess) return entries;

                foreach (var row in result.Rows)
                {
                    entries.Add(new PA999SopEntry
                    {
                        SopId       = Convert.ToInt32(row["SOP_ID"]),
                        SpId        = row["SP_ID"] is not null and not DBNull
                            ? Convert.ToInt32(row["SP_ID"]) : null,
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
                _logger.LogWarning(ex, "[SpCatalog] SOP 조회 오류 | ErrorType={E}", errorType);
            }
            return entries;
        }

        // ══════════════════════════════════════════════════════
        // ■ 헬퍼
        // ══════════════════════════════════════════════════════

        /// <summary>SP명이 PA999_SP_CATALOG에 등록되어 있는지 검증</summary>
        private async Task<bool> IsRegisteredSpAsync(string spName)
        {
            var sql = $"SELECT COUNT(*) AS CNT FROM PA999_SP_CATALOG WHERE SP_NAME = N'{EscapeSql(spName)}' AND USE_YN = 'Y'";
            var result = await _db.ExecuteQueryAsync(sql);
            if (!result.IsSuccess || result.Rows.Count == 0) return false;
            return Convert.ToInt32(result.Rows[0]["CNT"]) > 0;
        }

        private static SqlDbType MapSqlDbType(string dataType)
        {
            return (dataType?.ToUpper() ?? "NVARCHAR") switch
            {
                "INT"      => SqlDbType.Int,
                "BIGINT"   => SqlDbType.BigInt,
                "NUMERIC"  => SqlDbType.Decimal,
                "DECIMAL"  => SqlDbType.Decimal,
                "BIT"      => SqlDbType.Bit,
                "DATETIME" => SqlDbType.DateTime,
                "DATE"     => SqlDbType.Date,
                "NCHAR"    => SqlDbType.NChar,
                "CHAR"     => SqlDbType.Char,
                "VARCHAR"  => SqlDbType.VarChar,
                _          => SqlDbType.NVarChar
            };
        }

        private static string EscapeSql(string value)
            => value.Replace("'", "''");
    }

    // ══════════════════════════════════════════════════════════
    // ■ SP 실행 결과 모델
    // ══════════════════════════════════════════════════════════

    public class PA999SpParamDef
    {
        public int     ParamSeq    { get; set; }
        public string  ParamName   { get; set; } = "";
        public string  ParamDesc   { get; set; } = "";
        public string  DataType    { get; set; } = "NVARCHAR";
        public int     MaxLength   { get; set; }
        public bool    IsRequired  { get; set; }
        public bool    IsOutput    { get; set; }
        public string? DefaultVal  { get; set; }
        public string? MappingHint { get; set; }
    }

    public class PA999SpExecutionResult
    {
        public string  SpName       { get; set; } = "";
        public bool    IsSuccess    { get; set; }
        public string? ErrorMessage { get; set; }
        public List<string>                      Columns      { get; set; } = new();
        public List<Dictionary<string, object?>>  Rows         { get; set; } = new();
        public Dictionary<string, string?>        OutputValues { get; set; } = new();
    }

    public class PA999DiagResult
    {
        public bool    IsSuccess  { get; set; }
        public string? Summary    { get; set; }

        // 자동마감 헤더 (STATUS_CD, TX_STATUS)
        public List<Dictionary<string, object?>>? AutoCloseHeaders { get; set; }
        // 자동마감 디테일 (부족 자재)
        public List<Dictionary<string, object?>>? AutoCloseDetails { get; set; }
        // 부족 자재의 저장위치별 현재 재고
        public List<Dictionary<string, object?>>? StockStatus { get; set; }

        // SOP 가이드
        public string? SopCauseDesc   { get; set; }
        public string? SopActionGuide { get; set; }
        public string? SopMenuPath    { get; set; }
    }
}
