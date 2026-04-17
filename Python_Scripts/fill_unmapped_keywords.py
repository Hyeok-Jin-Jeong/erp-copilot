# -*- coding: utf-8 -*-
"""미매핑 컬럼의 KOREAN_NAME/KEYWORDS를 컬럼명 패턴 기반으로 자동 채움
SP 소스 주석 파싱에서 누락된 440건을 ERP 도메인 지식으로 보완"""
import pyodbc, re, sys, io

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')

CONN = "DRIVER={SQL Server};SERVER=172.31.3.59,11433;DATABASE=UNIERP60N;UID=SA;PWD=HC#erpsa202003s"

# ── 1) 완전 일치 매핑 (컬럼명 → 한글명) ──
EXACT_MAP = {
    # 리포트 헤더
    'COMPANY':          '회사명',
    'COMPANY2':         '회사명2',
    'SEARCHDT':         '조회기간',
    'TITLE':            '타이틀',
    # 검색 파라미터
    'SEARCH_DT_FROM':   '조회시작일',
    'SEARCH_DT_TO':     '조회종료일',
    'SEARCH_GUBUN_CD':  '조회구분코드',
    'TP_GUBUN_CD':      '유형구분코드',
    'TP_GUBUN_NM':      '유형구분명',
    'TP_GUBUN_CD_NM':   '유형구분명',
    'TX_REMARK11':      '비고',
    # 공통 코드/명칭
    'GUBUN_CD':         '구분코드',
    'GUBUN_NM':         '구분명',
    'GUBUN_CD_NM':      '구분코드명',
    'GUBUN_CD_NM2':     '구분코드명2',
    'GUBUN_CD_NM3':     '구분코드명3',
    'GUBUN_CD2':        '구분코드2',
    'GUBUN_CD3':        '구분코드3',
    'GUBUN_ORD':        '구분순서',
    'CD_GUBUN':         '코드구분',
    'CD_GUBUN_NM':      '코드구분명',
    'WC_GROUP_GUBUN_NM':'호기구분명',
    'WC_CD_CNT':        '작업장수',
    # 마스터 코드
    'PLANT_CD':         '공장코드',
    'WC_CD':            '작업장코드',
    'WC_GROUP_CD':      '호기구분코드',
    'ITEM_CD':          '품목코드',
    'ITEM_NM':          '품목명',
    'ITEM_NM_GUB':      '품목명구분',
    'CHILD_ITEM_CD':    '자품목코드',
    'CHILD_ITEM_NM':    '자품목명',
    'CHILD_BASE_UNIT':  '자품목기본단위',
    'BASIC_UNIT':       '기본단위',
    'LINE_NO':          '라인번호',
    'SORT_NO':          '정렬순서',
    'SEND_PLANT_CD':    '이동공장코드',
    'DATA_CNT':         '데이터건수',
    # 재고
    'BEF_JAEGO_QTY':    '기초재고',
    'CUR_JAEGO_QTY':    '기말재고',
    'IPGO_QTY':         '입고량',
    'CHULGO_QTY':       '출고량',
    'IPGO_UNDRY_QTY':   '입고미착량',
    # 생산
    'DAY_IPGO_QTY':     '일입고량',
    'MONTH_IPGO_QTY':   '월입고량',
    'YEAR_IPGO_QTY':    '연입고량',
    'DAY_CHULGO_QTY':   '일출고량',
    'MONTH_CHULGO_QTY': '월출고량',
    'YEAR_CHULGO_QTY':  '연출고량',
    'DAY_PROD_IPGO_QTY':'일생산입고량',
    'DAY_SALE_CHULGO_QTY':'일판매출고량',
    'MONTH_PROD_IPGO_QTY':'월생산입고량',
    'MONTH_SALE_CHULGO_QTY':'월판매출고량',
    'YEAR_IPGO_QTY':    '연입고량',
    'YEAR_CHULGO_QTY':  '연출고량',
    'MOVE_CHULGO_QTY':  '이동출고량',
    'SALE_CHULGO_QTY':  '판매출고량',
    'SOBI_CHULGO_QTY':  '소비출고량',
    'REAL_CHULGO_QTY':  '실출고량',
    'REAL_CHULGO_QTY_RATE': '실출고비율',
    'YEAR_REAL_CHULGO_QTY': '연실출고량',
    'YEAR_REAL_CHULGO_QTY_RATE': '연실출고비율',
    # 가동
    'FAC_WORK_TIME_HOUR':       '공장가동시간',
    'MONTH_FAC_WORK_TIME_HOUR': '월공장가동시간',
    'MONTH_FAC_WORK_RATE':      '월공장가동률',
    'YEAR_FAC_WORK_RATE':       '연공장가동률',
    'PROD_RESULT_RATE':         '생산실적비율',
    'TOT_STOP_CHA_TIME_HOUR':   '합계정지차시간',
    # 원단위
    'ONE_UNIT_QTY':             '원단위',
    'ONE_UNIT_QTY1':            '원단위1',
    'ONE_UNIT_RESULT_QTY':      '원단위실적',
    'ONE_UNIT_YEAR_RESULT_QTY': '연원단위실적',
    'KWH_ONE_UNIT_QTY':         '전력원단위',
    'FUEL_ONE_UNIT_QTY':        '연료원단위',
    'ONE_UNIT_CHULGO_QTY':      '원단위출고량',
    'TOT_KWH_ONE_UNIT_QTY':     '합계전력원단위',
    'TOT_FUEL_ONE_UNIT_QTY':    '합계연료원단위',
    'TOT_ONE_UNIT_RATE':        '합계원단위비율',
    'TOT_ADD_ONE_UNIT_RATE':    '합계첨가원단위비율',
    'TOT_YEAR_ONE_UNIT_RATE':   '연합계원단위비율',
    'TOT_ONE_UNIT_SOBI_QTY':    '합계원단위소비량',
    'TOT_ONE_UNIT_RM_QTY':      '합계원재료원단위',
    # 생산능력
    'DAY_BASE_PROD_QTY':        '일기준생산량',
    'DAY_PROD_CAPA':            '일생산능력',
    'MONTH_PROD_CAPA':          '월생산능력',
    'YEAR_PROD_CAPA':           '연생산능력',
    # 기타 합계
    'TOT_MONTH_QTY':            '월합계',
    'TOT_BASE_SOBI_QTY':        '합계기준소비량',
    'TOT_CHULGO_QTY':           '합계출고량',
    'TOT_SOBI_QTY_RM':          '합계소비량RM',
    'TOT_REPLACE_RATE':         '합계대체율',
    'TOT_BASE_SOBI_QTY_CM':     '합계기준소비량CM',
    # 호기별 대체율
    'RE_USED_RATE_KL01':        'KL1호대체율',
    'RE_USED_RATE_KL02':        'KL2호대체율',
    'RE_USED_RATE_KL03':        'KL3호대체율',
    'RE_USED_RATE_KL04':        'KL4호대체율',
    'RE_USED_RATE_KL05':        'KL5호대체율',
    'RE_USED_RATE_KL06':        'KL6호대체율',
    # @파라미터
    '@P_SEARCH_DATE':           '조회일자',
    '@P_TITLE':                 '타이틀',
}

# ── 2) 패턴 매핑 (정규식 → 한글명 생성 함수) ──
PATTERN_MAP = [
    # CHULGO_QTY_NN → N월출고량
    (r'^CHULGO_QTY_(\d+)$', lambda m: f'{int(m.group(1))}월출고량'),
]

# ── 3) 노이즈 키워드 (KEYWORDS 생성 시 제외) ──
NOISE = {'기간', '누계', '합계', '소계', '제외', '포함', '기준', '단위',
         '수량', '전체', '해당', '이상', '이하', '경우', '데이터',
         '회사명', '구분', '구분명', '구분코드', '타이틀명', '조회기간',
         '공란', '기초', '투입', '비고', '문서번호',
         '회사명2', '타이틀', '조회시작일', '조회종료일', '조회구분코드',
         '유형구분코드', '비고', '구분코드명', '구분코드명2', '구분코드명3',
         '구분코드2', '구분코드3', '코드구분', '코드구분명', '라인번호',
         '정렬순서', '기본단위', '구분순서', '데이터건수'}


def generate_keywords(korean_name):
    """한글명 → 검색 키워드 세트"""
    keywords = set()
    if not korean_name:
        return keywords

    # 호기 패턴 (KL1호, CM2호 등)
    equip_matches = re.findall(r'(?:KL|CM|RM|PK)\d+호', korean_name)
    keywords.update(equip_matches)

    # 한글 단어 추출 (2자 이상)
    korean_words = re.findall(r'[가-힣]{2,}', korean_name)
    for w in korean_words:
        if w not in NOISE and len(w) >= 2:
            keywords.add(w)

    # 복합 키워드 (인접 2단어 결합)
    clean = re.sub(r'[A-Za-z0-9/,_\-\.]', ' ', korean_name)
    parts = [w for w in clean.split() if len(w) >= 2 and re.match(r'^[가-힣]+$', w) and w not in NOISE]
    for i in range(len(parts) - 1):
        combined = parts[i] + parts[i+1]
        if len(combined) >= 4:
            keywords.add(combined)

    # 호기+한글 결합
    for eq in equip_matches:
        for kw in korean_words:
            if kw not in NOISE and len(kw) >= 2:
                keywords.add(eq + kw)

    return keywords


def resolve_column_name(col_name):
    """컬럼명 → 한글명 (EXACT → PATTERN 순)"""
    # 1) 완전 일치
    if col_name in EXACT_MAP:
        return EXACT_MAP[col_name]

    # 2) 패턴 매칭
    for pattern, func in PATTERN_MAP:
        m = re.match(pattern, col_name)
        if m:
            return func(m)

    return None


def main():
    conn = pyodbc.connect(CONN)
    cur = conn.cursor()

    # 미매핑 컬럼 조회
    cur.execute("""
        SELECT SP_ID, MULTISET_NO, COLUMN_NAME
        FROM PA999_SP_COLUMN_MAP (NOLOCK)
        WHERE (KEYWORDS IS NULL OR KEYWORDS = '')
        ORDER BY SP_ID, MULTISET_NO, COLUMN_NAME
    """)
    unmapped = cur.fetchall()
    print(f"미매핑 컬럼: {len(unmapped)}건")

    updated = 0
    skipped = []
    for sp_id, mset_no, col_name in unmapped:
        korean = resolve_column_name(col_name)
        if korean:
            kw_set = generate_keywords(korean)
            kw_str = ','.join(sorted(kw_set)) if kw_set else ''
            cur.execute("""
                UPDATE PA999_SP_COLUMN_MAP
                SET KOREAN_NAME = ?, KEYWORDS = ?
                WHERE SP_ID = ? AND MULTISET_NO = ? AND COLUMN_NAME = ?
                  AND (KOREAN_NAME IS NULL OR KOREAN_NAME = '')
            """, korean, kw_str, sp_id, mset_no, col_name)
            updated += cur.rowcount
        else:
            skipped.append((sp_id, mset_no, col_name))

    conn.commit()
    print(f"\n업데이트: {updated}건")
    print(f"미해결: {len(skipped)}건")

    if skipped:
        print(f"\n=== 미해결 컬럼 목록 ===")
        for sp_id, mset_no, col_name in skipped:
            print(f"  {sp_id} / MSET{mset_no} / {col_name}")

    # 최종 현황
    cur.execute("""
        SELECT SP_ID,
               COUNT(*) AS TOTAL,
               SUM(CASE WHEN KEYWORDS IS NOT NULL AND KEYWORDS != '' THEN 1 ELSE 0 END) AS HAS_KW,
               SUM(CASE WHEN KOREAN_NAME IS NOT NULL AND KOREAN_NAME != '' THEN 1 ELSE 0 END) AS HAS_KR
        FROM PA999_SP_COLUMN_MAP (NOLOCK)
        GROUP BY SP_ID ORDER BY SP_ID
    """)
    print(f"\n=== 최종 현황 ===")
    for r in cur.fetchall():
        kr_pct = (r[3] / r[1] * 100) if r[1] > 0 else 0
        kw_pct = (r[2] / r[1] * 100) if r[1] > 0 else 0
        print(f"  {r[0]:25s}: 한글명 {r[3]:3d}/{r[1]:3d} ({kr_pct:.0f}%), 키워드 {r[2]:3d}/{r[1]:3d} ({kw_pct:.0f}%)")

    conn.close()


if __name__ == '__main__':
    main()
