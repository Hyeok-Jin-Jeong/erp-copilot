using Microsoft.Extensions.Caching.Memory;
using Bizentro.App.SV.PP.PA999S1_CKO087.Models;

namespace Bizentro.App.SV.PP.PA999S1_CKO087.Services
{
    /// <summary>
    /// PA999S1 - 쿼리 결과 캐시 서비스
    ///
    /// ▶ 목적: 챗봇이 동일한 SQL을 반복 실행할 때 DB를 거치지 않고
    ///         메모리 캐시에서 결과를 바로 반환하여 운영 DB 부하 감소
    ///
    /// ▶ 캐시 TTL 전략
    ///   - 마스터 데이터 (품목/공장/거래처)  : 1시간  - 거의 변하지 않는 기준정보
    ///   - 일반 조회 (생산실적/발주/전표)    : 10분   - 업무시간 중 자주 조회
    ///   - 실시간 데이터 (재고/마감여부)     : 3분    - 빠르게 변하는 데이터
    ///
    /// ▶ 캐시 키: SQL 문자열을 정규화(공백/대소문자 통일) 후 SHA256 해시
    ///   → 동일한 의미의 쿼리는 항상 같은 키로 처리
    /// </summary>
    public class PA999QueryCacheService
    {
        private readonly IMemoryCache                    _cache;
        private readonly PA999DbService                  _dbService;
        private readonly ILogger<PA999QueryCacheService> _logger;

        private static readonly TimeSpan TtlMaster   = TimeSpan.FromHours(1);
        private static readonly TimeSpan TtlDefault  = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan TtlRealtime = TimeSpan.FromMinutes(3);
        private static readonly TimeSpan TtlNoCache  = TimeSpan.Zero;  // 캐시 제외

        // 1시간 TTL + 무효화: 변경이 드문 기준 마스터
        private static readonly string[] MasterKeywords =
            { "B_ITEM", "B_PLANT", "B_BIZ_PARTNER", "B_EMPLOYEE", "A_ACCT", "CODE_VALUES" };

        // 3분 TTL만 적용 (무효화 없음): 변경 잦은 실시간 데이터
        private static readonly string[] RealtimeKeywords =
            { "STOCK", "재고", "MAGAM", "마감", "INOUT", "입출고", "ORDER_QTY", "수주잔" };

        // 캐시 제외: 오차 절대 불허 데이터 (현재는 없음, 필요시 추가)
        private static readonly string[] NoCacheKeywords =
            { };

        // ★ 테이블명 → 관련 캐시 키 목록 (무효화용 역인덱스)
        // Key: 테이블명 대문자,  Value: 해당 테이블 조회로 생성된 캐시 키 Set
        private readonly Dictionary<string, HashSet<string>> _tableKeyIndex = new();
        private readonly object _indexLock = new();

        public PA999QueryCacheService(
            IMemoryCache cache,
            PA999DbService dbService,
            ILogger<PA999QueryCacheService> logger)
        {
            _cache     = cache;
            _dbService = dbService;
            _logger    = logger;
        }

        /// <summary>
        /// 캐시 우선 조회 → 없으면 DB 실행 후 캐시 저장
        /// PA999ChtbotService Step 5에서 호출
        /// </summary>
        public async Task<PA999QueryResult> ExecuteWithCacheAsync(string sql)
        {
            var cacheKey = BuildCacheKey(sql);

            // ── 캐시 HIT: DB 접근 없이 즉시 반환 ──────────────────
            if (_cache.TryGetValue(cacheKey, out PA999QueryResult? cached) && cached != null)
            {
                _logger.LogInformation("[QueryCache] HIT | 키={K} | 행수={R}",
                    cacheKey[^8..], cached.Rows.Count);
                return cached;
            }

            // ── 캐시 MISS: DB 실행 ─────────────────────────────────
            _logger.LogInformation("[QueryCache] MISS → DB 실행 | 키={K}", cacheKey[^8..]);
            var result = await _dbService.ExecuteQueryAsync(sql);

            if (result.IsSuccess)
            {
                var ttl = DetermineTtl(sql);

                if (ttl == TtlNoCache)
                {
                    // 캐시 제외 대상: 항상 DB 직접 조회
                    _logger.LogInformation("[QueryCache] 캐시 제외 대상 (항상 DB 직접 조회)");
                }
                else
                {
                    _cache.Set(cacheKey, result, ttl);
                    RegisterTableIndex(sql, cacheKey);
                    _logger.LogInformation("[QueryCache] 캐시 저장 | TTL={T}분 | 행수={R}",
                        (int)ttl.TotalMinutes, result.Rows.Count);
                }
            }

            return result;
        }

        // ══════════════════════════════════════════════════════
        // ★ Cache Invalidation (캐시 무효화)
        // CUD 작업 발생 시 관련 테이블 캐시를 즉시 삭제
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 특정 테이블과 관련된 캐시 전체 즉시 삭제
        /// ERP CUD SP 실행 완료 후 호출
        ///
        /// 예) 생산실적 저장 완료 → InvalidateByTable("M_PROD_RESULT")
        /// </summary>
        public void InvalidateByTable(string tableName)
        {
            var key = tableName.ToUpperInvariant();
            lock (_indexLock)
            {
                if (!_tableKeyIndex.TryGetValue(key, out var cacheKeys) || cacheKeys.Count == 0)
                {
                    _logger.LogInformation("[QueryCache] 무효화 대상 없음: {T}", tableName);
                    return;
                }

                int removed = 0;
                foreach (var cacheKey in cacheKeys)
                {
                    _cache.Remove(cacheKey);
                    removed++;
                }
                _tableKeyIndex.Remove(key);

                _logger.LogInformation("[QueryCache] 캐시 무효화 완료 | 테이블={T} | 삭제={N}건",
                    tableName, removed);
            }
        }

        /// <summary>
        /// 전체 캐시 삭제 (긴급 초기화용)
        /// POST /api/PA999/admin/cache/clear 에서 호출
        /// </summary>
        public void InvalidateAll()
        {
            lock (_indexLock)
            {
                var allKeys = _tableKeyIndex.Values.SelectMany(s => s).Distinct().ToList();
                foreach (var key in allKeys)
                    _cache.Remove(key);

                _tableKeyIndex.Clear();
                _logger.LogWarning("[QueryCache] 전체 캐시 강제 삭제 | {N}건", allKeys.Count);
            }
        }

        // ── TTL 결정 ───────────────────────────────────────────────

        private static TimeSpan DetermineTtl(string sql)
        {
            var upper = sql.ToUpperInvariant();

            if (MasterKeywords.Any(k => upper.Contains(k)))
                return TtlMaster;

            if (RealtimeKeywords.Any(k => upper.Contains(k)))
                return TtlRealtime;

            return TtlDefault;
        }

        // ── 역인덱스 등록 ──────────────────────────────────────────

        /// <summary>SQL에서 테이블명 추출 → cacheKey를 역인덱스에 등록</summary>
        private void RegisterTableIndex(string sql, string cacheKey)
        {
            // FROM 또는 JOIN 뒤에 오는 단어를 테이블명으로 추출
            var matches = System.Text.RegularExpressions.Regex.Matches(
                sql.ToUpperInvariant(),
                @"(?:FROM|JOIN)\s+([A-Z0-9_]+)");

            lock (_indexLock)
            {
                foreach (System.Text.RegularExpressions.Match m in matches)
                {
                    var tbl = m.Groups[1].Value;
                    if (!_tableKeyIndex.ContainsKey(tbl))
                        _tableKeyIndex[tbl] = new HashSet<string>();
                    _tableKeyIndex[tbl].Add(cacheKey);
                }
            }
        }

        // ── 캐시 키 생성 ───────────────────────────────────────────

        private static string BuildCacheKey(string sql)
        {
            var normalized = System.Text.RegularExpressions.Regex
                .Replace(sql.Trim().ToUpperInvariant(), @"\s+", " ");

            using var sha = System.Security.Cryptography.SHA256.Create();
            var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(normalized));
            return "PA999:qry:" + Convert.ToHexString(bytes)[..16];
        }
    }
}
