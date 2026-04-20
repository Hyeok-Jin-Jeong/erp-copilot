using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Bizentro.App.SV.PP.PA999S1_CKO087.Models;

namespace Bizentro.App.SV.PP.PA999S1_CKO087.Services
{
    /// <summary>
    /// PA999S1 - MSSQL 읽기 전용 쿼리 실행 서비스
    /// ※ SELECT만 허용 / 최대 100행 / 30초 타임아웃
    /// </summary>
    public class PA999DbService
    {
        private readonly string _connectionString;
        private readonly ILogger<PA999DbService> _logger;
        private readonly PA999DemoDataService    _demo;

        private const int MaxRows              = 100;
        private const int CommandTimeoutSeconds = 30;

        // DB 연결 가용 여부 캐시 (최초 실패 후 5분간 재시도 생략)
        private static bool   _dbAvailable     = true;
        private static DateTime _dbRetryAfter  = DateTime.MinValue;

        public PA999DbService(IOptions<PA999Options> options, ILogger<PA999DbService> logger)
        {
            // Railway/클라우드 환경에서 내부망 DB 미접근 시 무한 대기 방지 (Connect Timeout=5)
            var cs = EnsureConnectTimeout(options.Value.ConnectionString, 5);
            // Railway 환경에서 TLS 핸드셰이크 오류 방지 (Encrypt=False, TrustServerCertificate=True)
            _connectionString = EnsureRailwayTlsSettings(cs);
            _logger           = logger;
            _demo             = new PA999DemoDataService();
        }

        private static string EnsureConnectTimeout(string cs, int seconds)
        {
            if (string.IsNullOrWhiteSpace(cs)) return cs;
            var lower = cs.ToLower();
            if (lower.Contains("connect timeout") || lower.Contains("connection timeout"))
                return cs;
            return cs.TrimEnd(';') + $";Connect Timeout={seconds}";
        }

        /// <summary>
        /// Railway/클라우드 MSSQL 연결 시 TLS pre-login 핸드셰이크 오류 방지
        /// "Connection reset by peer" 해결: Encrypt=False + TrustServerCertificate=True
        /// </summary>
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

        /// <summary>DB 연결이 현재 사용 가능한지 확인 (실패 캐시 포함)</summary>
        public bool IsDatabaseAvailable => _dbAvailable || DateTime.UtcNow > _dbRetryAfter;

        /// <summary>
        /// SELECT 쿼리 실행 → PA999QueryResult 반환
        /// DB 연결 불가 시 PA999DemoDataService 폴백 (포트폴리오 데모용)
        /// </summary>
        public async Task<PA999QueryResult> ExecuteQueryAsync(string sql)
        {
            // DB 연결 불가 캐시 — 재시도 시간 전이면 즉시 Demo 반환
            if (!_dbAvailable && DateTime.UtcNow < _dbRetryAfter)
            {
                _logger.LogWarning("[PA999DbService] DB 불가 캐시 — Demo 폴백 반환");
                return _demo.GetDemoResult(sql);
            }

            var result = new PA999QueryResult();

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(sql, conn)
                {
                    CommandTimeout = CommandTimeoutSeconds,
                    CommandType    = CommandType.Text
                };

                using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleResult);

                // 컬럼명 수집
                for (int i = 0; i < reader.FieldCount; i++)
                    result.Columns.Add(reader.GetName(i));

                // 데이터 수집 (최대 MaxRows)
                int rowCount = 0;
                while (await reader.ReadAsync() && rowCount < MaxRows)
                {
                    var row = new Dictionary<string, object?>();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        object? value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                        if (value is DateTime dt)
                            value = dt.ToString("yyyy-MM-dd HH:mm:ss");
                        row[result.Columns[i]] = value;
                    }
                    result.Rows.Add(row);
                    rowCount++;
                }

                result.IsSuccess = true;
                _dbAvailable     = true;   // 성공 시 캐시 리셋
            }
            catch (SqlException ex) when (ex.Number == 0 || ex.Class >= 20)
            {
                // 연결 오류 (네트워크/서버 불가) → Demo 폴백
                _logger.LogWarning(ex, "[PA999DbService] DB 연결 실패 → Demo 폴백");
                MarkDbUnavailable();
                return _demo.GetDemoResult(sql);
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "[PA999DbService] SQL 실행 오류: {Sql}", sql);
                result.IsSuccess    = false;
                result.ErrorMessage = $"SQL 실행 오류: {ex.Message}";
            }
            catch (Exception ex)
            {
                // 네트워크/기타 오류 → Demo 폴백
                _logger.LogWarning(ex, "[PA999DbService] DB 접속 불가 → Demo 폴백");
                MarkDbUnavailable();
                return _demo.GetDemoResult(sql);
            }

            return result;
        }

        private static void MarkDbUnavailable()
        {
            _dbAvailable   = false;
            _dbRetryAfter  = DateTime.UtcNow.AddMinutes(5);  // 5분 후 재시도
        }

        /// <summary>
        /// UPDATE/INSERT 등 Non-Query 실행 → PA999QueryResult 반환
        /// PA999MetaBatchService 에서 PA999_TABLE_META UPDATE 시 사용
        /// </summary>
        public async Task<PA999QueryResult> ExecuteNonQueryAsync(string sql)
        {
            var result = new PA999QueryResult();

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(sql, conn)
                {
                    CommandTimeout = CommandTimeoutSeconds,
                    CommandType    = CommandType.Text
                };

                await cmd.ExecuteNonQueryAsync();
                result.IsSuccess = true;
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "[PA999DbService] NonQuery 실행 오류: {Sql}", sql);
                result.IsSuccess    = false;
                result.ErrorMessage = $"SQL 실행 오류: {ex.Message}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PA999DbService] DB 연결 오류");
                result.IsSuccess    = false;
                result.ErrorMessage = "DB 연결 오류가 발생했습니다.";
            }

            return result;
        }

        /// <summary>
        /// 관리자 분석 전용 쿼리 실행 (행수 제한 없음)
        /// PA999AdminQuery 엔드포인트 전용으로만 사용
        /// </summary>
        public async Task<PA999QueryResult> ExecuteUnlimitedQueryAsync(string sql)
        {
            var result = new PA999QueryResult();
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                using var cmd = new SqlCommand(sql, conn)
                {
                    CommandTimeout = 120,
                    CommandType    = CommandType.Text
                };
                using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleResult);
                for (int i = 0; i < reader.FieldCount; i++)
                    result.Columns.Add(reader.GetName(i));
                while (await reader.ReadAsync())
                {
                    var row = new Dictionary<string, object?>();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        object? value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                        if (value is DateTime dt)
                            value = dt.ToString("yyyy-MM-dd HH:mm:ss");
                        row[result.Columns[i]] = value;
                    }
                    result.Rows.Add(row);
                }
                result.IsSuccess = true;
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "[PA999DbService] Unlimited 쿼리 오류: {Sql}", sql);
                result.IsSuccess    = false;
                result.ErrorMessage = $"SQL 실행 오류: {ex.Message}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PA999DbService] DB 연결 오류");
                result.IsSuccess    = false;
                result.ErrorMessage = "DB 연결 오류가 발생했습니다.";
            }
            return result;
        }
    }
}
