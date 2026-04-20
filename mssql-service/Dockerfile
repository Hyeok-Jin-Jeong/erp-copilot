# ══════════════════════════════════════════════════════════════
# PA999_DEMO — Railway MSSQL 서비스
# SQL Server 2022 + 자동 DB 초기화
# ══════════════════════════════════════════════════════════════
FROM mcr.microsoft.com/mssql/server:2022-latest

USER root

# 초기화 스크립트 복사
COPY init/ /init/
RUN chmod +x /init/entrypoint.sh

USER mssql

ENV ACCEPT_EULA=Y
ENV MSSQL_PID=Express
ENV MSSQL_COLLATION=Korean_Wansung_CI_AS

EXPOSE 1433

ENTRYPOINT ["/bin/bash", "/init/entrypoint.sh"]
