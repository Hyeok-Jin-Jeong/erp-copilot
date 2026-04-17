using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Bizentro.App.SV.PP.PA999S1_CKO087.Models;

namespace Bizentro.App.SV.PP.PA999S1_CKO087.Services
{
    /// <summary>
    /// OpenAI text-embedding-3-small 기반 RAG 임베딩 서비스
    ///
    /// ▶ 역할: 사용자 질문 → 임베딩 벡터 생성 → 유사 패턴/피드백 검색 (코사인 유사도)
    /// ▶ Top-K 패턴을 System Prompt에 주입하여 Claude SQL 정확도 향상
    /// ▶ OpenAIApiKey 미설정 시 자동 비활성화 (서버 시작에 영향 없음)
    /// </summary>
    public class PA999EmbeddingService
    {
        private readonly IHttpClientFactory             _httpFactory;
        private readonly PA999Options                   _options;
        private readonly ILogger<PA999EmbeddingService> _logger;

        private const string EmbeddingEndpoint = "https://api.openai.com/v1/embeddings";

        public bool IsEnabled => !string.IsNullOrWhiteSpace(_options.OpenAIApiKey);

        public PA999EmbeddingService(
            IHttpClientFactory httpFactory,
            IOptions<PA999Options> options,
            ILogger<PA999EmbeddingService> logger)
        {
            _httpFactory = httpFactory;
            _options     = options.Value;
            _logger      = logger;

            if (!IsEnabled)
                _logger.LogInformation("[Embedding] OpenAIApiKey 미설정 → RAG 임베딩 비활성화");
            else
                _logger.LogInformation("[Embedding] RAG 임베딩 활성화 | 모델: {M}, TopK: {K}",
                    _options.EmbeddingModel, _options.EmbeddingTopK);
        }

        // ══════════════════════════════════════════════════════
        // ▶ 임베딩 생성
        // ══════════════════════════════════════════════════════

        /// <summary>텍스트 → float[] 임베딩 벡터 반환. 비활성화 or 오류 시 null.</summary>
        public async Task<float[]?> GetEmbeddingAsync(string text)
        {
            if (!IsEnabled) return null;

            try
            {
                var client = _httpFactory.CreateClient("OpenAIEmbedding");
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", _options.OpenAIApiKey);

                var body = JsonSerializer.Serialize(new
                {
                    input = text,
                    model = _options.EmbeddingModel,
                });

                var response = await client.PostAsync(
                    EmbeddingEndpoint,
                    new StringContent(body, Encoding.UTF8, "application/json"));

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("[Embedding] API 오류 {Code}", response.StatusCode);
                    return null;
                }

                using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                var arr = doc.RootElement
                             .GetProperty("data")[0]
                             .GetProperty("embedding");

                var result = new float[arr.GetArrayLength()];
                int i = 0;
                foreach (var el in arr.EnumerateArray())
                    result[i++] = el.GetSingle();

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Embedding] GetEmbeddingAsync 예외");
                return null;
            }
        }

        // ══════════════════════════════════════════════════════
        // ▶ 유사도 계산
        // ══════════════════════════════════════════════════════

        /// <summary>코사인 유사도 (0 ~ 1)</summary>
        public static float CosineSimilarity(float[] a, float[] b)
        {
            if (a.Length != b.Length) return 0f;

            float dot = 0f, normA = 0f, normB = 0f;
            for (int i = 0; i < a.Length; i++)
            {
                dot   += a[i] * b[i];
                normA += a[i] * a[i];
                normB += b[i] * b[i];
            }

            if (normA == 0f || normB == 0f) return 0f;
            return dot / (MathF.Sqrt(normA) * MathF.Sqrt(normB));
        }

        /// <summary>
        /// 후보 텍스트 목록 중 질문과 가장 유사한 Top-K 인덱스 반환
        /// 임베딩 비활성화 시 빈 배열 반환
        /// </summary>
        public async Task<int[]> GetTopKIndicesAsync(string query, IList<string> candidates)
        {
            if (!IsEnabled || candidates.Count == 0) return Array.Empty<int>();

            var queryVec = await GetEmbeddingAsync(query);
            if (queryVec == null) return Array.Empty<int>();

            var scores = new List<(int Index, float Score)>();

            foreach (var (text, idx) in candidates.Select((t, i) => (t, i)))
            {
                var vec = await GetEmbeddingAsync(text);
                if (vec == null) continue;
                scores.Add((idx, CosineSimilarity(queryVec, vec)));
            }

            return scores
                .OrderByDescending(s => s.Score)
                .Take(_options.EmbeddingTopK)
                .Select(s => s.Index)
                .ToArray();
        }
    }
}
