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

        private const int MaxRows              = 100;
        private const int CommandTimeoutSeconds = 30;

        public PA999DbService(IOptions<PA999Options> options, ILogger<PA999DbService> logger)
        {
            // Railway/클라우드 환경에서 내부망 DB 미접근 시 무한 대기 방지 (Connect Timeout=5)
            _connectionString = EnsureConnectTimeout(options.Value.ConnectionString, 5);
            _logger           = logger;
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
        /// SELECT 쿼리 실행 → PA999QueryResult 반환
        /// </summary>
        public async Task<PA999QueryResult> ExecuteQueryAsync(string sql)
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
                        // DateTime 포맷 통일
                        if (value is DateTime dt)
                            value = dt.ToString("yyyy-MM-dd HH:mm:ss");
                        row[result.Columns[i]] = value;
                    }
                    result.Rows.Add(row);
                    rowCount++;
                }

                result.IsSuccess = true;
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "[PA999DbService] SQL 실행 오류: {Sql}", sql);
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
