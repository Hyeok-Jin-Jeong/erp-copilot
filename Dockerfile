# ══════════════════════════════════════════════════════════════
#  UNIERP AI 챗봇 백엔드 - Dockerfile (Railway 배포용)
#  PA999S1_Server: ASP.NET Core 8.0 REST API
# ══════════════════════════════════════════════════════════════

# ── Stage 1: Build ────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# csproj 먼저 복사 → 레이어 캐시 활용 (소스 변경 시 restore 생략)
COPY PA999S1_Server/PA999S1_Server.csproj PA999S1_Server/
RUN dotnet restore PA999S1_Server/PA999S1_Server.csproj

# 전체 소스 복사 후 publish
COPY PA999S1_Server/ PA999S1_Server/
RUN dotnet publish PA999S1_Server/PA999S1_Server.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# ── Stage 2: Runtime ──────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# 빌드 결과물 복사
COPY --from=build /app/publish .

# Railway가 $PORT 환경변수로 포트 주입 (Program.cs에서 자동 처리)
EXPOSE 5000

ENTRYPOINT ["dotnet", "PA999S1_Server.dll"]
