#region ● Namespace declaration

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;

using Microsoft.Practices.CompositeUI.SmartParts;

using Infragistics.Shared;
using Infragistics.Win;
using Infragistics.Win.UltraWinGrid;

using Bizentro.AppFramework.UI.Controls.uniControls;
using Bizentro.AppFramework.UI.Common;
using Bizentro.AppFramework.UI.Controls;
using Bizentro.AppFramework.UI.Module;
using Bizentro.AppFramework.UI.Variables;
using Bizentro.AppFramework.UI.Common.Exceptions;
using Bizentro.AppFramework.UI.Controls.Popup;
using Bizentro.AppFramework.DataBridge;

#endregion

namespace Bizentro.App.UI.PP.CB990M1_CKO087
{
    [SmartPart]
    public partial class ModuleViewer : ViewBase
    {

        #region ▶ 1. Declaration part

        #region ■ 1.1 Program information
        /// <TemplateVersion>2.0.0.0</TemplateVersion>
        /// <NameSpace>Bizentro.App.UI.PP.CB990M1_CKO087</NameSpace>
        /// <Module>module PP</Module>
        /// <Class>ModuleViewer</Class>
        /// <Desc>
        ///   PA999 AI 챗봇 피드백 관리 (Human Evaluation → AI Optimization Bridge)
        ///   ADMIN 전용: KakaoTalk 스타일 채팅 로그 뷰 + 평가점수/피드백 편집 + AI 학습 패턴 등록
        ///
        ///   UI 구조
        ///     상단: 검색 조건 패널 (기간, 사용자)
        ///     중간: SplitContainer
        ///       Panel1 (채팅 버블): USER_QUERY=금색/우측, AI_RESPONSE=백색/좌측
        ///       Panel2 (UNIERP 그리드): 로그 상세 + 편집 가능 컬럼
        ///     하단: 평가 입력 바 (점수, 피드백, 저장, 패턴등록)
        ///
        ///   SP 명명 규칙 (v2 — UNIERP 표준)
        ///     USP_CB990M1_CKO087_Q       조회
        ///     USP_CB990M1_CKO087_CUD     생성·수정·삭제 (BOM 백업/복원 패턴 적용)
        ///     USP_CB990M1_CKO087_PAT_CUD 패턴 등록
        ///     UTP_CB990M1_CKO087         TVP (CUD 파라미터)
        ///
        ///   보안: RBAC Layer-1(메뉴권한) + Layer-2(조직 데이터 범위)
        ///   SQL 힌트: (NOLOCK) — WITH(NOLOCK) 사용 금지
        /// </Desc>
        /// <History>
        ///   <history name="JHJ" Date="2026-03-23">CB990M1 최초 생성</history>
        ///   <history name="JHJ" Date="2026-04-01">
        ///     v2 전면 리팩터링:
        ///     · KakaoTalk 스타일 채팅 버블 UI 적용
        ///     · SP 명칭 USP_ 접두사 + _Q/_CUD 접미사로 표준화
        ///     · TVP UTP_ 접두사 표준화
        ///     · BOM CUD 전략 SQL 적용 (백업→헤더수정→복원)
        ///     · 감사 컬럼 INSRT_DT/INSRT_USER_ID/UPDT_DT/UPDT_USER_ID 표준화
        ///     · TOP N 전면 제거 (2022~2025 전체 데이터 반환)
        ///     · UNIERP 그리드 하단 패널로 이동 (SplitContainer)
        ///   </history>
        /// </History>
        #endregion

        #region ■ 1.2 Class global constants

        // ── SP 명칭 (v2 — USP_ 접두사, _Q/_CUD 접미사) ──────────────────
        private const string SP_QUERY   = "USP_CB990M1_CKO087_Q";
        private const string SP_SAVE    = "USP_CB990M1_CKO087_CUD";
        private const string SP_PATTERN = "USP_CB990M1_CKO087_PAT_CUD";

        /// <summary>
        /// 이 화면 자신의 MNU_ID — RBAC 권한 체크 기준점
        /// Z_USR_ROLE_MNU_AUTHZTN_ASSO.MNU_ID 와 일치해야 함
        /// </summary>
        private const string MNU_ID_SELF = "CB990M1_CKO087";

        private string URL_PATTERN;   // PA999S1 피드백 패턴 API (app.config)

        // ── 채팅 버블 색상 상수 ─────────────────────────────────────────
        private static readonly Color COLOR_CHAT_BG      = Color.FromArgb(183, 200, 215);  // 카카오 톡 파란회색
        private static readonly Color COLOR_USER_BUBBLE  = Color.FromArgb(254, 229,   0);  // 카카오톡 금색
        private static readonly Color COLOR_USER_TEXT    = Color.FromArgb( 50,  50,  50);
        private static readonly Color COLOR_AI_BUBBLE    = Color.FromArgb(242, 242, 242);  // 연회색
        private static readonly Color COLOR_AI_TEXT      = Color.FromArgb( 30,  30,  30);
        private static readonly Color COLOR_ENTRY_SEP    = Color.FromArgb(170, 185, 200);
        private static readonly Color COLOR_HEADER_BLUE  = Color.FromArgb(  0,  95, 184);

        #endregion

        #region ■ 1.3 Class global variables

        /// <summary>
        /// Form_Load_Completed 시 1회 초기화되는 RBAC 컨텍스트.
        /// null 이면 해당 메뉴에 대한 접근 권한 없음.
        /// </summary>
        private AuthContext _authCtx = null;

        /// <summary>현재 선택된 채팅 로그 번호 (-1 = 미선택)</summary>
        private long _selectedLogSeq = -1L;

        /// <summary>선택된 채팅 엔트리 패널 (하이라이트 관리)</summary>
        private Panel _selectedPanel = null;

        #endregion

        #region ■ 1.4 Working DataSet
        dsWorking cWorking = new dsWorking();
        #endregion

        #endregion


        #region ▶ 2. Initialization part

        #region ■ 2.1 Constructor
        public ModuleViewer()
        {
            InitializeComponent();

            URL_PATTERN = ReadAppSetting(
                "PatternApiUrl",
                "http://localhost:5000/api/PA999/log/pattern");

            // ── 평가 입력 바 상단 경계선 ──────────────────────────────────
            pnlEvalBar.Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(190, 195, 210), 1))
                    e.Graphics.DrawLine(pen, 0, 0, pnlEvalBar.Width, 0);
            };
        }

        // pnlEvalBar_Paint 이벤트는 Designer.cs 에서 연결 → 위 Paint 람다와 병존
        private void pnlEvalBar_Paint(object sender, PaintEventArgs e) { }

        private static string ReadAppSetting(string key, string defaultVal)
        {
            try
            {
                var val = System.Configuration.ConfigurationManager.AppSettings[key];
                return string.IsNullOrEmpty(val) ? defaultVal : val;
            }
            catch { return defaultVal; }
        }
        #endregion

        #region ■ 2.2 Form_Load
        protected override void Form_Load()
        {
            uniBase.UData.SetWorkingDataSet(cWorking);
            uniBase.UCommon.SetViewType(enumDef.ViewType.T02_Multi);
            uniBase.UCommon.LoadInfTB19029(
                enumDef.FormType.Input,
                enumDef.ModuleInformation.Common);
            this.LoadCustomInfTB19029();
        }

        protected override void Form_Load_Completed()
        {
            uniBase.UCommon.SetToolBarSingle(enumDef.ToolBitSingle.New,    false);
            uniBase.UCommon.SetToolBarSingle(enumDef.ToolBitSingle.Delete, true);

            // ── [RBAC Layer-1+2] 폼 로드 시 권한 컨텍스트 1회 초기화 ──────
            _authCtx = CheckUserPermission(MNU_ID_SELF);
            if (_authCtx == null)
            {
                _authCtx = new AuthContext
                {
                    UsrId     = CommonVariable.gUsrID ?? "",
                    UsrNm     = CommonVariable.gUsrID ?? "",
                    RoleId    = "",
                    MnuId     = MNU_ID_SELF,
                    OrgType   = "",
                    OrgCd     = "",
                    OrgFilter = ""
                };
                uniBase.UCommon.SetToolBarSingle(enumDef.ToolBitSingle.Delete, false);
            }

            // 평가 입력 바 초기 비활성화 (항목 선택 전)
            SetEvalBarEnabled(false);
            lblSelInfo.Text = "조회 후 채팅 항목을 클릭하면 평가할 수 있습니다.";

            SyncFlpWidth();
        }

        protected override void Form_Shown() { }
        #endregion

        #region ■ 2.3 InitLocalVariables
        protected override void InitLocalVariables() { }
        #endregion

        #region ■ 2.4 SetLocalDefaultValue
        protected override void SetLocalDefaultValue()
        {
            uniDtmFrom.Value = DateTime.Today.AddDays(-30);
            uniDtmTo.Value   = DateTime.Today;
        }
        #endregion

        #region ■ 2.5 GatheringComboData
        protected override void GatheringComboData() { }
        #endregion

        #region ■ 2.6 LoadCustomInfTB19029
        public void LoadCustomInfTB19029() { }
        #endregion

        #endregion


        #region ▶ 3. Grid method part

        #region ■ 3.1 InitSpreadSheet
        private void InitSpreadSheet()
        {
            dsWorking.E_DATADataTable TB1 = cWorking.E_DATA;

            // ── 읽기 전용 컬럼 ───────────────────────────────────────────
            uniGrid1.SSSetEdit(TB1.LOG_SEQColumn.ColumnName,
                "로그번호", 70, enumDef.FieldType.ReadOnly,
                enumDef.CharCase.Default, false, enumDef.HAlign.Center, 10);

            uniGrid1.SSSetEdit(TB1.CREATED_DTColumn.ColumnName,
                "질문일시", 140, enumDef.FieldType.ReadOnly,
                enumDef.CharCase.Default, false, enumDef.HAlign.Center, 20);

            uniGrid1.SSSetEdit(TB1.USER_IDColumn.ColumnName,
                "사용자", 80, enumDef.FieldType.ReadOnly,
                enumDef.CharCase.Default, false, enumDef.HAlign.Center, 50);

            uniGrid1.SSSetEdit(TB1.USER_QUERYColumn.ColumnName,
                "사용자 질문", 240, enumDef.FieldType.ReadOnly,
                enumDef.CharCase.Default, false, enumDef.HAlign.Left, 400);

            uniGrid1.SSSetEdit(TB1.AI_RESPONSEColumn.ColumnName,
                "AI 답변", 300, enumDef.FieldType.ReadOnly,
                enumDef.CharCase.Default, false, enumDef.HAlign.Left, 600);

            uniGrid1.SSSetEdit(TB1.GENERATED_SQLColumn.ColumnName,
                "생성된 SQL", 220, enumDef.FieldType.ReadOnly,
                enumDef.CharCase.Default, false, enumDef.HAlign.Left, 1000);

            uniGrid1.SSSetEdit(TB1.RELATED_TABLESColumn.ColumnName,
                "관련 테이블", 140, enumDef.FieldType.ReadOnly,
                enumDef.CharCase.Default, false, enumDef.HAlign.Left, 200);

            // ── 편집 가능 컬럼 ───────────────────────────────────────────
            uniGrid1.SSSetEdit(TB1.PERF_SCOREColumn.ColumnName,
                "평가점수", 70, enumDef.FieldType.Default,
                enumDef.CharCase.Default, false, enumDef.HAlign.Center, 1);

            uniGrid1.SSSetEdit(TB1.SCORE_LABELColumn.ColumnName,
                "등급", 70, enumDef.FieldType.ReadOnly,
                enumDef.CharCase.Default, false, enumDef.HAlign.Center, 10);

            uniGrid1.SSSetEdit(TB1.DEV_FEEDBACKColumn.ColumnName,
                "피드백 메모", 180, enumDef.FieldType.Default,
                enumDef.CharCase.Default, false, enumDef.HAlign.Left, 2000);

            uniGrid1.SSSetEdit(TB1.CORRECT_SQLColumn.ColumnName,
                "올바른 SQL", 280, enumDef.FieldType.Default,
                enumDef.CharCase.Default, false, enumDef.HAlign.Left, 4000);

            uniGrid1.SSSetEdit(TB1.LESSSONColumn.ColumnName,
                "교훈/코멘트", 200, enumDef.FieldType.Default,
                enumDef.CharCase.Default, false, enumDef.HAlign.Left, 2000);

            // ── 감사 컬럼 (표준: INSRT_DT/INSRT_USER_ID/UPDT_DT/UPDT_USER_ID) ─
            // PA999_CHAT_LOG 실제 컬럼: CREATED_DT, FEEDBACK_DT, FEEDBACK_BY
            // SP에서 표준 감사 컬럼명으로 매핑 (SP 주석 참조)
            uniGrid1.SSSetEdit(TB1.FEEDBACK_DTColumn.ColumnName,
                "수정일시(UPDT_DT)", 135, enumDef.FieldType.ReadOnly,
                enumDef.CharCase.Default, false, enumDef.HAlign.Center, 20);

            uniGrid1.SSSetEdit(TB1.FEEDBACK_BYColumn.ColumnName,
                "수정자(UPDT_USER_ID)", 90, enumDef.FieldType.ReadOnly,
                enumDef.CharCase.Default, false, enumDef.HAlign.Center, 50);

            this.uniGrid1.InitializeGrid(
                enumDef.IsOutlookGroupBy.No,
                enumDef.IsSearch.No);
        }
        #endregion

        #region ■ 3.2 InitData
        private void InitData() { }
        #endregion

        #region ■ 3.3 SetSpreadColor
        private void SetSpreadColor()
        {
            foreach (UltraGridRow row in uniGrid1.Rows)
            {
                if (row.IsGroupByRow) continue;
                var scoreCell = row.Cells[cWorking.E_DATA.PERF_SCOREColumn.ColumnName];
                if (scoreCell == null) continue;
                object scoreObj = scoreCell.Value;

                Color bg;
                if (scoreObj == null || scoreObj == DBNull.Value)
                    bg = Color.FromArgb(230, 240, 255);
                else
                {
                    int score = 0;
                    int.TryParse(scoreObj.ToString(), out score);
                    switch (score)
                    {
                        case 5:  bg = Color.FromArgb(255, 245, 180); break;
                        case 4:  bg = Color.FromArgb(200, 240, 200); break;
                        case 3:  bg = Color.FromArgb(255, 255, 200); break;
                        case 2:
                        case 1:  bg = Color.FromArgb(255, 210, 210); break;
                        default: bg = Color.White;                    break;
                    }
                }
                row.Appearance.BackColor  = bg;
                row.Appearance.BackColor2 = bg;
            }
        }
        #endregion

        #region ■ 3.4 InitControlBinding
        protected override void InitControlBinding()
        {
            InitSpreadSheet();
            uniGrid1.uniGridSetDataBinding(cWorking.E_DATA);
        }
        #endregion

        #endregion


        #region ▶ 4. Toolbar method part

        #region ■ 4.1 Common Function group
        protected override bool OnFncQuery()    { return DBQuery(); }
        protected override bool OnPreFncSave()  { return base.OnPreFncSave(); }
        protected override bool OnFncSave()     { return DBSaveFromEvalBar(); }
        protected override bool OnPostFncSave()
        {
            base.OnPostFncSave();
            return DBQuery();
        }
        #endregion

        #region ■ 4.2 Single Function group
        protected override bool OnFncNew()    { return true; }
        protected override bool OnFncDelete() { return DBDeleteRow(); }
        protected override bool OnFncCopy()   { return true; }
        protected override bool OnFncPrev()   { return true; }
        protected override bool OnFncNext()   { return true; }
        #endregion

        #region ■ 4.3 Grid Function group
        protected override bool OnFncInsertRow() { return true; }
        protected override bool OnFncDeleteRow() { return DBDeleteRow(); }
        protected override bool OnFncCancel()    { return true; }
        protected override bool OnFncCopyRow()   { return true; }
        #endregion

        #region ■ 4.4 DB function group

        #region ■■ 4.4.1 DBQuery
        private bool GetData(out DataSet pTemp)
        {
            pTemp = null;
            try
            {
                if (_authCtx == null)
                    _authCtx = CheckUserPermission(MNU_ID_SELF);

                if (_authCtx == null)
                {
                    _authCtx = new AuthContext
                    {
                        UsrId = CommonVariable.gUsrID ?? "", UsrNm = CommonVariable.gUsrID ?? "",
                        RoleId = "", MnuId = MNU_ID_SELF, OrgType = "", OrgCd = "", OrgFilter = ""
                    };
                }

                string sFromDt   = (uniDtmFrom.Value != null && uniDtmFrom.Value != DBNull.Value)
                                   ? Convert.ToDateTime(uniDtmFrom.Value).ToString("yyyyMMdd") : "";
                string sToDt     = (uniDtmTo.Value != null && uniDtmTo.Value != DBNull.Value)
                                   ? Convert.ToDateTime(uniDtmTo.Value).ToString("yyyyMMdd") : "";
                string sUserId   = (txtUsrId.Value?.ToString() ?? "").Trim();

                // ★ SP 시그니처: USP_CB990M1_CKO087_Q @USER_ID, @FROM_DT, @TO_DT, @IS_ERROR, @SEARCH_KWD
                StringBuilder sb = new StringBuilder();
                sb.AppendFormat(@"
                    EXEC {0}
                         {1}   -- @USER_ID     (1)
                        ,{2}   -- @FROM_DT     (2)
                        ,{3}   -- @TO_DT       (3)
                        ,{4}   -- @IS_ERROR    (4) NULL=전체
                        ,{5}   -- @SEARCH_KWD  (5) NULL=전체
                    "
                    , SP_QUERY
                    , uniBase.UCommon.FilterVariable(sUserId,  "''", enumDef.FilterVarType.BraceWithSingleQuotation, true)
                    , uniBase.UCommon.FilterVariable(sFromDt,  "''", enumDef.FilterVarType.BraceWithSingleQuotation, true)
                    , uniBase.UCommon.FilterVariable(sToDt,    "''", enumDef.FilterVarType.BraceWithSingleQuotation, true)
                    , "NULL"    // @IS_ERROR — 전체 조회
                    , "NULL"    // @SEARCH_KWD — 키워드 검색 없음
                );

                pTemp = uniBase.UDataAccess.CommonQuerySQL(sb.ToString());
                if (pTemp == null || pTemp.Tables.Count == 0 || pTemp.Tables[0].Rows.Count == 0)
                {
                    uniBase.UMessage.DisplayMessageBox("900014", MessageBoxButtons.OK);
                    return false;
                }
            }
            catch (Exception ex)
            {
                bool reThrow = uniBase.UExceptionController.AutoProcessException(ex);
                if (reThrow) throw;
                return false;
            }
            return true;
        }

        private bool DBQuery()
        {
            try
            {
                _selectedLogSeq = -1L;
                _selectedPanel  = null;
                SetEvalBarEnabled(false);
                lblSelInfo.Text = "조회 중...";

                cWorking.E_DATA.Clear();
                DataSet iTemp;
                if (GetData(out iTemp))
                {
                    uniBase.UData.MergeDataTable(
                        cWorking.E_DATA, iTemp.Tables[0],
                        false, MissingSchemaAction.Ignore);

                    SetSpreadColor();
                    lblSelInfo.Text = string.Format("총 {0}건 조회됨 — 그리드 행을 클릭하여 평가하세요.", cWorking.E_DATA.Rows.Count);
                }
                else
                {
                    lblSelInfo.Text = "조회 결과 없음";
                }
            }
            catch (Exception ex)
            {
                bool reThrow = ExceptionControler.AutoProcessException(ex);
                if (reThrow) throw;
                return false;
            }
            return true;
        }
        #endregion

        #region ■■ 4.4.2 DBSave (그리드 편집 → 직접 저장)
        private bool DBSave()
        {
            if (_authCtx == null)
            {
                uniBase.UMessage.DisplayMessageBox("DT9999", MessageBoxButtons.OK,
                    "저장 권한이 없습니다. 관리자에게 문의하세요.");
                return false;
            }
            try
            {
                // ★ 그리드 편집 중인 셀을 먼저 커밋 (편집 모드 종료)
                uniGrid1.UpdateData();

                DataTable TB1 = cWorking.E_DATA.GetChanges();
                if (TB1 == null || TB1.Rows.Count == 0)
                {
                    uniBase.UMessage.DisplayMessageBox("900005", MessageBoxButtons.OK);
                    return true;
                }

                // ① CHAT_LOG 저장 (PERF_SCORE + DEV_FEEDBACK → TVP/CUD SP)
                using (uniCommand unicmd = uniBase.UDatabase.GetStoredProcCommand(SP_SAVE))
                {
                    dsInput iInput = new dsInput();
                    iInput.I_DATA.Merge(TB1, false, MissingSchemaAction.Ignore);
                    DataTable TB_IN = iInput.I_DATA.DefaultView.ToTable();

                    uniBase.UDatabase.AddInParameter(unicmd,  "@TV_DATA", System.Data.SqlDbType.Structured, TB_IN);
                    uniBase.UDatabase.AddInParameter(unicmd,  "@USER_ID", System.Data.SqlDbType.NVarChar,   CommonVariable.gUsrID);
                    uniBase.UDatabase.AddOutParameter(unicmd, "@MSG_CD",  System.Data.SqlDbType.NVarChar,   6);
                    uniBase.UDatabase.AddOutParameter(unicmd, "@MESSAGE", System.Data.SqlDbType.NVarChar,   200);
                    uniBase.UDatabase.AddReturnParameter(unicmd, "return", System.Data.SqlDbType.Int,       0);

                    uniBase.UDatabase.ExecuteNonQuery(unicmd, false);

                    int iReturn = Convert.ToInt32(
                        uniBase.UDatabase.GetParameterValue(unicmd, "return") ?? -1);
                    if (iReturn < 0)
                    {
                        string msgCd  = uniBase.UDatabase.GetParameterValue(unicmd, "@MSG_CD")?.ToString()  ?? "ER9999";
                        string msgStr = uniBase.UDatabase.GetParameterValue(unicmd, "@MESSAGE")?.ToString() ?? "오류 발생";
                        uniBase.UMessage.DisplayMessageBox(msgCd, MessageBoxButtons.OK, msgStr);
                        return false;
                    }
                }

                // ② FEEDBACK_PATTERN 자동 저장 (CORRECT_SQL 또는 LESSON이 입력된 행)
                System.Diagnostics.Debug.WriteLine("[CB990M1] ② PAT저장 대상 행 수: " + TB1.Rows.Count +
                    " | CORRECT_SQL컬럼존재: " + TB1.Columns.Contains("CORRECT_SQL") +
                    " | LESSON컬럼존재: " + TB1.Columns.Contains("LESSON"));
                foreach (DataRow row in TB1.Rows)
                {
                    if (row["CUD_CHAR"]?.ToString() != "U") continue;

                    string correctSql = row.Table.Columns.Contains("CORRECT_SQL")
                        ? (row["CORRECT_SQL"]?.ToString() ?? "").Trim() : "";
                    string lesson = row.Table.Columns.Contains("LESSON")
                        ? (row["LESSON"]?.ToString() ?? "").Trim() : "";

                    if (string.IsNullOrEmpty(correctSql) && string.IsNullOrEmpty(lesson))
                        continue;  // 패턴 데이터 없으면 건너뜀

                    long logSeq = 0;
                    if (row["LOG_SEQ"] != null && row["LOG_SEQ"] != DBNull.Value)
                        long.TryParse(row["LOG_SEQ"].ToString(), out logSeq);
                    if (logSeq <= 0) continue;

                    // 점수 기반 우선순위 결정
                    int score = 0;
                    if (row["PERF_SCORE"] != null && row["PERF_SCORE"] != DBNull.Value)
                        int.TryParse(row["PERF_SCORE"].ToString(), out score);
                    int priority = (score >= 4) ? 5 : (score == 1 ? 10 : 8);

                    string sQuery = row.Table.Columns.Contains("USER_QUERY")
                        ? (row["USER_QUERY"]?.ToString() ?? "") : "";
                    string genSql = row.Table.Columns.Contains("GENERATED_SQL")
                        ? (row["GENERATED_SQL"]?.ToString() ?? "") : "";

                    string ruleDesc = (score >= 4)
                        ? string.Format("우수 예시 (점수:{0}) - {1}", score, TruncLeft(sQuery, 50))
                        : string.Format("오류 교정 (점수:{0}) - {1}", score, TruncLeft(sQuery, 50));

                    string badPattern = (score < 4) ? genSql : "";
                    string lessonFinal = !string.IsNullOrEmpty(lesson) ? lesson : ruleDesc;

                    try
                    {
                        using (uniCommand patCmd = uniBase.UDatabase.GetStoredProcCommand(SP_PATTERN))
                        {
                            uniBase.UDatabase.AddInParameter(patCmd,  "@P_LOG_SEQ",         System.Data.SqlDbType.BigInt,   logSeq);
                            uniBase.UDatabase.AddInParameter(patCmd,  "@P_QUERY_PATTERN",   System.Data.SqlDbType.NVarChar, TruncLeft(sQuery, 200));  // ★ 사용자 질문
                            uniBase.UDatabase.AddInParameter(patCmd,  "@P_LESSON",          System.Data.SqlDbType.NVarChar, lessonFinal);
                            uniBase.UDatabase.AddInParameter(patCmd,  "@P_WRONG_APPROACH",  System.Data.SqlDbType.NVarChar, badPattern);
                            uniBase.UDatabase.AddInParameter(patCmd,  "@P_CORRECT_SQL",     System.Data.SqlDbType.NVarChar, correctSql);
                            uniBase.UDatabase.AddInParameter(patCmd,  "@P_PRIORITY",        System.Data.SqlDbType.TinyInt,  (byte)priority);
                            uniBase.UDatabase.AddInParameter(patCmd,  "@P_USER_ID",         System.Data.SqlDbType.NVarChar, CommonVariable.gUsrID);
                            uniBase.UDatabase.AddOutParameter(patCmd, "@P_MSG_CD",          System.Data.SqlDbType.NVarChar, 6);
                            uniBase.UDatabase.AddOutParameter(patCmd, "@P_MESSAGE",         System.Data.SqlDbType.NVarChar, 200);
                            uniBase.UDatabase.AddReturnParameter(patCmd, "return",          System.Data.SqlDbType.Int,      0);

                            uniBase.UDatabase.ExecuteNonQuery(patCmd, false);
                        }
                    }
                    catch (Exception patEx)
                    {
                        System.Diagnostics.Debug.WriteLine("[CB990M1] PAT_CUD 오류: " + patEx.Message);
                        MessageBox.Show("패턴 저장 오류 (LOG_SEQ=" + logSeq + "):\n" + patEx.Message,
                            "PAT_CUD 디버그", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }

                TryInvalidatePatternCache();
            }
            catch (Exception ex)
            {
                bool reThrow = ExceptionControler.AutoProcessException(ex);
                if (reThrow) throw;
                return false;
            }
            return true;
        }
        #endregion

        #region ■■ 4.4.3 DBSaveFromEvalBar — 평가 입력 바에서 직접 저장
        /// <summary>
        /// 하단 평가 입력 바(txtScore, txtFeedback)의 값을
        /// dsWorking.E_DATA 에 반영 후 DBSave() 호출.
        /// </summary>
        private bool DBSaveFromEvalBar()
        {
            if (_authCtx == null)
            {
                uniBase.UMessage.DisplayMessageBox("DT9999", MessageBoxButtons.OK,
                    "저장 권한이 없습니다. 관리자에게 문의하세요.");
                return false;
            }

            if (_selectedLogSeq < 0)
            {
                uniBase.UMessage.DisplayMessageBox("DT9999", MessageBoxButtons.OK,
                    "평가할 항목을 채팅 목록에서 먼저 클릭하세요.");
                return false;
            }

            DataRow[] rows = cWorking.E_DATA.Select(
                "LOG_SEQ = " + _selectedLogSeq);
            if (rows.Length == 0)
            {
                uniBase.UMessage.DisplayMessageBox("DT9999", MessageBoxButtons.OK,
                    "선택된 로그를 데이터셋에서 찾을 수 없습니다. 재조회 후 다시 시도하세요.");
                return false;
            }

            DataRow row = rows[0];

            // 평가점수 반영
            string scoreText = (txtScore.Text ?? "").Trim();
            int    scoreVal;
            if (!string.IsNullOrEmpty(scoreText) &&
                int.TryParse(scoreText, out scoreVal) &&
                scoreVal >= 1 && scoreVal <= 5)
            {
                row["PERF_SCORE"]  = scoreVal;
                row["SCORE_LABEL"] = ScoreToLabel(scoreVal);
            }
            else if (string.IsNullOrEmpty(scoreText))
            {
                row["PERF_SCORE"]  = DBNull.Value;
                row["SCORE_LABEL"] = "미평가";
            }
            else
            {
                uniBase.UMessage.DisplayMessageBox("DT9999", MessageBoxButtons.OK,
                    "평가점수는 1~5 사이의 정수 또는 빈 값(미평가)만 입력 가능합니다.");
                return false;
            }

            // 피드백 메모 반영
            row["DEV_FEEDBACK"] = (txtFeedback.Text ?? "").Trim();

            // 올바른 SQL 반영 (eval bar에서 입력한 경우만)
            string sCorrectSql = (txtCorrectSql.Text ?? "").Trim();
            if (!string.IsNullOrEmpty(sCorrectSql))
                row["CORRECT_SQL"] = sCorrectSql;

            // LESSON은 그리드 셀 값 유지 (eval bar 피드백 메모와 별개)

            // CUD 마킹
            row["CUD_CHAR"] = "U";

            bool ok = DBSave();
            if (ok) DBQuery();  // 저장 성공 → 재조회
            return ok;
        }
        #endregion

        #region ■■ 4.4.4 DBDeleteRow
        private bool DBDeleteRow()
        {
            if (_authCtx == null)
            {
                uniBase.UMessage.DisplayMessageBox("DT9999", MessageBoxButtons.OK,
                    "삭제 권한이 없습니다. 관리자에게 문의하세요.");
                return false;
            }

            UltraGridRow activeRow = uniGrid1.ActiveRow;
            if (activeRow == null || activeRow.IsGroupByRow) return true;

            DialogResult dr = MessageBox.Show(
                "선택한 로그를 소프트 삭제([DELETED] 마킹) 처리하시겠습니까?\n" +
                "(물리 삭제가 아닌 감사 추적 보존 방식으로 처리됩니다)",
                "삭제 확인", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            if (dr != DialogResult.Yes) return true;

            var cudCell = activeRow.Cells[cWorking.E_DATA.CUD_CHARColumn.ColumnName];
            if (cudCell == null) return false;
            cudCell.Value = "D";

            return DBSave();
        }
        #endregion

        #endregion

        #endregion


        #region ▶ 5. Event method part

        #region ■ 5.1 pnlChatScroll_Resize (미사용 — 표준 그리드 모드)
        private void pnlChatScroll_Resize(object sender, EventArgs e) { }
        #endregion

        #region ■ 5.2 그리드 CellChange — PERF_SCORE 변경 시 SCORE_LABEL 자동 갱신
        private void uniGrid1_CellChange(object sender, CellEventArgs e)
        {
            if (e.Cell.Column.Key != cWorking.E_DATA.PERF_SCOREColumn.ColumnName)
                return;

            object val = e.Cell.Value;
            string label;

            if (val == null || val == DBNull.Value || val.ToString().Trim() == "")
                label = "미평가";
            else
            {
                int score = 0;
                int.TryParse(val.ToString(), out score);
                label = ScoreToLabel(score);
            }

            var labelCell = e.Cell.Row.Cells[cWorking.E_DATA.SCORE_LABELColumn.ColumnName];
            var cudCell   = e.Cell.Row.Cells[cWorking.E_DATA.CUD_CHARColumn.ColumnName];
            if (labelCell != null) labelCell.Value = label;
            if (cudCell   != null) cudCell.Value   = "U";
        }
        #endregion

        #region ■ 5.3 그리드 AfterCellUpdate — 편집 가능 컬럼 변경 시 CUD_CHAR 'U' 마킹
        private void uniGrid1_AfterCellUpdate(object sender, CellEventArgs e)
        {
            string col = e.Cell.Column.Key;
            // 편집 가능한 모든 컬럼: DEV_FEEDBACK, CORRECT_SQL, LESSON
            if (col == cWorking.E_DATA.DEV_FEEDBACKColumn.ColumnName ||
                col == cWorking.E_DATA.CORRECT_SQLColumn.ColumnName  ||
                col == cWorking.E_DATA.LESSSONColumn.ColumnName)
            {
                var cudCell = e.Cell.Row.Cells[cWorking.E_DATA.CUD_CHARColumn.ColumnName];
                if (cudCell != null && cudCell.Value?.ToString() != "D")
                    cudCell.Value = "U";
            }
        }
        #endregion

        #region ■ 5.4 그리드 AfterSelectChange — 그리드 행 선택 시 채팅 버블 연동
        private void uniGrid1_AfterSelectChange(object sender, AfterSelectChangeEventArgs e)
        {
            UltraGridRow activeRow = uniGrid1.ActiveRow;
            if (activeRow == null || activeRow.IsGroupByRow) return;

            var logSeqCell = activeRow.Cells[cWorking.E_DATA.LOG_SEQColumn.ColumnName];
            if (logSeqCell == null) return;

            long logSeq = 0L;
            if (logSeqCell?.Value != null && logSeqCell.Value != DBNull.Value)
                long.TryParse(logSeqCell.Value.ToString(), out logSeq);
            if (logSeq > 0 && logSeq != _selectedLogSeq)
                SelectEntry(logSeq, syncGrid: false);  // 그리드에서 선택 시 그리드 재동기화 불필요
        }
        #endregion

        #region ■ 5.5 btnSave_Click — 평가 입력 바 저장 버튼
        private void btnSave_Click(object sender, EventArgs e)
        {
            DBSaveFromEvalBar();
        }
        #endregion

        #region ■ 5.6 btnRegPattern_Click — AI 패턴 등록 버튼
        private void btnRegPattern_Click(object sender, EventArgs e)
        {
            RegisterPattern();
        }
        #endregion

        #endregion


        #region ▶ 6. Popup method part
        // (현재 팝업 없음)
        #endregion


        #region ▶ 7. User-defined method part

        // ════════════════════════════════════════════════════════════════
        //  7-A. KakaoTalk 스타일 채팅 버블 렌더링
        // ════════════════════════════════════════════════════════════════

        #region ■ 7.1 SyncFlpWidth (미사용 — 표준 그리드 모드)
        private void SyncFlpWidth()
        {
            // 표준 그리드 모드에서는 채팅 UI 미사용 → 안전 종료
            if (pnlChatScroll == null || !pnlChatScroll.Visible ||
                flpMessages == null) return;
            int w = pnlChatScroll.ClientSize.Width;
            if (w > 0)
            {
                flpMessages.Width        = w;
                flpMessages.MinimumSize  = new Size(w, 0);
            }
        }
        #endregion

        #region ■ 7.2 RenderChatBubbles — DataTable → 채팅 버블 렌더링
        /// <summary>
        /// cWorking.E_DATA 의 모든 행을 KakaoTalk 스타일 채팅 버블로 렌더링.
        /// 각 행 = 1개 Q&A 교환 (USER_QUERY 금색/우측, AI_RESPONSE 회색/좌측).
        /// </summary>
        private void RenderChatBubbles(DataTable dt)
        {
            // 표준 그리드 모드에서는 채팅 UI 미사용
            if (flpMessages == null || !flpMessages.Visible || pnlChatScroll == null) return;
            RenderChatBubblesInternal(dt);
        }
        private void RenderChatBubblesInternal(DataTable dt)
        {
            flpMessages.SuspendLayout();

            // 기존 채팅 엔트리 제거 (Controls.Clear 는 자식 Dispose 없음 → 수동 Dispose)
            var oldControls = new List<Control>();
            foreach (Control c in flpMessages.Controls)
                oldControls.Add(c);
            flpMessages.Controls.Clear();
            foreach (Control c in oldControls)
                c.Dispose();

            SyncFlpWidth();

            int chatW  = flpMessages.ClientSize.Width;
            if (chatW < 300) chatW = 800;
            int innerW = chatW - flpMessages.Padding.Horizontal - 4;

            int totalH = flpMessages.Padding.Top;

            foreach (DataRow row in dt.Rows)
            {
                Panel entry = CreateChatEntryPanel(row, innerW);
                flpMessages.Controls.Add(entry);
                totalH += entry.Height + entry.Margin.Vertical;
            }

            totalH += flpMessages.Padding.Bottom;
            flpMessages.Height = Math.Max(totalH, pnlChatScroll.ClientSize.Height);

            flpMessages.ResumeLayout(true);

            // 스크롤을 최상단으로 (최신 로그가 마지막 행 → 최하단)
            // 최신 데이터 기준으로 맨 아래 스크롤
            if (flpMessages.Height > pnlChatScroll.ClientSize.Height)
                pnlChatScroll.AutoScrollPosition = new Point(0, flpMessages.Height);
        }
        #endregion

        #region ■ 7.3 CreateChatEntryPanel — 1개 Q&A 교환 패널 생성
        private Panel CreateChatEntryPanel(DataRow row, int entryW)
        {
            string userQuery   = row["USER_QUERY"]?.ToString()    ?? "";
            string aiResponse  = row["AI_RESPONSE"]?.ToString()   ?? "";
            string userId      = row["USER_ID"]?.ToString()       ?? "";
            string createdDt   = row["CREATED_DT"]?.ToString()    ?? "";
            long   logSeq      = (row["LOG_SEQ"] != null && row["LOG_SEQ"] != DBNull.Value)
                                  ? Convert.ToInt64(row["LOG_SEQ"]) : 0L;
            object scoreObj    = row["PERF_SCORE"];
            string scoreLabel  = row["SCORE_LABEL"]?.ToString()   ?? "미평가";
            string feedback    = row["DEV_FEEDBACK"]?.ToString()  ?? "";

            int maxUserW   = (int)(entryW * 0.68);
            int aiBubbleW  = (int)(entryW * 0.88);

            var panel = new Panel
            {
                Width     = entryW,
                BackColor = Color.Transparent,
                Cursor    = Cursors.Hand,
                Tag       = logSeq,
                Margin    = new Padding(0, 0, 0, 0)
            };

            int y = 8;

            // ── 헤더: 일시 + 사용자 ─────────────────────────────────────
            string headerText = string.Format("{0}  |  사용자: {1}", createdDt, userId);
            var lblHeader = new Label
            {
                Text      = headerText,
                Font      = new Font("맑은 고딕", 7.5F),
                ForeColor = Color.FromArgb(80, 90, 110),
                BackColor = Color.Transparent,
                AutoSize  = false,
                Location  = new Point(8, y),
                Size      = new Size(entryW - 16, 18)
            };
            panel.Controls.Add(lblHeader);
            y += lblHeader.Height + 4;

            // ── 사용자 말풍선 (금색, 우측 정렬) ─────────────────────────
            if (!string.IsNullOrEmpty(userQuery))
            {
                Panel userBubble = CreateUserBubble(userQuery, maxUserW);
                userBubble.Location = new Point(entryW - userBubble.Width - 8, y);
                panel.Controls.Add(userBubble);
                y += userBubble.Height + 6;
            }

            // ── AI 말풍선 (회색, 좌측 정렬) ─────────────────────────────
            if (!string.IsNullOrEmpty(aiResponse))
            {
                Panel aiBubble = CreateAiBubble(aiResponse, aiBubbleW);
                aiBubble.Location = new Point(8, y);
                panel.Controls.Add(aiBubble);
                y += aiBubble.Height + 6;
            }

            // ── 평가 요약 바 ─────────────────────────────────────────────
            y += 2;
            Color scoreColor = GetScoreColor(scoreObj);
            string scoreTxt  = string.Format("⭐ {0}", scoreLabel);
            if (!string.IsNullOrEmpty(feedback))
                scoreTxt += string.Format("  |  💬 {0}", TruncLeft(feedback, 60));

            var lblEval = new Label
            {
                Text      = scoreTxt,
                Font      = new Font("맑은 고딕", 7.5F),
                ForeColor = scoreColor,
                BackColor = Color.Transparent,
                AutoSize  = false,
                Location  = new Point(8, y),
                Size      = new Size(entryW - 16, 18)
            };
            panel.Controls.Add(lblEval);
            y += lblEval.Height + 6;

            // ── 구분선 ───────────────────────────────────────────────────
            var sep = new Panel
            {
                BackColor = COLOR_ENTRY_SEP,
                Location  = new Point(0, y),
                Size      = new Size(entryW, 1)
            };
            panel.Controls.Add(sep);
            y += 1 + 4;

            panel.Height = y;

            // ── 클릭 이벤트: 패널과 모든 자식에 연결 ────────────────────
            long capturedLogSeq = logSeq;
            EventHandler clickHandler = (s, e2) => SelectEntry(capturedLogSeq, syncGrid: true);
            panel.Click += clickHandler;
            foreach (Control c in panel.Controls)
                c.Click += clickHandler;

            return panel;
        }
        #endregion

        #region ■ 7.4 CreateUserBubble — 금색 우측 말풍선
        private Panel CreateUserBubble(string text, int maxW)
        {
            const int PAD = 12;
            var font    = new Font("맑은 고딕", 9.75F, FontStyle.Regular);
            int singleW = MeasureW(text, font);
            int textW   = Math.Min(singleW, maxW - PAD * 2);
            textW       = Math.Max(textW, 60);
            int textH   = MeasureH(text, font, textW);

            var bubble = new Panel
            {
                BackColor = COLOR_USER_BUBBLE,
                Size      = new Size(textW + PAD * 2, textH + 16),
                Cursor    = Cursors.Hand
            };

            // 둥근 모서리 효과 (Paint)
            bubble.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (var brush = new SolidBrush(COLOR_USER_BUBBLE))
                using (var path = RoundedRect(new Rectangle(0, 0, bubble.Width - 1, bubble.Height - 1), 8))
                    e.Graphics.FillPath(brush, path);
            };

            var lbl = new Label
            {
                Text      = text,
                Font      = font,
                ForeColor = COLOR_USER_TEXT,
                BackColor = Color.Transparent,
                AutoSize  = false,
                Location  = new Point(PAD, 8),
                Size      = new Size(textW, textH),
                Cursor    = Cursors.Hand
            };
            bubble.Controls.Add(lbl);
            return bubble;
        }
        #endregion

        #region ■ 7.5 CreateAiBubble — 흰/회색 좌측 말풍선
        private Panel CreateAiBubble(string text, int innerW)
        {
            const int PAD = 14;
            var badgeFont  = new Font("맑은 고딕", 7F, FontStyle.Bold);
            var answerFont = new Font("맑은 고딕", 9.75F, FontStyle.Bold);

            int textW = innerW - PAD * 2 - 30; // 30 = AI 배지 공간
            textW     = Math.Max(textW, 100);
            int textH = MeasureH(text, answerFont, textW);

            int panelW = innerW;
            int panelH = textH + PAD * 2 + 8;

            var bubble = new Panel
            {
                BackColor = COLOR_AI_BUBBLE,
                Size      = new Size(panelW, panelH),
                Cursor    = Cursors.Hand
            };

            bubble.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (var brush = new SolidBrush(COLOR_AI_BUBBLE))
                using (var path = RoundedRect(new Rectangle(0, 0, bubble.Width - 1, bubble.Height - 1), 8))
                    e.Graphics.FillPath(brush, path);
            };

            // AI 배지
            var lblBadge = new Label
            {
                Text      = "AI",
                Font      = badgeFont,
                BackColor = COLOR_HEADER_BLUE,
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleCenter,
                AutoSize  = false,
                Location  = new Point(PAD, 8),
                Size      = new Size(24, 17),
                Cursor    = Cursors.Hand
            };
            bubble.Controls.Add(lblBadge);

            // 답변 텍스트 (Bold)
            var lblAnswer = new Label
            {
                Text      = text,
                Font      = answerFont,
                ForeColor = COLOR_AI_TEXT,
                BackColor = Color.Transparent,
                AutoSize  = false,
                Location  = new Point(PAD + 30, PAD),
                Size      = new Size(textW, textH),
                Cursor    = Cursors.Hand
            };
            bubble.Controls.Add(lblAnswer);

            // 패널 높이 보정: 자식 Controls.Bottom 최대값 기준
            int finalH = PAD;
            foreach (Control c in bubble.Controls)
                if (c.Bottom + PAD > finalH) finalH = c.Bottom + PAD;
            bubble.Height = finalH;

            return bubble;
        }
        #endregion

        #region ■ 7.6 SelectEntry — 채팅 엔트리 선택 처리
        /// <summary>
        /// 채팅 버블 클릭 또는 그리드 행 선택 시 호출.
        /// 1. 이전 선택 패널 테두리 제거 / 새 패널 하이라이트
        /// 2. 평가 입력 바(txtScore, txtFeedback) 값 반영
        /// 3. syncGrid=true 이면 uniGrid1 해당 행으로 스크롤
        /// </summary>
        private void SelectEntry(long logSeq, bool syncGrid = true)
        {
            if (logSeq <= 0) return;

            // ① 이전 선택 패널 하이라이트 해제
            if (_selectedPanel != null)
            {
                _selectedPanel.BackColor = Color.Transparent;
                _selectedPanel           = null;
            }

            _selectedLogSeq = logSeq;

            // ② 채팅 버블 하이라이트 (표준 그리드 모드에서는 건너뜀)
            if (flpMessages != null && flpMessages.Visible && flpMessages.Controls.Count > 0)
            {
                foreach (Control c in flpMessages.Controls)
                {
                    if (c is Panel && c.Tag is long && (long)c.Tag == logSeq)
                    {
                        _selectedPanel           = (Panel)c;
                        _selectedPanel.BackColor = Color.FromArgb(220, 235, 255);
                        break;
                    }
                }
            }

            // ③ dsWorking에서 해당 행 찾기 → 평가 입력 바 반영
            DataRow[] rows = cWorking.E_DATA.Select("LOG_SEQ = " + logSeq);
            if (rows.Length > 0)
            {
                DataRow row   = rows[0];
                object  score = row["PERF_SCORE"];
                string  fb    = row["DEV_FEEDBACK"]?.ToString() ?? "";
                string  uid   = row["USER_ID"]?.ToString()      ?? "";
                string  dt    = row["CREATED_DT"]?.ToString()   ?? "";
                string  slbl  = row["SCORE_LABEL"]?.ToString()  ?? "미평가";

                txtScore.Text    = (score == null || score == DBNull.Value)
                                   ? "" : score.ToString();
                txtFeedback.Text = fb;
                txtCorrectSql.Text = row["CORRECT_SQL"]?.ToString() ?? "";  // ★ 올바른 SQL

                lblSelInfo.Text = string.Format(
                    "선택: LOG# {0}  |  {1}  |  {2}  |  현재평가: {3}",
                    logSeq, uid, dt, slbl);

                SetEvalBarEnabled(true);
            }

            // ④ 그리드 행 동기화 (syncGrid=true 시)
            if (syncGrid)
            {
                foreach (UltraGridRow gridRow in uniGrid1.Rows)
                {
                    if (gridRow.IsGroupByRow) continue;
                    var cell = gridRow.Cells[cWorking.E_DATA.LOG_SEQColumn.ColumnName];
                    if (cell == null) continue;
                    long gSeq = Convert.ToInt64(cell.Value ?? 0L);
                    if (gSeq == logSeq)
                    {
                        uniGrid1.ActiveRow = gridRow;
                        gridRow.Selected   = true;
                        break;
                    }
                }
            }
        }
        #endregion

        #region ■ 7.7 SetEvalBarEnabled — 평가 입력 바 활성/비활성
        private void SetEvalBarEnabled(bool enabled)
        {
            txtScore.Enabled       = enabled;
            txtFeedback.Enabled    = enabled;
            if (txtCorrectSql != null)
                txtCorrectSql.Enabled = enabled;
            btnSave.Enabled        = enabled;
            btnRegPattern.Enabled  = enabled && (_authCtx != null);
        }
        #endregion

        // ════════════════════════════════════════════════════════════════
        //  7-B. AI 패턴 등록
        // ════════════════════════════════════════════════════════════════

        #region ■ 7.8 RegisterPattern — 선택된 로그를 PA999_FEEDBACK_PATTERN에 등록
        private void RegisterPattern()
        {
            // 채팅 버블에서 선택된 항목 우선, 없으면 그리드 ActiveRow 사용
            long logSeq = _selectedLogSeq;

            UltraGridRow activeRow = uniGrid1.ActiveRow;
            if (logSeq < 0 && (activeRow == null || activeRow.IsGroupByRow))
            {
                uniBase.UMessage.DisplayMessageBox("DT9999", MessageBoxButtons.OK,
                    "패턴으로 등록할 항목을 먼저 선택하세요.");
                return;
            }

            // 데이터 수집 (채팅 버블 선택 우선)
            object scoreObj;
            string sQuery, sSql, sFeedback, sCorrectSql, sLesson;

            if (logSeq >= 0)
            {
                DataRow[] rows = cWorking.E_DATA.Select("LOG_SEQ = " + logSeq);
                if (rows.Length == 0) { return; }
                DataRow row = rows[0];
                scoreObj    = row["PERF_SCORE"];
                sQuery      = row["USER_QUERY"]?.ToString()    ?? "";
                sSql        = row["GENERATED_SQL"]?.ToString() ?? "";
                sFeedback   = row["DEV_FEEDBACK"]?.ToString()  ?? "";
                sCorrectSql = row["CORRECT_SQL"]?.ToString()   ?? "";
                sLesson     = row["LESSON"]?.ToString()        ?? "";
            }
            else
            {
                logSeq      = Convert.ToInt64(activeRow.Cells[cWorking.E_DATA.LOG_SEQColumn.ColumnName]?.Value ?? 0L);
                scoreObj    = activeRow.Cells[cWorking.E_DATA.PERF_SCOREColumn.ColumnName]?.Value;
                sQuery      = activeRow.Cells[cWorking.E_DATA.USER_QUERYColumn.ColumnName]?.Value?.ToString()    ?? "";
                sSql        = activeRow.Cells[cWorking.E_DATA.GENERATED_SQLColumn.ColumnName]?.Value?.ToString() ?? "";
                sFeedback   = activeRow.Cells[cWorking.E_DATA.DEV_FEEDBACKColumn.ColumnName]?.Value?.ToString()  ?? "";
                sCorrectSql = activeRow.Cells[cWorking.E_DATA.CORRECT_SQLColumn.ColumnName]?.Value?.ToString()   ?? "";
                sLesson     = activeRow.Cells[cWorking.E_DATA.LESSSONColumn.ColumnName]?.Value?.ToString()       ?? "";
            }

            // ★ eval bar 값이 있으면 우선 사용 (그리드 값보다 eval bar 직접 입력 우선)
            string evalCorrectSql = (txtCorrectSql.Text?.ToString() ?? "").Trim();
            string evalLesson     = (txtFeedback.Text ?? "").Trim();
            if (!string.IsNullOrEmpty(evalCorrectSql)) sCorrectSql = evalCorrectSql;
            if (!string.IsNullOrEmpty(evalLesson))     sLesson     = evalLesson;

            if (scoreObj == null || scoreObj == DBNull.Value || scoreObj.ToString().Trim() == "")
            {
                uniBase.UMessage.DisplayMessageBox("DT9999", MessageBoxButtons.OK,
                    "평가점수(1~5)를 먼저 입력한 후 패턴을 등록하세요.");
                return;
            }

            int score = 0;
            int.TryParse(scoreObj.ToString(), out score);
            if (score == 3)
            {
                uniBase.UMessage.DisplayMessageBox("DT9999", MessageBoxButtons.OK,
                    "3점(REVIEW)은 패턴 등록 대상이 아닙니다.\n" +
                    "5점/4점(GOLD/PASS) 또는 1점/2점(FAIL)만 등록 가능합니다.");
                return;
            }

            string sRuleDescDefault;
            string sBadPattern  = "";
            string sGoodPattern = "";
            string sLessonFinal = "";
            int    nPriority;

            if (score >= 4)
            {
                // GOLD/PASS: 올바른SQL = AI가 만든 SQL(또는 개발자가 입력한 SQL)
                sRuleDescDefault = string.Format("우수 예시 (점수:{0}) - {1}", score, TruncLeft(sQuery, 50));
                sGoodPattern     = !string.IsNullOrEmpty(sCorrectSql) ? sCorrectSql : sSql;
                sLessonFinal     = !string.IsNullOrEmpty(sLesson) ? sLesson : sRuleDescDefault;
                nPriority        = 5;
            }
            else
            {
                // FAIL: 잘못된SQL = AI SQL, 올바른SQL = 개발자 입력
                sRuleDescDefault = string.Format("오류 교정 (점수:{0}) - {1}", score, TruncLeft(sQuery, 50));
                sBadPattern      = sSql;
                sGoodPattern     = !string.IsNullOrEmpty(sCorrectSql) ? sCorrectSql : sFeedback;
                sLessonFinal     = !string.IsNullOrEmpty(sLesson) ? sLesson : sRuleDescDefault;
                nPriority        = score == 1 ? 10 : 8;
            }

            string sTypeLabel = score >= 4 ? "GOLD_EXAMPLE (우수 예시)" : "ERROR_CORRECTION (오류 교정)";
            DialogResult drConfirm = MessageBox.Show(
                string.Format(
                    "다음 내용으로 AI 학습 패턴을 등록하시겠습니까?\n\n" +
                    "  유형    : {0}\n" +
                    "  질문    : {1}\n" +
                    "  우선순위: {2}\n" +
                    (score >= 4
                        ? "  GOLD SQL: {3}"
                        : "  교정 메모: {3}"),
                    sTypeLabel,
                    TruncLeft(sQuery, 80),
                    nPriority,
                    TruncLeft(score >= 4 ? sSql : sFeedback, 100)
                ),
                "AI 패턴 등록 확인",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (drConfirm != DialogResult.Yes) return;

            try
            {
                using (uniCommand unicmd = uniBase.UDatabase.GetStoredProcCommand(SP_PATTERN))
                {
                    uniBase.UDatabase.AddInParameter(unicmd,  "@P_LOG_SEQ",         System.Data.SqlDbType.BigInt,   logSeq);
                    uniBase.UDatabase.AddInParameter(unicmd,  "@P_QUERY_PATTERN",  System.Data.SqlDbType.NVarChar, TruncLeft(sQuery, 200));
                    uniBase.UDatabase.AddInParameter(unicmd,  "@P_LESSON",         System.Data.SqlDbType.NVarChar, sLessonFinal);
                    uniBase.UDatabase.AddInParameter(unicmd,  "@P_WRONG_APPROACH", System.Data.SqlDbType.NVarChar, sBadPattern);
                    uniBase.UDatabase.AddInParameter(unicmd,  "@P_CORRECT_SQL",    System.Data.SqlDbType.NVarChar, sGoodPattern);
                    uniBase.UDatabase.AddInParameter(unicmd,  "@P_PRIORITY",       System.Data.SqlDbType.TinyInt,  (byte)nPriority);
                    uniBase.UDatabase.AddInParameter(unicmd,  "@P_USER_ID",        System.Data.SqlDbType.NVarChar, CommonVariable.gUsrID);
                    uniBase.UDatabase.AddOutParameter(unicmd, "@P_MSG_CD",         System.Data.SqlDbType.NVarChar, 6);
                    uniBase.UDatabase.AddOutParameter(unicmd, "@P_MESSAGE",        System.Data.SqlDbType.NVarChar, 200);
                    uniBase.UDatabase.AddReturnParameter(unicmd, "return",         System.Data.SqlDbType.Int,      0);

                    uniBase.UDatabase.ExecuteNonQuery(unicmd, false);

                    int    iReturn = Convert.ToInt32(uniBase.UDatabase.GetParameterValue(unicmd, "return") ?? -1);
                    string msgStr  = uniBase.UDatabase.GetParameterValue(unicmd, "@P_MESSAGE")?.ToString() ?? "";

                    if (iReturn < 0)
                    {
                        uniBase.UMessage.DisplayMessageBox("DT9999", MessageBoxButtons.OK, msgStr);
                        return;
                    }

                    TryInvalidatePatternCache();

                    uniBase.UMessage.DisplayMessageBox("DT9999", MessageBoxButtons.OK,
                        msgStr + "\n\nAI 패턴이 등록되었습니다.\n" +
                        "다음 질문부터 시스템 프롬프트에 자동 반영됩니다.");
                }
            }
            catch (Exception ex)
            {
                bool reThrow = ExceptionControler.AutoProcessException(ex);
                if (reThrow) throw;
            }
        }
        #endregion

        #region ■ 7.9 TryInvalidatePatternCache
        private void TryInvalidatePatternCache()
        {
            try
            {
                // ★ 수정: /health 대신 전용 캐시 무효화 엔드포인트 호출
                //   → 패턴 등록 즉시 서버 캐시 비움 (기존: 최대 10분 지연)
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(
                    URL_PATTERN + "/cache");   // DELETE /api/PA999/log/pattern/cache
                req.Method  = "DELETE";
                req.Timeout = 3000;
                req.Proxy   = null;
                using (var resp = req.GetResponse()) { }
            }
            catch { }
        }
        #endregion

        // ════════════════════════════════════════════════════════════════
        //  7-C. 헬퍼 메서드
        // ════════════════════════════════════════════════════════════════

        #region ■ 7.10 MeasureH / MeasureW — 텍스트 크기 측정
        private static int MeasureH(string text, Font font, int maxWidth)
        {
            if (string.IsNullOrEmpty(text) || maxWidth <= 0) return 20;
            int h = TextRenderer.MeasureText(
                text, font,
                new Size(maxWidth, 99999),
                TextFormatFlags.WordBreak | TextFormatFlags.Left | TextFormatFlags.NoPadding).Height;
            return Math.Max(h + 6, 20);
        }

        private static int MeasureW(string text, Font font)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            return TextRenderer.MeasureText(
                text, font,
                new Size(9999, 9999),
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width;
        }
        #endregion

        #region ■ 7.11 RoundedRect — 둥근 모서리 GraphicsPath
        private static System.Drawing.Drawing2D.GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            int d = radius * 2;
            path.AddArc(bounds.X,                     bounds.Y,                      d, d, 180, 90);
            path.AddArc(bounds.X + bounds.Width - d,  bounds.Y,                      d, d, 270, 90);
            path.AddArc(bounds.X + bounds.Width - d,  bounds.Y + bounds.Height - d,  d, d,   0, 90);
            path.AddArc(bounds.X,                     bounds.Y + bounds.Height - d,  d, d,  90, 90);
            path.CloseFigure();
            return path;
        }
        #endregion

        #region ■ 7.12 ScoreToLabel / GetScoreColor
        private static string ScoreToLabel(int score)
        {
            switch (score)
            {
                case 5:  return "GOLD";
                case 4:  return "PASS";
                case 3:  return "REVIEW";
                case 2:
                case 1:  return "FAIL";
                default: return "";
            }
        }

        private static Color GetScoreColor(object scoreObj)
        {
            if (scoreObj == null || scoreObj == DBNull.Value)
                return Color.FromArgb(100, 130, 180);  // 미평가 = 파란계열

            int _scoreVal = 0;
            int.TryParse(scoreObj.ToString(), out _scoreVal);
            switch (_scoreVal)
            {
                case 5:  return Color.FromArgb(180, 130,   0);  // GOLD  = 금색
                case 4:  return Color.FromArgb( 34, 139,  60);  // PASS  = 녹색
                case 3:  return Color.FromArgb(180, 150,   0);  // REVIEW= 황색
                default: return Color.FromArgb(200,  50,  50);  // FAIL  = 빨간색
            }
        }
        #endregion

        #region ■ 7.13 IsNull_DBNull_Empty
        private bool IsNull_DBNull_Empty(object obj)
        {
            return obj == null || obj == DBNull.Value || obj.ToString().Trim() == "";
        }
        #endregion

        #region ■ 7.14 TruncLeft
        private static string TruncLeft(string s, int len)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Length <= len ? s : s.Substring(0, len) + "...";
        }
        #endregion

        // ════════════════════════════════════════════════════════════════
        //  RBAC (Role-Based Access Control) — 이중 보안 레이어
        //
        //  Layer-1  메뉴 기능 권한  : MNU_ID 기준 접근 가능 여부
        //  Layer-2  조직 데이터 범위: ORG_TYPE / ORG_CD 기준 자동 필터
        // ════════════════════════════════════════════════════════════════

        #region ■ 7.15 AuthContext (RBAC 컨텍스트 내부 클래스)
        private sealed class AuthContext
        {
            public string UsrId    { get; set; }
            public string UsrNm    { get; set; }
            public string RoleId   { get; set; }
            public string MnuId    { get; set; }
            public string OrgType  { get; set; }
            public string OrgCd    { get; set; }
            public string OrgFilter { get; set; }
        }
        #endregion

        #region ■ 7.16 CheckUserPermission
        private AuthContext CheckUserPermission(string pMnuId)
        {
            try
            {
                string sSafeUsrId = (CommonVariable.gUsrID ?? "").Replace("'", "''");
                string sSafeMnuId = (pMnuId ?? "").Replace("'", "''");

                // (NOLOCK) 힌트 — WITH(NOLOCK) 사용 금지 (UNIERP 표준)
                string sql = string.Format(@"
                    SELECT DISTINCT
                         A.USR_ID
                        ,A.USR_NM
                        ,B.USR_ROLE_ID
                        ,E.MNU_ID
                        ,F.MNU_NM
                        ,O.ORG_TYPE
                        ,O.ORG_CD
                    FROM Z_USR_MAST_REC                (NOLOCK) A
                    INNER JOIN Z_USR_MAST_REC_USR_ROLE_ASSO (NOLOCK) B
                        ON  A.USR_ID      = B.USR_ID
                    INNER JOIN Z_USR_ROLE               (NOLOCK) C
                        ON  B.USR_ROLE_ID = C.USR_ROLE_ID
                    INNER JOIN Z_USR_ROLE_COMPST_ROLE_ASSO (NOLOCK) D
                        ON  C.USR_ROLE_ID = D.COMPST_ROLE_ID
                    INNER JOIN Z_USR_ROLE_MNU_AUTHZTN_ASSO (NOLOCK) E
                        ON  D.USR_ROLE_ID = E.USR_ROLE_ID
                    INNER JOIN Z_LANG_CO_MAST_MNU       (NOLOCK) F
                        ON  E.MNU_ID      = F.MNU_ID
                    LEFT  JOIN Z_USR_ORG_MAST           (NOLOCK) O
                        ON  A.USR_ID      = O.USR_ID
                    WHERE A.USR_VALID_DT >= GETDATE()
                      AND A.USE_YN        != 'N'
                      AND F.LANG_CD        = 'KO'
                      AND E.MNU_USE_YN     = 'Y'
                      AND A.USR_ID         = '{0}'
                      AND E.MNU_ID         = '{1}'
                    ", sSafeUsrId, sSafeMnuId);

                DataSet ds = uniBase.UDataAccess.CommonQuerySQL(sql);
                if (ds == null || ds.Tables.Count == 0 || ds.Tables[0].Rows.Count == 0)
                    return null;

                DataRow r = ds.Tables[0].Rows[0];

                string orgType = (r["ORG_TYPE"] == DBNull.Value)
                                 ? "" : r["ORG_TYPE"].ToString().Trim();
                string orgCd   = (r["ORG_CD"]   == DBNull.Value)
                                 ? "" : r["ORG_CD"].ToString().Trim();

                return new AuthContext
                {
                    UsrId     = r["USR_ID"]?.ToString()      ?? "",
                    UsrNm     = r["USR_NM"]?.ToString()      ?? "",
                    RoleId    = r["USR_ROLE_ID"]?.ToString() ?? "",
                    MnuId     = r["MNU_ID"]?.ToString()      ?? "",
                    OrgType   = orgType,
                    OrgCd     = orgCd,
                    OrgFilter = BuildOrgFilterClause(orgType, orgCd)
                };
            }
            catch (Exception ex)
            {
                bool reThrow = ExceptionControler.AutoProcessException(ex);
                if (reThrow) throw;
                return null;
            }
        }
        #endregion

        #region ■ 7.17 BuildOrgFilterClause
        private static string BuildOrgFilterClause(string orgType, string orgCd)
        {
            if (string.IsNullOrEmpty(orgType) || string.IsNullOrEmpty(orgCd))
                return "";

            string safeCd = orgCd.Replace("'", "''");
            switch (orgType.ToUpperInvariant())
            {
                case "PL": return string.Format("AND PLANT_CD = '{0}'",  safeCd);
                case "BU": return string.Format("AND BU_CD    = '{0}'",  safeCd);
                case "BA": return string.Format("AND BA_CD    = '{0}'",  safeCd);
                default:   return "";
            }
        }
        #endregion

        #endregion

    }
}
