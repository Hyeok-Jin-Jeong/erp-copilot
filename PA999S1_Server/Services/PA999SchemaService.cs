using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Bizentro.App.SV.PP.PA999S1_CKO087.Models;

namespace Bizentro.App.SV.PP.PA999S1_CKO087.Services
{
    /// <summary>
    /// PA999S1 - UNIERP DB 스키마 메타데이터 제공 서비스
    /// Claude의 System Prompt에 주입할 테이블/컬럼/FK 정보를 동적으로 조회합니다.
    /// 조회 결과는 메모리 캐시(1시간)로 관리합니다.
    /// </summary>
    public class PA999SchemaService
    {
        private readonly string _connectionString;
        private readonly IMemoryCache _cache;
        private readonly ILogger<PA999SchemaService> _logger;

        private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);

        // ── 테이블 접두사 → 업무 설명 (Claude 이해도 향상용) ─────────────
        private static readonly Dictionary<string, string> PrefixDescriptions = new()
        {
            { "A_ACCT",       "계정과목 마스터" },
            { "A_ASSET",      "고정자산" },
            { "A_AP",         "매입채무(AP)" },
            { "A_AR",         "매출채권(AR)" },
            { "A_ALLC",       "배부" },
            { "A_SLIP",       "전표" },
            { "A_BDG",        "예산" },
            { "B_BIZ_AREA",   "사업장" },
            { "B_BIZ_PARTNER","거래처(BP)" },
            { "B_ACCT_DEPT",  "회계부서" },
            { "B_COST_CENTER","비용센터" },
            { "B_BANK",       "은행" },
            { "B_ITEM",       "품목" },
            { "B_PLANT",      "공장/플랜트" },
            { "B_EMPLOYEE",   "직원" },
            { "H_EMP",        "인사/직원" },
            { "M_",           "제조(Manufacturing)" },
            { "P_",           "구매(Procurement)" },
            { "S_",           "영업/판매(Sales)" },
            { "L_",           "물류(Logistics)" },
            { "C_",           "원가(Cost)" },
            { "ZTF",          "인터페이스/통계" },
        };

        // ── 공통 컬럼명 → 한국어 설명 ────────────────────────────────────
        private static readonly Dictionary<string, string> ColumnDescriptions = new()
        {
            { "INSRT_USER_ID", "등록자ID"          },
            { "INSRT_DT",      "등록일시"          },
            { "UPDT_USER_ID",  "수정자ID"          },
            { "UPDT_DT",       "수정일시"          },
            { "DEL_FG",        "삭제여부(Y/N)"     },
            { "USE_YN",        "사용여부(Y/N)"     },
            { "USE_FG",        "사용여부(Y/N)"     },
            { "CO_CODE",       "회사코드"          },
            { "BIZ_AREA_CD",   "사업장코드"        },
            { "DEPT_CD",       "부서코드"          },
            { "COST_CD",       "비용센터코드"      },
            { "ACCT_CD",       "계정코드"          },
            { "ACCT_NM",       "계정명"            },
            { "BP_CD",         "거래처코드"        },
            { "BP_NM",         "거래처명"          },
            { "EMP_NO",        "직원번호"          },
            { "EMP_NM",        "직원명"            },
            { "PLANT_CD",      "공장코드"          },
            { "PLANT_NM",      "공장명"            },
            { "YYYYMM",        "년월(YYYYMM)"      },
            { "YYYY",          "년도(YYYY)"        },
            { "AMT",           "금액"              },
            { "QTY",           "수량"              },
            { "CURRENCY",      "통화"              },
            { "ITEM_CD",       "품목코드"          },
            { "ITEM_NM",       "품목명"            },
        };

        public PA999SchemaService(
            IOptions<PA999Options> options,
            IMemoryCache cache,
            ILogger<PA999SchemaService> logger)
        {
            _connectionString = options.Value.ConnectionString;
            _cache  = cache;
            _logger = logger;
        }

        // ── 전체 테이블 목록 (캐시) ───────────────────────────────────────

        /// <summary>DB의 전체 테이블 이름 목록 반환 (캐시 1시간)</summary>
        public HashSet<string> GetAllTableNames()
        {
            return _cache.GetOrCreate("PA999:schema:all_tables", entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CacheDuration;
                return LoadAllTableNamesFromDb();
            })!;
        }

        private HashSet<string> LoadAllTableNamesFromDb()
        {
            var tables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using var conn = new SqlConnection(_connectionString);
                conn.Open();
                using var cmd = new SqlCommand(
                    "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE='BASE TABLE' ORDER BY TABLE_NAME",
                    conn);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                    tables.Add(reader.GetString(0).ToUpper());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PA999SchemaService] 전체 테이블 목록 로드 실패");
            }
            return tables;
        }

        // ── 스키마 컨텍스트 생성 (Claude System Prompt 주입용) ────────────

        /// <summary>
        /// 지정 테이블들의 컬럼/FK 정보를 마크다운 형식으로 반환
        /// Claude System Prompt에 직접 주입합니다.
        /// </summary>
        public async Task<string> GetSchemaContextAsync(List<string> tableNames)
        {
            if (tableNames == null || tableNames.Count == 0)
                tableNames = GetCoreTableNames();

            var sb = new StringBuilder();

            foreach (var tableName in tableNames.Take(6))
            {
                var columns = await GetTableColumnsAsync(tableName);
                if (columns.Count == 0) continue;

                var desc = GetTableDescription(tableName);
                sb.AppendLine($"### {tableName}" + (desc != null ? $"  ※ {desc}" : ""));
                sb.AppendLine("| 컬럼명 | 타입 | NULL | 설명 |");
                sb.AppendLine("|--------|------|------|------|");

                foreach (var col in columns)
                {
                    var typeStr = col.DataType + (col.MaxLength > 0 ? $"({col.MaxLength})" : "");
                    var nullStr = col.IsNullable ? "Y" : "N";
                    var colDesc = GetColumnDescription(col.ColumnName);
                    sb.AppendLine($"| {col.ColumnName} | {typeStr} | {nullStr} | {colDesc} |");
                }
                sb.AppendLine();
            }

            // FK 관계 추가
            var fks = await GetForeignKeysAsync(tableNames);
            if (fks.Count > 0)
            {
                sb.AppendLine("### 테이블 관계 (FK)");
                foreach (var fk in fks.Take(10))
                    sb.AppendLine($"- {fk.ParentTable}.{fk.ParentColumn} → {fk.RefTable}.{fk.RefColumn}");
            }

            return sb.ToString();
        }

        // ── 컬럼 정보 조회 (캐시) ────────────────────────────────────────

        private async Task<List<PA999ColumnInfo>> GetTableColumnsAsync(string tableName)
        {
            var cacheKey = $"PA999:schema:columns:{tableName}";
            if (_cache.TryGetValue(cacheKey, out List<PA999ColumnInfo>? cached) && cached != null)
                return cached;

            var columns = new List<PA999ColumnInfo>();
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                using var cmd = new SqlCommand(@"
                    SELECT
                        COLUMN_NAME,
                        DATA_TYPE,
                        ISNULL(CHARACTER_MAXIMUM_LENGTH, 0) AS MAX_LENGTH,
                        IS_NULLABLE
                    FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_NAME = @TableName
                    ORDER BY ORDINAL_POSITION", conn);

                cmd.Parameters.AddWithValue("@TableName", tableName);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    columns.Add(new PA999ColumnInfo
                    {
                        ColumnName = reader.GetString(0),
                        DataType   = reader.GetString(1),
                        MaxLength  = reader.GetInt32(2),
                        IsNullable = reader.GetString(3) == "YES"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PA999SchemaService] 컬럼 정보 로드 실패: {Table}", tableName);
            }

            _cache.Set(cacheKey, columns, CacheDuration);
            return columns;
        }

        // ── FK 관계 조회 ─────────────────────────────────────────────────

        private async Task<List<PA999FkInfo>> GetForeignKeysAsync(List<string> tableNames)
        {
            var fks = new List<PA999FkInfo>();
            if (tableNames.Count == 0) return fks;

            try
            {
                var tableList = string.Join(",", tableNames.Select(t => $"'{t}'"));
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                using var cmd = new SqlCommand($@"
                    SELECT
                        tp.name AS PARENT_TABLE,
                        cp.name AS PARENT_COLUMN,
                        tr.name AS REF_TABLE,
                        cr.name AS REF_COLUMN
                    FROM sys.foreign_keys fk
                    INNER JOIN sys.tables tp  ON fk.parent_object_id      = tp.object_id
                    INNER JOIN sys.tables tr  ON fk.referenced_object_id  = tr.object_id
                    INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
                    INNER JOIN sys.columns cp ON fkc.parent_column_id     = cp.column_id AND tp.object_id = cp.object_id
                    INNER JOIN sys.columns cr ON fkc.referenced_column_id = cr.column_id AND tr.object_id = cr.object_id
                    WHERE tp.name IN ({tableList})
                    ORDER BY tp.name", conn);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    fks.Add(new PA999FkInfo
                    {
                        ParentTable  = reader.GetString(0),
                        ParentColumn = reader.GetString(1),
                        RefTable     = reader.GetString(2),
                        RefColumn    = reader.GetString(3)
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PA999SchemaService] FK 정보 로드 실패");
            }

            return fks;
        }

        // ── 헬퍼 ─────────────────────────────────────────────────────────

        /// <summary>질문과 무관하게 항상 포함되는 핵심 기준 테이블</summary>
        private List<string> GetCoreTableNames() => new()
        {
            "B_PLANT", "B_ITEM", "A_ACCT"
        };

        private string? GetTableDescription(string tableName)
        {
            foreach (var (prefix, desc) in PrefixDescriptions)
                if (tableName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return desc;
            return null;
        }

        private string GetColumnDescription(string columnName)
        {
            return ColumnDescriptions.TryGetValue(columnName.ToUpper(), out var desc)
                ? desc : string.Empty;
        }
    }
}
