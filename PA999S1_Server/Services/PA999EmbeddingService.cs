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

        /// <summary>OpenAI API Key 설정 여부 — false이면 모든 임베딩 메서드가 null/빈값 반환</summary>
        public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.OpenAIApiKey);

        public PA999EmbeddingService(
            IHttpClientFactory httpFactory,
            IOptions<PA999Options> options,
            ILogger<PA999EmbeddingService> logger)
        {
            _httpFactory = httpFactory;
            _options     = options.Value;
            _logger      = logger;

            if (!IsConfigured)
                _logger.LogInformation("[Embedding] OpenAIApiKey 미설정 → RAG 임베딩 비활성화");
            else
                _logger.LogInformation("[Embedding] RAG 임베딩 활성화 | 모델: {M}, TopK: {K}",
                    _options.EmbeddingModel, _options.EmbeddingTopK);
        }

        // ══════════════════════════════════════════════════════
        // ▶ 임베딩 생성 (단건)
        // ══════════════════════════════════════════════════════

        /// <summary>텍스트 → float[] 임베딩 벡터 반환. 비활성화 or 오류 시 null.</summary>
        public async Task<float[]?> GetEmbeddingAsync(string text)
        {
            if (!IsConfigured) return null;

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

                return ParseEmbeddingResponse(await response.Content.ReadAsStringAsync());
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Embedding] GetEmbeddingAsync 예외");
                return null;
            }
        }

        // ══════════════════════════════════════════════════════
        // ▶ 임베딩 생성 (배치)
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 텍스트 목록 → 임베딩 벡터 목록 반환 (순서 보장).
        /// 비활성화 or 오류 시 null.
        /// OpenAI API는 배열 입력을 지원하므로 단일 요청으로 처리.
        /// </summary>
        public async Task<List<float[]>?> GetBatchEmbeddingsAsync(IList<string> texts)
        {
            if (!IsConfigured || texts.Count == 0) return null;

            try
            {
                var client = _httpFactory.CreateClient("OpenAIEmbedding");
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", _options.OpenAIApiKey);

                var body = JsonSerializer.Serialize(new
                {
                    input = texts,
                    model = _options.EmbeddingModel,
                });

                var response = await client.PostAsync(
                    EmbeddingEndpoint,
                    new StringContent(body, Encoding.UTF8, "application/json"));

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("[Embedding] Batch API 오류 {Code}", response.StatusCode);
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var data = doc.RootElement.GetProperty("data");

                // API 응답은 index 순서 보장됨
                var results = new List<float[]>(data.GetArrayLength());
                foreach (var item in data.EnumerateArray())
                {
                    var arr = item.GetProperty("embedding");
                    var vec = new float[arr.GetArrayLength()];
                    int i = 0;
                    foreach (var el in arr.EnumerateArray())
                        vec[i++] = el.GetSingle();
                    results.Add(vec);
                }

                _logger.LogDebug("[Embedding] 배치 임베딩 완료 {Count}건", results.Count);
                return results;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Embedding] GetBatchEmbeddingsAsync 예외");
                return null;
            }
        }

        // ══════════════════════════════════════════════════════
        // ▶ 유사도 계산 (static — ChatLogService에서 직접 호출)
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

        // ══════════════════════════════════════════════════════
        // ▶ 직렬화 / 역직렬화 (static — DB EMBEDDING 컬럼용 JSON)
        // ══════════════════════════════════════════════════════

        /// <summary>float[] → JSON 문자열 (DB 저장용)</summary>
        public static string SerializeEmbedding(float[]? embedding)
        {
            if (embedding == null || embedding.Length == 0) return "[]";
            return JsonSerializer.Serialize(embedding);
        }

        /// <summary>JSON 문자열 → float[] (DB 조회 후 복원용). null or 빈값 → null.</summary>
        public static float[]? DeserializeEmbedding(string? json)
        {
            if (string.IsNullOrWhiteSpace(json) || json == "[]") return null;
            try
            {
                return JsonSerializer.Deserialize<float[]>(json);
            }
            catch
            {
                return null;
            }
        }

        // ══════════════════════════════════════════════════════
        // ▶ 내부 유틸
        // ══════════════════════════════════════════════════════

        private static float[]? ParseEmbeddingResponse(string responseJson)
        {
            try
            {
                using var doc = JsonDocument.Parse(responseJson);
                var arr = doc.RootElement
                             .GetProperty("data")[0]
                             .GetProperty("embedding");

                var result = new float[arr.GetArrayLength()];
                int i = 0;
                foreach (var el in arr.EnumerateArray())
                    result[i++] = el.GetSingle();
                return result;
            }
            catch { return null; }
        }
    }
}
