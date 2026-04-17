# -*- coding: utf-8 -*-
"""SP 파라미터 바인딩 + 재질문 로직 테스트 스크립트
서버 실행 후: python test_sp_param_binding.py"""
import urllib.request, json, sys, io, time
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')

def ask(q, uid, plant='P001'):
    data = json.dumps({'question': q, 'userId': uid, 'plantCd': plant}).encode('utf-8')
    req = urllib.request.Request('http://localhost:5000/api/PA999/ask', data=data,
          headers={'Content-Type': 'application/json'})
    with urllib.request.urlopen(req, timeout=180) as resp:
        return json.loads(resp.read().decode('utf-8'))

tests = [
    # (질문, userId, plantCd, 기대모드, 설명)
    ('단양공장 생산일보', 'T_A', 'P001', 'SP_REQUERY', 'A: 날짜 누락 → 재질문'),
    ('단양공장 2026년 3월 생산일보', 'T_B', 'P001', 'SP', 'B: 날짜 지정 → SP 실행'),
    ('영월공장 2026년 3월 생산일보', 'T_C', 'P031', 'SP', 'C: 영월+날짜 → SP 실행'),
    ('이번 달 단양공장 작업장별 생산량', 'T_D', 'P001', 'SQL', 'D: SQL 모드 확인'),
    ('레미콘 생산일보 대구공장 2025년 10월', 'T_E', 'P003', 'SP', 'E: 레미콘 SP'),
]

print("=" * 60)
print(" SP 파라미터 바인딩 테스트")
print("=" * 60)

for q, uid, plant, expected, desc in tests:
    print(f'\n=== {desc} ===')
    print(f'  Q: "{q}"')
    try:
        body = ask(q, uid, plant)
        mode = body.get('processMode', '?')
        err = body.get('isError', False)
        grid = body.get('gridData', None)
        cnt = len(grid) if grid else 0
        answer = str(body.get('answer', ''))

        if err:
            print(f'  ❌ API Error')
            continue

        is_requery = '추가 정보' in answer
        actual = 'SP_REQUERY' if is_requery else mode

        ok = '✅' if actual == expected or (expected == 'SP' and mode == 'SP' and cnt > 0) else '⚠️'
        print(f'  {ok} processMode={mode} | gridData={cnt}건 | expected={expected}')
        if cnt > 0:
            tables = set(r.get('TABLE_NO', '?') for r in grid)
            print(f'     MULTISET: {len(tables)}개 섹션')
        if is_requery:
            print(f'     재질문: {answer[:150]}...')
    except Exception as e:
        print(f'  ❌ Exception: {e}')

    time.sleep(2)

print("\n" + "=" * 60)
print(" 테스트 완료")
print("=" * 60)
