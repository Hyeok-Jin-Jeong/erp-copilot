using System.Net.Http.Headers;
using Bizentro.App.SV.PP.PA999S1_CKO087.Models;
using Bizentro.App.SV.PP.PA999S1_CKO087.Services;

// ══════════════════════════════════════════════════════════════
//  Bizentro.App.SV.PP.PA999S1_CKO087
//  UNIERP AI 챗봇 REST API 서버
//  PA999M1 (UNIERP WinForms UI) ↔ 이 서버 ↔ Claude API / MSSQL
// ══════════════════════════════════════════════════════════════

// ── Railway / 클라우드 PORT 자동 감지 ─────────────────────────
// Railway는 $PORT 환경변수로 포트를 주입. appsettings.json "Urls" 보다 우선 적용.
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port))
    Environment.SetEnvironmentVariable("ASPNETCORE_URLS", $"http://0.0.0.0:{port}");

var builder = WebApplication.CreateBuilder(args);

// ── 1. 설정 바인딩 ────────────────────────────────────────────
builder.Services.Configure<PA999Options>(
    builder.Configuration.GetSection(PA999Options.SectionName));

var options = builder.Configuration
                     .GetSection(PA999Options.SectionName)
                     .Get<PA999Options>()
              ?? throw new Exception($"appsettings.json 에 [{PA999Options.SectionName}] 섹션이 없습니다.");

if (string.IsNullOrWhiteSpace(options.AnthropicApiKey))
    throw new Exception("AnthropicApiKey 가 설정되지 않았습니다. " +
                        "appsettings.json 또는 환경변수 PA999S1__AnthropicApiKey 를 확인하세요.");

// ── 2. Anthropic HttpClient ───────────────────────────────────
builder.Services.AddHttpClient("AnthropicClient", client =>
{
    client.BaseAddress = new Uri("https://api.anthropic.com");
    client.DefaultRequestHeaders.Add("x-api-key",           options.AnthropicApiKey);
    client.DefaultRequestHeaders.Add("anthropic-version",   "2023-06-01");
    client.DefaultRequestHeaders.Accept.Add(
        new MediaTypeWithQualityHeaderValue("application/json"));
    client.Timeout = TimeSpan.FromSeconds(120);
});

// ── 2-b. OpenAI Embedding HttpClient ──────────────────────────
builder.Services.AddHttpClient("OpenAIEmbedding", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});

// ── 3. 서비스 등록 ────────────────────────────────────────────
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<PA999EmbeddingService>();   // ★ RAG 임베딩 서비스 (OpenAI)
builder.Services.AddSingleton<PA999SchemaService>();      // 스키마는 캐시 활용 → Singleton
builder.Services.AddSingleton<PA999DbService>();
builder.Services.AddSingleton<PA999ChatLogService>();     // 로그·피드백 패턴 캐시 → Singleton
builder.Services.AddSingleton<PA999QueryCacheService>();  // ★ 쿼리 결과 캐시 서비스
builder.Services.AddSingleton<PA999ChtbotService>();      // 세션 히스토리 유지 → Singleton
builder.Services.AddSingleton<PA999ModeRouter>();          // ★ 3분기 자동 라우팅 (SP/SQL/SOP)
builder.Services.AddSingleton<PA999SpCatalogService>();    // ★ SP 카탈로그 실행 엔진 (Mode A)
builder.Services.AddHostedService<PA999CacheWarmupService>(); // ★ 서버 시작 시 캐시 예열
builder.Services.AddScoped<PA999MetaBatchService>();   // 배치 서비스 → Scoped

// ── 4. ASP.NET Core ───────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title       = "Bizentro.App.SV.PP.PA999S1_CKO087",
        Description = "UNIERP AI 챗봇 REST API 서버\nPA999M1 (WinForms UI) ↔ Claude API / MSSQL",
        Version     = "v1"
    });
    // XML 주석 포함 (Controllers 의 <summary> 표시)
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        c.IncludeXmlComments(xmlPath);
});

// ── 5. CORS (PA999M1 UNIERP 클라이언트 허용) ─────────────────
//   AllowedOrigins 는 appsettings.json 또는 appsettings.local.json 에서 관리
//   WinForms 클라이언트는 브라우저가 아니므로 CORS 적용 대상이 아니지만,
//   향후 웹 클라이언트 / Swagger UI 접근을 위해 명시적으로 설정
builder.Services.AddCors(opt =>
{
    opt.AddPolicy("PA999CorsPolicy", policy =>
    {
        var origins = builder.Configuration
                             .GetSection("AllowedOrigins")
                             .Get<string[]>();

        // 설정값이 없거나 비어있으면 localhost 기본 허용
        if (origins == null || origins.Length == 0)
            origins = new[] { "http://localhost", "http://localhost:5000" };

        policy.WithOrigins(origins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// ── 6. 로깅 ──────────────────────────────────────────────────
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// ══════════════════════════════════════════════════════════════
var app = builder.Build();
// ══════════════════════════════════════════════════════════════

// 포트폴리오 데모용: 항상 Swagger 활성화 (운영 배포 시 IsDevelopment() 조건으로 변경)
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "PA999S1 v1");
    c.RoutePrefix = "swagger";  // http://localhost:5000/swagger
});

app.UseCors("PA999CorsPolicy");
app.UseAuthorization();

// ── AdminQuery 엔드포인트 IP 화이트리스트 미들웨어 ──────────────
// /api/PA999/admin/* 경로는 localhost 또는 등록된 관리자 IP만 허용
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api/PA999/admin"))
    {
        var remoteIp = context.Connection.RemoteIpAddress?.ToString() ?? string.Empty;

        // IPv4-mapped IPv6 주소 변환 (::ffff:127.0.0.1 → 127.0.0.1)
        if (remoteIp.StartsWith("::ffff:"))
            remoteIp = remoteIp[7..];

        var allowedIps = builder.Configuration
                                .GetSection("PA999S1:AdminAllowedIPs")
                                .Get<string[]>()
                         ?? Array.Empty<string>();

        bool isAllowed = remoteIp == "::1"        // localhost IPv6
                      || remoteIp == "127.0.0.1"  // localhost IPv4
                      || allowedIps.Contains(remoteIp);

        if (!isAllowed)
        {
            var adminLogger = context.RequestServices
                .GetRequiredService<ILogger<Program>>();
            adminLogger.LogWarning(
                "[PA999][Security] AdminQuery 접근 차단 | IP={IP}", remoteIp);

            context.Response.StatusCode  = 403;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                "{\"error\":\"관리자 API는 허가된 IP에서만 접근할 수 있습니다.\"}");
            return;
        }
    }
    await next();
});

app.MapControllers();

// 시작 로그
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("═══════════════════════════════════════════════════");
logger.LogInformation(" Bizentro.App.SV.PP.PA999S1_CKO087 시작");
logger.LogInformation(" Claude Model  : {M}", options.Model);
logger.LogInformation(" ShowSql       : {S}", options.ShowSqlInResponse);
logger.LogInformation(" ChatLog       : PA999_CHAT_LOG (자동 저장)");
logger.LogInformation(" CacheWarmup   : 서버 시작 시 자동 예열 (테이블목록/스키마/마스터쿼리)");
logger.LogInformation(" RAG Embedding : {E}", string.IsNullOrWhiteSpace(options.OpenAIApiKey) ? "OFF (OpenAIApiKey 미설정)" : "ON (text-embedding-3-small)");
logger.LogInformation(" FeedbackAPI   : PATCH /api/PA999/log/{{logSeq}}/feedback");
logger.LogInformation(" ReviewAPI     : GET  /api/PA999/log/review");
logger.LogInformation(" PatternAPI    : POST /api/PA999/log/pattern");
logger.LogInformation(" Swagger UI    : http://localhost:{P}/swagger",
    builder.Configuration["ASPNETCORE_URLS"]?.Split(";")[0].Split(":").LastOrDefault() ?? "5000");
logger.LogInformation("═══════════════════════════════════════════════════");

app.Run();
