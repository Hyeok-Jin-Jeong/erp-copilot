using Bizentro.App.SV.PP.PA999S1_CKO087.Models;

namespace Bizentro.App.SV.PP.PA999S1_CKO087.Services
{
    /// <summary>
    /// DB 연결 불가 시 포트폴리오 데모용 하드코딩 데이터 제공
    /// Railway 크로스-프로젝트 네트워크 제한으로 MSSQL 접속 불가할 때 사용
    /// </summary>
    public class PA999DemoDataService
    {
        // ── PA999_TABLE_META 하드코딩 ──────────────────────────────
        public static readonly List<Dictionary<string, object?>> TableMeta = new()
        {
            new() { ["TABLE_NM"] = "B_PLANT",            ["TABLE_DESC"] = "공장 마스터 — 레미콘/시멘트/레미탈 공장 목록, 지역, 사업영역",  ["KEYWORD_LIST"] = "공장,plant,공장코드,공장명,지역,레미콘,시멘트" },
            new() { ["TABLE_NM"] = "B_ITEM",             ["TABLE_DESC"] = "품목 마스터 — 생산/판매 품목 목록, 단위",                           ["KEYWORD_LIST"] = "품목,item,품목코드,품목명,단위,레미콘,시멘트" },
            new() { ["TABLE_NM"] = "A_ACCT",             ["TABLE_DESC"] = "계정과목 마스터 — 매출/원가/비용 계정",                             ["KEYWORD_LIST"] = "계정,account,계정과목,매출,원가,비용" },
            new() { ["TABLE_NM"] = "P_PROD_DAILY_HDR",  ["TABLE_DESC"] = "일별 생산 실적 헤더 — 공장별/일별 생산량, 반복제조/주문제조 구분",  ["KEYWORD_LIST"] = "생산,production,생산량,생산실적,일별생산,반복제조,주문제조,생산일보" },
            new() { ["TABLE_NM"] = "PA999_CHAT_LOG",     ["TABLE_DESC"] = "AI 챗봇 질의 로그 — 사용자 질문, AI 응답, 생성 SQL, 평가 점수",    ["KEYWORD_LIST"] = "채팅로그,질의,응답,피드백" },
            new() { ["TABLE_NM"] = "PA999_FEEDBACK_PATTERN", ["TABLE_DESC"] = "피드백 교정 패턴 — AI SQL 오류 교정 이력, RAG 임베딩",          ["KEYWORD_LIST"] = "피드백,패턴,교정,임베딩" },
            new() { ["TABLE_NM"] = "PA999_TABLE_META",   ["TABLE_DESC"] = "AI 테이블 메타 — 테이블 설명, 키워드 목록 (Step1 테이블 식별용)",   ["KEYWORD_LIST"] = "테이블메타,메타데이터" },
        };

        // ── B_PLANT 하드코딩 ──────────────────────────────────────
        public static readonly PA999QueryResult PlantList = BuildResult(
            new[] { "PLANT_CD", "PLANT_NM", "PLANT_TYPE", "BIZ_AREA_CD", "REGION_NM", "USE_YN" },
            new object?[][]
            {
                new object?[] { "P001", "단양공장",   "CEMENT",  "BA01", "충북", "Y" },
                new object?[] { "P003", "대구공장",   "REMICON", "BA02", "대구", "Y" },
                new object?[] { "P004", "부산공장",   "REMICON", "BA02", "부산", "Y" },
                new object?[] { "P006", "청주공장",   "REMICON", "BA01", "충북", "Y" },
                new object?[] { "P007", "성남공장",   "REMICON", "BA03", "경기", "Y" },
                new object?[] { "P008", "대전공장",   "REMICON", "BA01", "대전", "Y" },
                new object?[] { "P011", "김해공장",   "REMICON", "BA02", "경남", "Y" },
                new object?[] { "P014", "서대구공장", "REMICON", "BA02", "대구", "Y" },
                new object?[] { "P020", "서인천공장", "REMICON", "BA03", "인천", "Y" },
                new object?[] { "P022", "화성공장",   "REMICON", "BA03", "경기", "Y" },
                new object?[] { "P025", "부천공장",   "REMICON", "BA03", "경기", "Y" },
                new object?[] { "P031", "영월공장",   "CEMENT",  "BA01", "강원", "Y" },
            });

        // ── P_PROD_DAILY_HDR 합계 (공장별 3월 생산량) ─────────────
        public static readonly PA999QueryResult ProdSummary = BuildResult(
            new[] { "PLANT_CD", "PLANT_NM", "PLANT_TYPE", "TOTAL_QTY", "PROD_TYPE" },
            new object?[][]
            {
                new object?[] { "P001", "단양공장",   "CEMENT",  148_321m, "반복제조" },
                new object?[] { "P031", "영월공장",   "CEMENT",  124_850m, "반복제조" },
                new object?[] { "P022", "화성공장",   "REMICON",  99_410m, "반복제조" },
                new object?[] { "P007", "성남공장",   "REMICON",  97_830m, "반복제조" },
                new object?[] { "P004", "부산공장",   "REMICON",  90_120m, "반복제조" },
                new object?[] { "P020", "서인천공장", "REMICON",  88_560m, "반복제조" },
                new object?[] { "P008", "대전공장",   "REMICON",  79_860m, "반복제조" },
                new object?[] { "P003", "대구공장",   "REMICON",  77_120m, "반복제조" },
                new object?[] { "P014", "서대구공장", "REMICON",  74_310m, "반복제조" },
                new object?[] { "P006", "청주공장",   "REMICON",  70_240m, "반복제조" },
                new object?[] { "P025", "부천공장",   "REMICON",  72_450m, "반복제조" },
                new object?[] { "P011", "김해공장",   "REMICON",  67_630m, "반복제조" },
            });

        // ── B_ITEM 하드코딩 ───────────────────────────────────────
        public static readonly PA999QueryResult ItemList = BuildResult(
            new[] { "ITEM_CD", "ITEM_NM", "ITEM_TYPE_CD", "UNIT", "USE_YN" },
            new object?[][]
            {
                new object?[] { "RC2500", "레미콘 25-21-150", "REMICON", "M3",  "Y" },
                new object?[] { "RC3000", "레미콘 30-24-150", "REMICON", "M3",  "Y" },
                new object?[] { "RC3500", "레미콘 35-27-120", "REMICON", "M3",  "Y" },
                new object?[] { "CM0001", "시멘트(포틀랜드)", "CEMENT",  "TON", "Y" },
                new object?[] { "CM0002", "고로슬래그시멘트", "CEMENT",  "TON", "Y" },
                new object?[] { "RT0001", "레미탈(바닥용)",   "REMITAL", "TON", "Y" },
            });

        // ── PA999_TABLE_META 쿼리 결과 ────────────────────────────
        public static readonly PA999QueryResult AllTableMeta = BuildResult(
            new[] { "TABLE_NM", "TABLE_DESC", "KEYWORD_LIST", "COLUMN_LIST" },
            TableMeta.Select(t => new object?[]
            {
                t["TABLE_NM"], t["TABLE_DESC"], t["KEYWORD_LIST"], null
            }).ToArray());

        /// <summary>키워드 기반 TABLE_META 필터링 결과 반환</summary>
        public PA999QueryResult GetTableMetaResult(string[] tokens)
        {
            var matched = TableMeta
                .Where(t =>
                {
                    var kw   = (t["KEYWORD_LIST"] as string ?? "").ToLower();
                    var desc = (t["TABLE_DESC"]   as string ?? "").ToLower();
                    return tokens.Any(tok => kw.Contains(tok.ToLower()) || desc.Contains(tok.ToLower()));
                })
                .ToList();

            if (matched.Count == 0) matched = TableMeta;   // 매칭 없으면 전체 반환

            return BuildResult(
                new[] { "TABLE_NM", "TABLE_DESC", "KEYWORD_LIST", "COLUMN_LIST" },
                matched.Select(t => new object?[] { t["TABLE_NM"], t["TABLE_DESC"], t["KEYWORD_LIST"], null }).ToArray());
        }

        // ── INFORMATION_SCHEMA 컬럼 정보 하드코딩 ────────────────
        private static readonly PA999QueryResult SchemaColumns = BuildResult(
            new[] { "TABLE_NAME", "COLUMN_NAME", "DATA_TYPE", "CHARACTER_MAXIMUM_LENGTH", "IS_NULLABLE" },
            new object?[][]
            {
                // B_PLANT
                new object?[] { "B_PLANT", "PLANT_CD",   "nvarchar", 10,   "NO"  },
                new object?[] { "B_PLANT", "PLANT_NM",   "nvarchar", 100,  "NO"  },
                new object?[] { "B_PLANT", "PLANT_TYPE", "nvarchar", 20,   "YES" },
                new object?[] { "B_PLANT", "BIZ_AREA_CD","nvarchar", 10,   "YES" },
                new object?[] { "B_PLANT", "REGION_NM",  "nvarchar", 50,   "YES" },
                new object?[] { "B_PLANT", "USE_YN",     "char",     1,    "NO"  },
                // B_ITEM
                new object?[] { "B_ITEM", "ITEM_CD",      "nvarchar", 20,  "NO"  },
                new object?[] { "B_ITEM", "ITEM_NM",      "nvarchar", 200, "NO"  },
                new object?[] { "B_ITEM", "ITEM_TYPE_CD", "nvarchar", 10,  "YES" },
                new object?[] { "B_ITEM", "UNIT",         "nvarchar", 10,  "YES" },
                new object?[] { "B_ITEM", "USE_YN",       "char",     1,   "NO"  },
                // P_PROD_DAILY_HDR
                new object?[] { "P_PROD_DAILY_HDR", "PROD_SEQ",  "bigint",   null, "NO"  },
                new object?[] { "P_PROD_DAILY_HDR", "PLANT_CD",  "nvarchar", 10,   "NO"  },
                new object?[] { "P_PROD_DAILY_HDR", "PROD_DT",   "char",     8,    "NO"  },
                new object?[] { "P_PROD_DAILY_HDR", "PROD_QTY",  "decimal",  null, "YES" },
                new object?[] { "P_PROD_DAILY_HDR", "PROD_TYPE", "nvarchar", 10,   "YES" },
                new object?[] { "P_PROD_DAILY_HDR", "ITEM_CD",   "nvarchar", 20,   "YES" },
                // A_ACCT
                new object?[] { "A_ACCT", "ACCT_CD",      "nvarchar", 10,  "NO"  },
                new object?[] { "A_ACCT", "ACCT_NM",      "nvarchar", 100, "NO"  },
                new object?[] { "A_ACCT", "ACCT_TYPE_CD", "nvarchar", 10,  "YES" },
                new object?[] { "A_ACCT", "USE_YN",       "char",     1,   "NO"  },
            });

        // ── 키워드 기반 결과 매핑 ─────────────────────────────────
        public PA999QueryResult GetDemoResult(string sql)
        {
            var s = sql.ToUpperInvariant();

            // INFORMATION_SCHEMA → 컬럼 스키마 반환
            if (s.Contains("INFORMATION_SCHEMA"))
                return SchemaColumns;

            // PA999_TABLE_META → 테이블 메타 반환
            if (s.Contains("TABLE_META"))
                return AllTableMeta;

            // 생산 합계 / 생산량 관련
            if ((s.Contains("SUM") || s.Contains("TOTAL")) && s.Contains("PROD"))
                return ProdSummary;

            // B_PLANT / 공장 목록
            if (s.Contains("B_PLANT") || s.Contains("PLANT_CD") || s.Contains("PLANT_NM"))
                return PlantList;

            // B_ITEM / 품목
            if (s.Contains("B_ITEM") || s.Contains("ITEM_CD") || s.Contains("ITEM_NM"))
                return ItemList;

            // P_PROD_DAILY_HDR / 생산 실적
            if (s.Contains("PROD_DAILY") || s.Contains("PROD_QTY"))
                return ProdSummary;

            // 기본: 공장 목록 반환
            return PlantList;
        }

        // ── 헬퍼 ─────────────────────────────────────────────────
        private static PA999QueryResult BuildResult(string[] columns, object?[][] rows)
        {
            var result = new PA999QueryResult { IsSuccess = true };
            result.Columns.AddRange(columns);
            foreach (var row in rows)
            {
                var d = new Dictionary<string, object?>();
                for (int i = 0; i < columns.Length; i++)
                    d[columns[i]] = row[i];
                result.Rows.Add(d);
            }
            return result;
        }
    }
}
