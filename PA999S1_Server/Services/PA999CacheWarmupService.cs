using Microsoft.Extensions.Caching.Memory;
using Bizentro.App.SV.PP.PA999S1_CKO087.Models;

namespace Bizentro.App.SV.PP.PA999S1_CKO087.Services
{
    /// <summary>
    /// PA999S1 - 캐시 예열(Warm-up) 서비스
    ///
    /// ▶ 역할: 서버 시작 시 자동으로 자주 쓰이는 데이터를 미리 캐시에 적재
    ///         → 첫 사용자 질문도 캐시 HIT으로 빠르게 응답 가능
    ///
    /// ▶ 예열 대상 (서버 시작 순서대로)
    ///   1. 전체 테이블 목록 (PA999SchemaService)
    ///   2. PA999_TABLE_META 전체 (Step1 테이블 식별 기반)
    ///   3. 핵심 마스터 테이블 스키마 (B_PLANT, B_ITEM, A_ACCT 등)
    ///   4. 공장 목록 / 품목 목록 (자주 쓰는 마스터 쿼리)
    ///
    /// ▶ IHostedService 구현 → Program.cs에 AddHostedService 등록 필요
    /// </summary>
    public class PA999CacheWarmupService : IHostedService
    {
        private readonly PA999SchemaService          _schemaService;
        private readonly PA999QueryCacheService      _queryCache;
        private readonly IMemoryCache                _cache;
        private readonly ILogger<PA999CacheWarmupService> _logger;

        // 예열할 핵심 마스터 테이블 목록
        private static readonly List<string> WarmupSchemaTables = new()
        {
            "B_PLANT", "B_ITEM", "B_BIZ_PARTNER", "B_EMPLOYEE",
            "A_ACCT", "PA999_TABLE_META"
        };

        // 예열할 마스터 쿼리 목록 (자주 사용되는 조회)
        private static readonly List<(string Label, string Sql)> WarmupQueries = new()
        {
            ("공장 목록",
             "SELECT PLANT_CD, PLANT_NM, BIZ_AREA_CD FROM B_PLANT WITH(NOLOCK) WHERE USE_YN='Y' ORDER BY PLANT_CD"),

            ("품목 목록 (상위 200)",
             "SELECT TOP 200 ITEM_CD, ITEM_NM, ITEM_TYPE_CD FROM B_ITEM WITH(NOLOCK) WHERE USE_YN='Y' ORDER BY ITEM_CD"),

            ("계정과목 목록",
             "SELECT ACCT_CD, ACCT_NM, ACCT_TYPE_CD FROM A_ACCT WITH(NOLOCK) WHERE USE_YN='Y' ORDER BY ACCT_CD"),

            ("TABLE META 전체",
             "SELECT TABLE_NM, TABLE_DESC, KEYWORD_LIST FROM PA999_TABLE_META WITH(NOLOCK) WHERE USE_YN='Y' ORDER BY TABLE_NM"),
        };

        public PA999CacheWarmupService(
            PA999SchemaService schemaService,
            PA999QueryCacheService queryCache,
            IMemoryCache cache,
            ILogger<PA999CacheWarmupService> logger)
        {
            _schemaService = schemaService;
            _queryCache    = queryCache;
            _cache         = cache;
            _logger        = logger;
        }

        // ══════════════════════════════════════════════════════
        // ▶ 서버 시작 시 자동 실행
        // ══════════════════════════════════════════════════════

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("═══════════════════════════════════════════");
            _logger.LogInformation(" [CacheWarmup] 캐시 예열 시작...");
            _logger.LogInformation("═══════════════════════════════════════════");

            var sw = System.Diagnostics.Stopwatch.StartNew();

            // ① 전체 테이블 목록 예열
            await WarmupTableListAsync(cancellationToken);

            // ② 핵심 테이블 스키마 예열
            await WarmupSchemaAsync(cancellationToken);

            // ③ 자주 쓰는 마스터 쿼리 결과 예열
            await WarmupQueriesAsync(cancellationToken);

            sw.Stop();
            _logger.LogInformation("═══════════════════════════════════════════");
            _logger.LogInformation(" [CacheWarmup] 예열 완료 | 소요 {Ms}ms", sw.ElapsedMilliseconds);
            _logger.LogInformation("═══════════════════════════════════════════");
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        // ══════════════════════════════════════════════════════
        // ▶ 예열 단계별 실행
        // ══════════════════════════════════════════════════════

        /// <summary>① 전체 테이블 목록 → PA999SchemaService 캐시에 적재</summary>
        private async Task WarmupTableListAsync(CancellationToken ct)
        {
            try
            {
                _logger.LogInformation("[CacheWarmup] ① 전체 테이블 목록 로드 중...");
                var tables = _schemaService.GetAllTableNames();
                _logger.LogInformation("[CacheWarmup] ① 완료 | {N}개 테이블 캐시됨", tables.Count);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[CacheWarmup] ① 테이블 목록 예열 실패 (서버 시작은 계속)");
            }
            await Task.CompletedTask;
        }

        /// <summary>② 핵심 테이블 스키마(컬럼 정보) → PA999SchemaService 캐시에 적재</summary>
        private async Task WarmupSchemaAsync(CancellationToken ct)
        {
            _logger.LogInformation("[CacheWarmup] ② 핵심 테이블 스키마 로드 중...");
            int loaded = 0;

            foreach (var tableName in WarmupSchemaTables)
            {
                if (ct.IsCancellationRequested) break;
                try
                {
                    // GetSchemaContextAsync 내부에서 캐시에 저장됨
                    await _schemaService.GetSchemaContextAsync(new List<string> { tableName });
                    loaded++;
                    _logger.LogInformation("[CacheWarmup]   스키마 캐시 완료: {T}", tableName);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[CacheWarmup]   스키마 캐시 실패: {T}", tableName);
                }
            }

            _logger.LogInformation("[CacheWarmup] ② 완료 | {N}/{Total}개 테이블 스키마 캐시됨",
                loaded, WarmupSchemaTables.Count);
        }

        /// <summary>③ 자주 쓰는 마스터 쿼리 실행 결과 → PA999QueryCacheService 캐시에 적재</summary>
        private async Task WarmupQueriesAsync(CancellationToken ct)
        {
            _logger.LogInformation("[CacheWarmup] ③ 마스터 쿼리 결과 로드 중...");
            int loaded = 0;

            foreach (var (label, sql) in WarmupQueries)
            {
                if (ct.IsCancellationRequested) break;
                try
                {
                    var result = await _queryCache.ExecuteWithCacheAsync(sql);
                    if (result.IsSuccess)
                    {
                        loaded++;
                        _logger.LogInformation("[CacheWarmup]   쿼리 캐시 완료: {L} ({R}건)",
                            label, result.Rows.Count);
                    }
                    else
                    {
                        _logger.LogWarning("[CacheWarmup]   쿼리 캐시 실패: {L} | 오류: {E}",
                            label, result.ErrorMessage);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[CacheWarmup]   쿼리 캐시 예외: {L}", label);
                }
            }

            _logger.LogInformation("[CacheWarmup] ③ 완료 | {N}/{Total}개 쿼리 캐시됨",
                loaded, WarmupQueries.Count);
        }
    }
}
