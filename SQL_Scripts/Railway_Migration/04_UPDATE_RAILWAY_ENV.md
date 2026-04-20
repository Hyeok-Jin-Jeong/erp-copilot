# Railway 환경변수 업데이트 가이드

SQL 스크립트 실행 완료 후 Railway 대시보드에서 아래 환경변수를 설정하세요.

## Railway 대시보드 → erp-copilot-api → Variables

| 환경변수 이름 | 값 |
|---|---|
| `PA999S1__ConnectionString` | `Server=nozomi.proxy.rlwy.net,36504;Database=PA999_DEMO;User Id=SA;Password=PA999Demo@2026!;TrustServerCertificate=True;Connect Timeout=30;` |

## 설정 후 Redeploy

Variables 저장 → Railway가 자동으로 재배포합니다.

## 헬스체크 확인

```
curl https://erp-copilot-api-production.up.railway.app/api/PA999/health
```

→ `"dbStatus": "configured (not probed)"` 에서 실제 채팅 후 DB 연결 확인
