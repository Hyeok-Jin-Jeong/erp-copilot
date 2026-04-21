using System.Net.Http.Headers;
using Bizentro.App.SV.PP.PA999S1_CKO087.Models;
using Bizentro.App.SV.PP.PA999S1_CKO087.Services;

// ══════════════════════════════════════════════════════════════
//  Bizentro.App.SV.PP.PA999S1_CKO087
//  UNIERP AI 챗봇 REST API 서버
//  PA999M1 (UNIERP WinForms UI) ↔ 이 서버 ↔ Claude API / MSSQL
// ══════════════════════════════════════════════════════════════

// ── Railway / 클라우드 환경 감지 ──────────────────────────────
var railwayEnv  = Environment.GetEnvironmentVariable("RAILWAY_ENVIRONMENT")
               ?? Environment.GetEnvironmentVariable("RAILWAY_PROJECT_ID");
var isRailway   = !string.IsNullOrEmpty(railwayEnv)
               || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("RAILWAY_SERVICE_ID"));

// ── PORT 자동 감지 (Railway는 $PORT 환경변수로 포트 주입) ──────
var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";

var builder = WebApplication.CreateBuilder(args);

// Railway 환경에서는 appsettings.json의 "Urls" 보다 PORT 우선 적용
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// ── 1. 설정 바인딩 ────────────────────────────────────────────
builder.Services.Configure<PA999Options>(
    builder.Configuration.GetSection(PA999Options.SectionName));

var options = builder.Configuration
                     .GetSection(PA999Options.SectionName)
                     .Get<PA999Options>()
              ?? new PA999Options();   // null 대신 기본값 사용 (크래시 방지)

// ★ 포트폴리오/Railway 배포 시 API Key 미설정이어도 서버는 기동
//   실제 채팅 요청 시 ChatbotService 내부에서 오류 반환
if (string.IsNullOrWhiteSpace(options.AnthropicApiKey))
    Console.WriteLine("[WARNING] AnthropicApiKey 미설정 — 환경변수 PA999S1__AnthropicApiKey 를 확인하세요. 채팅 기능 비활성화.");
else
    Console.WriteLine("[INFO] AnthropicApiKey 설정 확인됨.");

// ── 2. Anthropic HttpClient ───────────────────────────────────
builder.Services.AddHttpClient("AnthropicClient", client =>
{
    client.BaseAddress = new Uri("https://api.anthropic.com");
    if (!string.IsNullOrWhiteSpace(options.AnthropicApiKey))
    {
        client.DefaultRequestHeaders.Add("x-api-key",         options.AnthropicApiKey);
        client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
    }
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
builder.Services.AddSingleton<PA999EmbeddingService>();
builder.Services.AddSingleton<PA999SchemaService>();
builder.Services.AddSingleton<PA999DbService>();
builder.Services.AddSingleton<PA999ChatLogService>();
builder.Services.AddSingleton<PA999QueryCacheService>();
builder.Services.AddSingleton<PA999ChtbotService>();
builder.Services.AddSingleton<PA999ModeRouter>();
builder.Services.AddSingleton<PA999SpCatalogService>();
builder.Services.AddScoped<PA999MetaBatchService>();

// ★ Railway 환경에서는 CacheWarmupService(DB 의존) 등록 스킵
//   로컬 MSSQL이 없으면 warmup이 에러를 내며 지연되므로 생략
if (!isRailway)
{
    builder.Services.AddHostedService<PA999CacheWarmupService>();
    Console.WriteLine("[INFO] CacheWarmupService 등록 (로컬/온프레미스 환경)");
}
else
{
    Console.WriteLine("[INFO] CacheWarmupService 스킵 (Railway 환경 — DB 미접근)");
}

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
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        c.IncludeXmlComments(xmlPath);
});

// ── 5. CORS ───────────────────────────────────────────────────
builder.Services.AddCors(opt =>
{
    opt.AddPolicy("PA999CorsPolicy", policy =>
    {
        var origins = builder.Configuration
                             .GetSection("AllowedOrigins")
                             .Get<string[]>();

        if (isRailway || origins == null || origins.Length == 0)
        {
            // Railway/포트폴리오 데모: 모든 오리진 허용 (Vercel URL 사전 등록 불필요)
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        }
        else
        {
            policy.WithOrigins(origins)
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
        }
    });
});

// ── 6. 로깅 ──────────────────────────────────────────────────
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// ══════════════════════════════════════════════════════════════
var app = builder.Build();
// ══════════════════════════════════════════════════════════════

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "PA999S1 v1");
    c.RoutePrefix = "swagger";
});

app.UseCors("PA999CorsPolicy");
app.UseAuthorization();

// ── AdminQuery IP 화이트리스트 미들웨어 ──────────────────────
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api/PA999/admin"))
    {
        var remoteIp = context.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
        if (remoteIp.StartsWith("::ffff:"))
            remoteIp = remoteIp[7..];

        var allowedIps = builder.Configuration
                                .GetSection("PA999S1:AdminAllowedIPs")
                                .Get<string[]>()
                         ?? Array.Empty<string>();

        // admin/migrate + admin/netdiag: X-Migration-Key 헤더로 IP 우회 허용
        var migrationKey = builder.Configuration["PA999S1:MigrationKey"] ?? string.Empty;
        bool isMigrationPath = context.Request.Path.StartsWithSegments("/api/PA999/admin/migrate")
                            || context.Request.Path.StartsWithSegments("/api/PA999/admin/netdiag")
                            || context.Request.Path.StartsWithSegments("/api/PA999/admin/query");
        bool hasMigrationKey = !string.IsNullOrWhiteSpace(migrationKey)
            && isMigrationPath
            && context.Request.Headers.TryGetValue("X-Migration-Key", out var keyHeader)
            && keyHeader.ToString() == migrationKey;

        bool isAllowed = remoteIp is "::1" or "127.0.0.1"
                      || allowedIps.Contains(remoteIp)
                      || hasMigrationKey;

        if (!isAllowed)
        {
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
logger.LogInformation(" 환경           : {E}", isRailway ? "Railway (클라우드)" : "로컬/온프레미스");
logger.LogInformation(" 포트           : {P}", port);
logger.LogInformation(" Claude Model  : {M}", options.Model);
logger.LogInformation(" API Key       : {K}", string.IsNullOrWhiteSpace(options.AnthropicApiKey) ? "❌ 미설정" : "✅ 설정됨");
logger.LogInformation(" RAG Embedding : {E}", string.IsNullOrWhiteSpace(options.OpenAIApiKey) ? "OFF" : "ON");
logger.LogInformation(" CacheWarmup   : {W}", isRailway ? "스킵 (Railway)" : "활성화");
logger.LogInformation(" Swagger UI    : http://0.0.0.0:{P}/swagger", port);
logger.LogInformation("═══════════════════════════════════════════════════");

app.Run();
