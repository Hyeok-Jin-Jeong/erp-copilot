# -*- coding: utf-8 -*-
"""PA999_SP_CATALOG.KEYWORD_LIST 재구축
각 SP 소스코드를 분석하여 실제 비즈니스 키워드를 추출하고 업데이트

분석 요소:
1. SP 소스 한글 주석 (-- 한글 주석)
2. SP_DESC의 핵심 단어
3. 참조 테이블 → TABLE_META의 키워드 연계
4. SP_COLUMN_MAP의 KEYWORDS (DIRECT_KEYWORDS)
5. 공장 코드/명칭 매핑
"""
import pyodbc, re, sys, io
from collections import Counter

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')

CONN = "DRIVER={SQL Server};SERVER=172.31.3.59,11433;DATABASE=UNIERP60N;UID=SA;PWD=HC#erpsa202003s"

# ── 공장 코드 → 공장명 매핑 ──
PLANT_MAP = {
    'P001': '단양공장', 'P031': '영월공장', 'P032': '삼곡공장',
    'P013': '포항공장', 'P023': '평택공장', 'P033': '당진공장',
    'P003': '대구공장', 'P004': '부산공장', 'P006': '청주공장',
    'P007': '성남공장', 'P008': '대전공장', 'P011': '김해공장',
    'P014': '서대구공장', 'P020': '서인천공장', 'P022': '화성공장',
    'P025': '부천레미콘', 'P012': '인천공장',
}

# 공장 유형 분류
PLANT_TYPE = {
    'P001': '반복제조', 'P031': '반복제조', 'P032': '반복제조',
    'P013': '지역공장', 'P023': '지역공장', 'P033': '지역공장',
    'P003': '레미콘', 'P004': '레미콘', 'P006': '레미콘',
    'P007': '레미콘', 'P008': '레미콘', 'P011': '레미콘',
    'P014': '레미콘', 'P020': '레미콘', 'P022': '레미콘',
    'P025': '레미콘', 'P012': '레미탈',
}

# ── 노이즈 (제거할 범용/기술 키워드) ──
NOISE = {
    # 범용
    '회사명', '구분', '구분명', '구분코드', '타이틀명', '조회기간', '타이틀',
    '공란', '비고', '문서번호', '데이터', '조회', '처리', '결과',
    '수량', '전체', '해당', '이상', '이하', '경우', '포함', '제외',
    '누계', '합계', '소계', '단위', '코드', '코드명', '코드값',
    '기간', '기준', '기초', '투입', '수량',
    # 기술 용어
    '파라미터', '입력', '출력', '리턴', '반환', '선언', '변수',
    '커서', '임시', '테이블', '인서트', '업데이트', '딜리트',
    '시작', '종료', '조건', '카운트', '합산', '인덱스',
    '프로시저', '프로시저명', '프로그램', '개발자', '세팅',
    '호출예', '호출', '생성일', '생성', '삭제', '수정',
    '내용', '경우는', '기준으로', '해당월의', '마지막',
    '한일현대', '김봉수',  # 개인명/회사명
    '라인', '로우', '컬럼', '필드', '오브젝트',
    '셀렉트', '조인', '웨어', '그룹바이', '오더바이',
    # 주석 파편 (동사/조사/서술형)
    '가져옴', '가져오기', '구하지', '떄문에', '나머지는', '다시한번',
    '기초수량은', '까지의', '발생한', '발생함', '보조재료가', '부분이',
    '구분값으로', '구분이', '없거나', '동일하게', '년계획은', '공장은',
    '가동시간은', '같으면', '간주한다', '건식수량에만',
    '게산하지', '넘버', '데이타', '동일공장', '동일사업장',
    '라우팅', '미삭제', '사용불가', '공장정보', '공장품목정보',
    '공장입고분', '교환공장입고분', '관계사매입', '고화재',
    # 지나치게 범용 (10개 이상 SP에 공통)
    '공장코드', '시작일',
}

# ── SP별 수동 보정 (SP_DESC에서 확인된 핵심 도메인 키워드) ──
SP_DOMAIN_OVERRIDE = {
    'USP_PP_PC551Q1_CKO087': {
        'type': '조회', 'factory_type': '반복제조',
        'plants': ['P001'],
        'domain': ['시멘트', '반복제조', '생산일보', '단양', '단양공장'],
    },
    'USP_PP_PC551Q1_CKO087_R': {
        'type': '리포트', 'factory_type': '반복제조',
        'plants': ['P001'],
        'domain': ['시멘트', '반복제조', '생산일보', '생산일보리포트', '단양', '단양공장', '단양생산일보'],
    },
    'USP_PP_PC581Q1_CKO087': {
        'type': '조회', 'factory_type': '반복제조',
        'plants': ['P031', 'P032'],
        'domain': ['시멘트', '반복제조', '생산일보', '영월', '영월공장', '삼곡', '삼곡공장'],
    },
    'USP_PP_PC581Q1_CKO087_R': {
        'type': '리포트', 'factory_type': '반복제조',
        'plants': ['P031', 'P032'],
        'domain': ['시멘트', '반복제조', '생산일보', '생산일보리포트', '영월', '영월공장', '삼곡', '삼곡공장', '영월생산일보', '삼곡생산일보'],
    },
    'USP_PP_PC781Q1_CKO087': {
        'type': '조회', 'factory_type': '반복제조',
        'plants': ['P032'],
        'domain': ['건식골재', '골재', '부원료', '생산일보', '삼곡', '삼곡공장'],
    },
    'USP_PP_PC781Q1_CKO087_R': {
        'type': '리포트', 'factory_type': '반복제조',
        'plants': ['P032'],
        'domain': ['건식골재', '골재', '부원료', '생산일보', '생산일보리포트', '삼곡', '삼곡공장', '부원료생산일보'],
    },
    'USP_PP_PC513Q1_CKO087_R': {
        'type': '리포트', 'factory_type': '반복제조',
        'plants': ['P001'],
        'domain': ['생산기간보', '기간보', '생산요약', '단양', '단양공장'],
    },
    'USP_PP_PD251Q1_CKO087': {
        'type': '조회', 'factory_type': '지역공장',
        'plants': ['P013', 'P023'],
        'domain': ['지역공장', '생산일보', '포항', '포항공장', '평택', '평택공장', '시멘트'],
    },
    'USP_PP_PD251Q1_CKO087_R': {
        'type': '리포트', 'factory_type': '지역공장',
        'plants': ['P013', 'P023'],
        'domain': ['지역공장', '생산일보', '생산일보리포트', '포항', '포항공장', '평택', '평택공장', '포항생산일보', '평택생산일보', '시멘트'],
    },
    'USP_PP_PD281Q1_CKO087': {
        'type': '조회', 'factory_type': '지역공장',
        'plants': ['P033'],
        'domain': ['지역공장', '생산월보', '당진', '당진공장', '시멘트'],
    },
    'USP_PP_PD281Q1_CKO087_R': {
        'type': '리포트', 'factory_type': '지역공장',
        'plants': ['P033'],
        'domain': ['지역공장', '생산월보', '생산월보리포트', '당진', '당진공장', '당진생산월보', '시멘트'],
    },
    'USP_PP_PJ251Q1_CKO087_R': {
        'type': '리포트', 'factory_type': '레미콘',
        'plants': ['P003','P004','P006','P007','P008','P011','P014','P020','P022','P025'],
        'domain': ['레미콘', '레미콘공장', '레미콘생산일보', '생산일보', '생산일보리포트'],
    },
    'USP_PP_PK251Q1_CKO087_R': {
        'type': '리포트', 'factory_type': '레미탈',
        'plants': ['P012'],
        'domain': ['레미탈', '레미탈공장', '레미탈생산일보', '생산일보', '생산일보리포트', '인천', '인천공장'],
    },
    'USP_PP_CLOSECHECK_CKO087': {
        'type': '조회', 'factory_type': None,
        'plants': [],
        'domain': ['마감', '마감체크', '마감확인', '마감가능', '일마감', '수불마감', '마감여부', '생산마감'],
    },
    'USP_PP_CLOSECHECK_CKO087_INOUT': {
        'type': '조회', 'factory_type': None,
        'plants': [],
        'domain': ['마감', '수불마감', '일마감', '마감현황', '마감상태', '입출고', '수불현황'],
    },
    'USP_PP_PD210Q1_CKO087_Q': {
        'type': '조회', 'factory_type': '지역공장',
        'plants': [],
        'domain': ['생산현황', '작업지시', '생산오더', '오더', '오더조회', '지역공장'],
    },
    'USP_PP_PA110M1_CKO087_Q': {
        'type': '조회', 'factory_type': None,
        'plants': [],
        'domain': ['공통코드', '코드마스터', 'PP코드', '코드조회'],
    },
}


def get_sp_source(conn, sp_name):
    """SP 소스코드 조회"""
    cur = conn.cursor()
    cur.execute(f"SELECT TEXT FROM syscomments WHERE id = OBJECT_ID('{sp_name}') ORDER BY colid")
    rows = cur.fetchall()
    return ''.join([r[0] for r in rows]) if rows else ''


def extract_korean_from_source(source):
    """SP 소스에서 비즈니스 한글 키워드만 추출 (주석의 AS 별칭 위주)"""
    keyword_counter = Counter()

    # 1) AS COLUMN_NAME -- 한글 주석 (가장 신뢰도 높은 소스)
    as_comments = re.findall(r'\bAS\s+[A-Z][A-Z0-9_]+\s*,?\s*--\s*(.+?)(?:\n|$)', source, re.IGNORECASE)
    for c in as_comments:
        # 괄호/콜론 이후 제거
        c = re.sub(r'\s*\(.*$', '', c)
        c = re.sub(r'\s*:.*$', '', c)
        words = re.findall(r'[가-힣]{2,}', c)
        for w in words:
            if w not in NOISE and len(w) >= 2:
                keyword_counter[w] += 2  # 가중치 2

    # 2) N'한글' 리터럴 (업무 용어가 많음)
    literals = re.findall(r"N'([가-힣][가-힣\s]{1,15})'", source)
    for lit in literals:
        clean = lit.strip()
        if clean not in NOISE and 2 <= len(clean) <= 10:
            keyword_counter[clean] += 1

    # 3) 일반 주석의 핵심 단어 (빈도 기반)
    general_comments = re.findall(r'--\s*(.+?)(?:\n|$)', source)
    for c in general_comments:
        words = re.findall(r'[가-힣]{3,}', c)  # 3자 이상만
        for w in words:
            if w not in NOISE and len(w) >= 3:
                keyword_counter[w] += 1

    # 빈도 2 이상 또는 AS 주석에서 나온 것만 반환
    return {kw for kw, cnt in keyword_counter.items() if cnt >= 2}


def extract_tables_from_source(source):
    """SP 소스에서 참조 테이블명 추출"""
    tables = set()
    # FROM/JOIN 뒤의 테이블명
    pattern = re.findall(r'(?:FROM|JOIN)\s+([A-Z][A-Z0-9_]+)', source, re.IGNORECASE)
    for t in pattern:
        t = t.upper()
        if t.startswith(('P_', 'B_', 'Z_')) and 'TEMP' not in t:
            tables.add(t)
    return tables


def extract_equipment_keywords(source):
    """호기/설비 관련 키워드 추출"""
    keywords = set()
    # KL1호, KL2호, CM1호, RM1호, PK1호 패턴
    for prefix, name in [('KL', 'KL'), ('CM', 'CM'), ('RM', 'RM'), ('PK', 'PK')]:
        matches = re.findall(rf"['\"]?{prefix}(\d+)['\"]?", source, re.IGNORECASE)
        for num in set(matches):
            keywords.add(f'{name}{num}호')
    # 호기 그룹 관련
    if re.search(r'WC_GROUP', source, re.IGNORECASE):
        keywords.add('호기')
        keywords.add('호기별')
    return keywords


def get_column_map_keywords(conn, sp_id):
    """SP_COLUMN_MAP에서 해당 SP의 고유 키워드 추출"""
    cur = conn.cursor()
    cur.execute("""
        SELECT DISTINCT TRIM(value) AS kw
        FROM PA999_SP_COLUMN_MAP (NOLOCK)
        CROSS APPLY STRING_SPLIT(KEYWORDS, ',')
        WHERE SP_ID = ?
          AND USE_YN = 'Y'
          AND NULLIF(TRIM(KEYWORDS), '') IS NOT NULL
          AND NULLIF(TRIM(value), '') IS NOT NULL
    """, sp_id)
    return {r[0] for r in cur.fetchall()}


def extract_plant_codes_from_source(source):
    """SP 소스에서 참조되는 공장 코드 추출"""
    codes = set()
    matches = re.findall(r"['\"]P(\d{3})['\"]", source)
    for m in matches:
        code = f'P{m}'
        if code in PLANT_MAP:
            codes.add(code)
    return codes


def build_keyword_list(sp_name, sp_desc, source_keywords, equip_keywords,
                       col_map_keywords, plant_codes, domain_override):
    """최종 KEYWORD_LIST 구성 — NVARCHAR(500) 제한 준수

    우선순위:
    Tier 1: 도메인 핵심 키워드 (수동 보정) — 반드시 포함
    Tier 2: 호기/설비 키워드 (KL1호 등) — 현업이 자주 검색
    Tier 3: SP 소스 한글 키워드 (빈도 2이상)
    Tier 4: 컬럼맵 키워드 (DIRECT_KEYWORDS 소스)
    """
    MAX_LEN = 490  # 여유 10자

    tier1 = set()  # 도메인 핵심
    tier2 = set()  # 호기/설비
    tier3 = set()  # 소스 한글
    tier4 = set()  # 컬럼맵

    # Tier 1: 도메인 키워드
    if domain_override:
        tier1.update(domain_override.get('domain', []))
        for pc in domain_override.get('plants', []):
            tier1.add(pc)
            if pc in PLANT_MAP:
                tier1.add(PLANT_MAP[pc])
                short = PLANT_MAP[pc].replace('공장', '').replace('레미콘', '')
                if short and len(short) >= 2:
                    tier1.add(short)

    # Tier 2: 호기/설비
    tier2.update(equip_keywords)

    # Tier 3: 소스 한글 (노이즈 필터)
    for kw in source_keywords:
        if kw not in NOISE and len(kw) >= 2:
            tier3.add(kw)

    # Tier 4: 컬럼맵 (노이즈 필터, 짧은 것 제외)
    for kw in col_map_keywords:
        if kw not in NOISE and len(kw) >= 2:
            tier4.add(kw)

    # 소스 공장 코드 → Tier 1에 추가
    for pc in plant_codes:
        tier1.add(pc)
        if pc in PLANT_MAP:
            tier1.add(PLANT_MAP[pc])

    # 우선순위별 조립 (MAX_LEN 내에서)
    result = []
    used = set()

    def add_tier(tier_set):
        for kw in sorted(tier_set):
            if kw in used:
                continue
            candidate = ','.join(result + [kw]) if result else kw
            if len(candidate) <= MAX_LEN:
                result.append(kw)
                used.add(kw)

    add_tier(tier1)
    add_tier(tier2)
    add_tier(tier3)
    add_tier(tier4)

    return result


def main():
    conn = pyodbc.connect(CONN)
    cur = conn.cursor()

    # SP 목록 조회
    cur.execute("SELECT SP_ID, SP_NAME, SP_DESC FROM PA999_SP_CATALOG (NOLOCK) ORDER BY SP_ID")
    sps = cur.fetchall()

    print(f"=" * 70)
    print(f" PA999_SP_CATALOG KEYWORD_LIST 재구축 ({len(sps)}개 SP)")
    print(f"=" * 70)

    for sp_id, sp_name, sp_desc in sps:
        print(f"\n{'=' * 60}")
        print(f" [{sp_id}] {sp_name}")
        print(f" DESC: {sp_desc}")
        print(f"{'=' * 60}")

        # 1) SP 소스코드 읽기
        source = get_sp_source(conn, sp_name)
        if not source:
            print(f"  ⚠ 소스코드 없음 — SKIP")
            continue
        print(f"  소스 길이: {len(source)} chars")

        # 2) 한글 키워드 추출
        source_kw = extract_korean_from_source(source)
        print(f"  소스 한글: {len(source_kw)}개")

        # 3) 호기/설비 키워드
        equip_kw = extract_equipment_keywords(source)
        print(f"  호기/설비: {len(equip_kw)}개 → {equip_kw}")

        # 4) SP_COLUMN_MAP 키워드
        # SP_ID mapping: SP_NAME에서 'USP_PP_' 제거
        col_map_sp_id = sp_name.replace('USP_PP_', '')
        col_map_kw = get_column_map_keywords(conn, col_map_sp_id)
        print(f"  컬럼맵 키워드: {len(col_map_kw)}개")

        # 5) 공장 코드 추출
        plant_codes = extract_plant_codes_from_source(source)
        print(f"  공장 코드: {plant_codes}")

        # 6) 도메인 오버라이드
        domain = SP_DOMAIN_OVERRIDE.get(sp_name, {})

        # 7) 최종 KEYWORD_LIST 구성
        kw_list = build_keyword_list(
            sp_name, sp_desc, source_kw, equip_kw,
            col_map_kw, plant_codes, domain
        )
        kw_str = ','.join(kw_list)
        print(f"  최종 키워드: {len(kw_list)}개")
        print(f"  → {kw_str[:200]}{'...' if len(kw_str) > 200 else ''}")

        # 8) UPDATE
        cur.execute("UPDATE PA999_SP_CATALOG SET KEYWORD_LIST = ? WHERE SP_ID = ?", kw_str, sp_id)

    conn.commit()

    # 최종 결과 출력
    print(f"\n{'=' * 70}")
    print(f" 최종 결과")
    print(f"{'=' * 70}")
    cur.execute("SELECT SP_ID, SP_NAME, LEN(KEYWORD_LIST) AS KW_LEN, KEYWORD_LIST FROM PA999_SP_CATALOG (NOLOCK) ORDER BY SP_ID")
    for r in cur.fetchall():
        print(f"\n  [{r[0]:2d}] {r[1]}")
        print(f"       len={r[2]}, keywords={r[3][:150]}...")

    conn.close()
    print(f"\n완료!")


if __name__ == '__main__':
    main()
