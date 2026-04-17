#region ● Namespace declaration

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;

using Microsoft.Practices.CompositeUI.SmartParts;

using Bizentro.AppFramework.UI.Common;
using Bizentro.AppFramework.UI.Controls;
using Bizentro.AppFramework.UI.Module;
using Bizentro.AppFramework.UI.Variables;
using Bizentro.AppFramework.UI.Common.Exceptions;
using Bizentro.AppFramework.UI.Controls.Popup;
using Bizentro.AppFramework.DataBridge;

#endregion

namespace Bizentro.App.UI.PP.PA999M1_CKO087
{
    [SmartPart]
    public partial class ModuleViewer : ViewBase
    {
        #region ▶ 1. Declaration part

        #region ■ 1.1 Program information
        /// <TemplateVersion>0.0.1.0</TemplateVersion>
        /// <NameSpace>Bizentro.App.UI.PP.PA999M1_CKO087</NameSpace>
        /// <Module>module PP</Module>
        /// <Class>ModuleViewer</Class>
        /// <Desc>UNIERP AI 챗봇 서비스</Desc>
        #endregion

        #region ■ 1.2. Class global constants
        private string URL_ASK;
        private string URL_SESSION;
        private string URL_HEALTH;
        private string URL_FEEDBACK;
        private const int API_TIMEOUT_MS = 120000;
        #endregion

        #region ■ 1.3. Class global variables
        private string cSessionId = Guid.NewGuid().ToString();
        private bool   cIsLoading = false;
        private string _orgType   = "";
        private string _orgCd     = "";
        private string _userName  = "";
        private Panel  _loadingPanel = null;  // "분석 중..." 임시 패널
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
            URL_ASK      = ReadAppSetting("ChatbotApiUrl",      "http://localhost:5000/api/PA999/ask");
            URL_SESSION  = ReadAppSetting("ChatbotSessionUrl",  "http://localhost:5000/api/PA999/session");
            URL_HEALTH   = ReadAppSetting("ChatbotHealthUrl",   "http://localhost:5000/api/PA999/health");
            URL_FEEDBACK = ReadAppSetting("ChatbotFeedbackUrl", "http://localhost:5000/api/PA999/log");

            // 창 크기 변경 시 flpMessages 너비 동기화
            pnlChatScroll.Resize += (s, e) => SyncFlpWidth();

            // ── 입력바 상단 "질문 입력" 헤더 레이블 ──────────────────────
            // 사용자가 어디에 입력해야 하는지 즉각 인지할 수 있도록 가시성 강화
            var lblInputHeader = new Label
            {
                Text      = "✏  질문 입력",
                Font      = new Font("맑은 고딕", 8.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 95, 184),
                AutoSize  = true,
                Location  = new Point(13, 6)
            };
            var lblInputHint = new Label
            {
                Text      = "Enter: 전송  ·  Shift+Enter: 줄바꿈  ·  F5: AI 질문",
                Font      = new Font("맑은 고딕", 7.5F),
                ForeColor = Color.FromArgb(140, 148, 168),
                AutoSize  = true,
                Location  = new Point(110, 8)
            };
            pnlInputBar.Controls.Add(lblInputHeader);
            pnlInputBar.Controls.Add(lblInputHint);
            lblInputHeader.BringToFront();
            lblInputHint.BringToFront();

            // ══════════════════════════════════════════════════════════
            // ■ 구역 구분 테두리 (셀 경계 가시화)
            // ══════════════════════════════════════════════════════════

            // ① 외곽 테두리: BackColor(파랑) + Padding(1px)만으로 렌더링
            //    Paint DrawRectangle 제거 → 이중 렌더링(2px 두께) 방지

            // ② 열 구분선: pnlColDiv(BackColor=파랑, 1px 절대폭 열)가 구분선 역할
            //    → Paint 이벤트 불필요 (전용 패널 배경색 자체가 구분선)

            // ③ 행 구분선: pnlRowDiv(BackColor=파랑, 1px 절대행)가 채팅↔입력 구분선 역할

            // ④ 이력 헤더 하단 구분선
            pnlHistoryHeader.Paint += PnlHistoryHeader_Paint;
        }

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
            uniBase.UCommon.LoadInfTB19029(enumDef.FormType.Input, enumDef.ModuleInformation.Common);
            this.LoadCustomInfTB19029();
        }

        protected override void Form_Load_Completed()
        {
            uniBase.UCommon.SetToolBarSingle(enumDef.ToolBitSingle.New, true);
            SyncFlpWidth();

            // ── 기본 화면: 모듈 최초 진입 시 환영 말풍선 표시 ─────────────
            // "+ 새 대화" 클릭 시와 동일한 화면을 초기 기본값으로 표시
            AppendAiBubble(false,
                "새 대화가 시작되었습니다.\n무엇이든 질문해 보세요!\n\n"
                + "예) 단양공장(P001) 이번달 생산량 알려줘\n"
                + "예) 2024년 연간 생산실적을 월별로 보여줘",
                "", "", null);

            FetchUserOrgInfo();
            LoadChatHistory();   // ★ 로그인 사용자의 기존 대화 이력 사이드바에 표시
            CheckServerHealth();
        }

        protected override void Form_Shown() { }
        #endregion

        #region ■ 2.3 InitLocalVariables
        protected override void InitLocalVariables() { }
        #endregion

        #region ■ 2.4 SetLocalDefaultValue
        protected override void SetLocalDefaultValue() { }
        #endregion

        #region ■ 2.5 GatheringComboData
        protected override void GatheringComboData()
        {
            uniBase.UData.ComboMajorAdd("USE_YN", "XQ040");
        }
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
            uniGrid1.SSSetEdit(TB1.SEQ_NOColumn.ColumnName,          "순번",        50,  enumDef.FieldType.ReadOnly, enumDef.CharCase.Default, false, enumDef.HAlign.Center, 10);
            uniGrid1.SSSetEdit(TB1.QUERY_DTColumn.ColumnName,        "질문일시",   140,  enumDef.FieldType.ReadOnly, enumDef.CharCase.Default, false, enumDef.HAlign.Center, 20);
            uniGrid1.SSSetEdit(TB1.QUESTIONColumn.ColumnName,        "사용자 질문",260,  enumDef.FieldType.ReadOnly, enumDef.CharCase.Default, false, enumDef.HAlign.Left,  500);
            uniGrid1.SSSetEdit(TB1.AI_ANSWERColumn.ColumnName,       "AI 답변",    360,  enumDef.FieldType.ReadOnly, enumDef.CharCase.Default, false, enumDef.HAlign.Left, 2000);
            uniGrid1.SSSetEdit(TB1.GENERATED_SQLColumn.ColumnName,   "생성된 SQL", 200,  enumDef.FieldType.ReadOnly, enumDef.CharCase.Default, false, enumDef.HAlign.Left, 2000);
            uniGrid1.SSSetEdit(TB1.RELEVANT_TABLESColumn.ColumnName, "관련 테이블",150,  enumDef.FieldType.ReadOnly, enumDef.CharCase.Default, false, enumDef.HAlign.Left,  200);
            this.uniGrid1.InitializeGrid(enumDef.IsOutlookGroupBy.No, enumDef.IsSearch.No);
        }
        #endregion

        #region ■ 3.2 InitData
        private void InitData() { }
        #endregion

        #region ■ 3.3 SetSpreadColor
        private void SetSpreadColor() { }
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

        protected override bool OnFncQuery() { return DBQuery(); }
        protected override bool OnPreFncSave() { return base.OnPreFncSave(); }
        protected override bool OnFncSave()    { return DBSave(); }
        protected override bool OnPostFncSave() { base.OnPostFncSave(); return true; }

        protected override bool OnFncNew()    { return true; }
        protected override bool OnFncDelete() { return true; }
        protected override bool OnFncCopy()   { return true; }
        protected override bool OnFncPrev()   { return true; }
        protected override bool OnFncNext()   { return true; }
        protected override bool OnFncInsertRow() { return true; }
        protected override bool OnFncDeleteRow() { return true; }
        protected override bool OnFncCancel()    { return true; }
        protected override bool OnFncCopyRow()   { return true; }

        #region ■ DBQuery
        private bool GetData(out DataSet pTemp)
        {
            pTemp = null;
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendFormat(@"EXEC USP_PP_PA999Q1_CKO087_Q {0}, {1}, {2}, {3}"
                    , uniBase.UCommon.FilterVariable("", "''", enumDef.FilterVarType.BraceWithSingleQuotation, true)
                    , uniBase.UCommon.FilterVariable("", "''", enumDef.FilterVarType.BraceWithSingleQuotation, true)
                    , uniBase.UCommon.FilterVariable("", "''", enumDef.FilterVarType.BraceWithSingleQuotation, true)
                    , uniBase.UCommon.FilterVariable("", "''", enumDef.FilterVarType.BraceWithSingleQuotation, true));
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
                DataSet iTemp;
                if (GetData(out iTemp))
                {
                    uniBase.UData.MergeDataTable(cWorking.E_DATA, iTemp.Tables[0], false, MissingSchemaAction.Ignore);
                    SetSpreadColor();
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

        #region ■ DBSave
        private bool DBSave()
        {
            try
            {
                DataTable TB1 = cWorking.E_DATA.GetChanges();
                if (TB1 != null)
                {
                    using (uniCommand unicmd = uniBase.UDatabase.GetStoredProcCommand("USP_PP_PA999M1_CKO087_CUD"))
                    {
                        dsInput iInput = new dsInput();
                        iInput.I_DATA.Merge(TB1, false, MissingSchemaAction.Ignore);
                        DataTable TB_HDR = iInput.I_DATA.DefaultView.ToTable();
                        uniBase.UDatabase.AddInParameter(unicmd,  "@DATA",    System.Data.SqlDbType.Structured, TB_HDR);
                        uniBase.UDatabase.AddInParameter(unicmd,  "@USER_ID", System.Data.SqlDbType.NVarChar,   CommonVariable.gUsrID);
                        uniBase.UDatabase.AddOutParameter(unicmd, "@MSG_CD",  System.Data.SqlDbType.NVarChar,   6);
                        uniBase.UDatabase.AddOutParameter(unicmd, "@MESSAGE", System.Data.SqlDbType.NVarChar,   200);
                        uniBase.UDatabase.AddReturnParameter(unicmd, "return", System.Data.SqlDbType.NVarChar,  200);
                        uniBase.UDatabase.ExecuteNonQuery(unicmd, false);
                        object retVal = uniBase.UDatabase.GetParameterValue(unicmd, "return");
                        int iReturn = (retVal == null || retVal == DBNull.Value) ? -1 : Convert.ToInt32(retVal);
                        if (iReturn < 0)
                        {
                            string msgCd  = uniBase.UDatabase.GetParameterValue(unicmd, "@MSG_CD")  as string;
                            string msgStr = uniBase.UDatabase.GetParameterValue(unicmd, "@MESSAGE") as string;
                            uniBase.UMessage.DisplayMessageBox(msgCd, MessageBoxButtons.OK, msgStr);
                            return false;
                        }
                    }
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

        #region ■ AskAI
        private void AskAI()
        {
            string sQuestion = (txtQuestion.Value?.ToString() ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(sQuestion))
            {
                uniBase.UMessage.DisplayMessageBox("DT9999", MessageBoxButtons.OK, "질문을 입력해 주세요.");
                return;
            }
            if (cIsLoading)
            {
                uniBase.UMessage.DisplayMessageBox("DT9999", MessageBoxButtons.OK, "AI가 응답 중입니다. 잠시 후 다시 시도해 주세요.");
                return;
            }

            cIsLoading       = true;
            btnAskAI.Enabled = false;

            // 1) 사용자 말풍선 추가
            AppendUserBubble(sQuestion);

            // 2) "분석 중..." 임시 패널
            _loadingPanel = CreateLoadingPanel();
            AppendToFlp(_loadingPanel);

            var requestObj = new Dictionary<string, object>
            {
                { "question",  sQuestion             },
                { "sessionId", cSessionId            },
                { "userId",    CommonVariable.gUsrID },
                { "plantCd",   _orgCd                },
                { "deptCd",    ""                    },
                { "userRole",  ""                    },
                { "orgType",   _orgType              },
                { "orgCd",     _orgCd                }
            };
            string jsonRequest = new JavaScriptSerializer().Serialize(requestObj);

            // ── BackgroundWorker: HTTP 호출을 백그라운드 스레드에서 실행 ──
            // 이유: PostWithTimeout은 최대 120초 대기 → UI 스레드에서 직접 실행 시
            //       버튼/입력창 포함 전체 화면이 120초간 완전히 멈추는 버그 수정
            var bgw = new System.ComponentModel.BackgroundWorker();
            bgw.DoWork += (s, e) =>
            {
                e.Result = PostWithTimeout(URL_ASK, (string)e.Argument, API_TIMEOUT_MS);
            };
            bgw.RunWorkerCompleted += (s, e) =>
            {
                // RunWorkerCompleted는 UI 스레드에서 실행 → 컨트롤 접근 안전
                try
                {
                    RemoveLoadingPanel();

                    if (e.Error is WebException wex)
                    {
                        string errMsg = wex.Message;
                        if (wex.Response != null)
                        {
                            try
                            {
                                using (var resp = wex.Response)
                                {
                                    var stream = resp?.GetResponseStream();
                                    if (stream != null)
                                        using (var sr = new StreamReader(stream))
                                            errMsg = sr.ReadToEnd();
                                }
                            }
                            catch { }
                        }
                        AppendAiBubble(true,
                            "⚠ PA999S1 서버 연결 오류\n\n" + errMsg
                            + "\n\n[확인사항]\n"
                            + "① PA999S1 서버가 실행 중인지 확인\n"
                            + "② app.config ChatbotApiUrl 주소 확인\n"
                            + "③ 방화벽/포트(5000) 허용 여부 확인",
                            "", "", null);
                        return;
                    }

                    if (e.Error != null)
                    {
                        bool reThrow = ExceptionControler.AutoProcessException(e.Error);
                        if (reThrow)
                        {
                            AppendAiBubble(true, "시스템 오류: " + e.Error.Message, "", "", null);
                        }
                        return;
                    }

                    string jsonResponse = (string)e.Result;
                    var responseObj = new JavaScriptSerializer()
                        .Deserialize<Dictionary<string, object>>(jsonResponse);

                    if (responseObj == null)
                    {
                        AppendAiBubble(true, "서버 응답을 파싱할 수 없습니다.", "", "", null);
                        return;
                    }

                    bool isError = responseObj.ContainsKey("isError") &&
                                   responseObj["isError"] is bool b && b;

                    string sAnswer    = responseObj.ContainsKey("answer")
                                        ? (responseObj["answer"]?.ToString() ?? "") : "";
                    // Zero SQL Policy: 생성된 SQL은 서버 PA999_CHAT_LOG에만 기록
                    // UI에 원시 SQL 노출 시 보안 위협(SQL 구조 노출, 테이블명 유출) 발생
                    string sGenSql    = ""; // 의도적으로 빈 문자열 — UI 미노출
                    string sRelTables = "";
                    if (responseObj.ContainsKey("relevantTables") &&
                        responseObj["relevantTables"] is System.Collections.ArrayList lst)
                    {
                        var tblList = new List<string>();
                        foreach (var t in lst) tblList.Add(t.ToString());
                        sRelTables = string.Join(", ", tblList);
                    }

                    System.Collections.ArrayList gridList = null;
                    if (responseObj.ContainsKey("gridData") &&
                        responseObj["gridData"] is System.Collections.ArrayList gl && gl.Count > 0)
                        gridList = gl;

                    // ★ logSeq 추출 (사용자 피드백용)
                    long? logSeqVal = null;
                    if (responseObj.ContainsKey("logSeq") && responseObj["logSeq"] != null)
                    {
                        long parsed;
                        if (long.TryParse(responseObj["logSeq"].ToString(), out parsed))
                            logSeqVal = parsed;
                    }

                    // 3) AI 말풍선 추가 (logSeq 전달 → 피드백 버튼 표시용)
                    AppendAiBubble(isError, sAnswer, sGenSql, sRelTables, gridList, logSeqVal);

                    // 4) 이력 사이드바 추가
                    AddHistoryItem(sQuestion, DateTime.Now);

                    // 5) DataSet 행 추가 (DB저장용)
                    txtQuestion.Value = string.Empty;
                    dsWorking.E_DATARow dr = cWorking.E_DATA.NewE_DATARow();
                    dr.CUD_CHAR        = "I";
                    dr.PLANT_CD        = "";
                    dr.PLANT_NM        = "";
                    dr.SESSION_ID      = cSessionId;
                    dr.QUESTION        = sQuestion;
                    dr.AI_ANSWER       = sAnswer;
                    dr.GENERATED_SQL   = sGenSql;
                    dr.RELEVANT_TABLES = sRelTables;
                    dr.QUERY_DT        = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    dr.USE_YN          = "Y";
                    cWorking.E_DATA.AddE_DATARow(dr);
                }
                catch (Exception ex)
                {
                    RemoveLoadingPanel();
                    bool reThrow = ExceptionControler.AutoProcessException(ex);
                    if (reThrow) throw;
                }
                finally
                {
                    cIsLoading       = false;
                    btnAskAI.Enabled = true;
                }
            };
            bgw.RunWorkerAsync(jsonRequest);
        }

        private string PostWithTimeout(string url, string jsonBody, int timeoutMs)
        {
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.Method      = "POST";
            req.ContentType = "application/json; charset=utf-8";
            req.Accept      = "application/json";
            req.Timeout     = timeoutMs;
            req.Proxy       = null;
            byte[] bodyBytes = Encoding.UTF8.GetBytes(jsonBody);
            req.ContentLength = bodyBytes.Length;
            using (Stream reqStream = req.GetRequestStream())
                reqStream.Write(bodyBytes, 0, bodyBytes.Length);
            using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
            using (StreamReader sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                return sr.ReadToEnd();
        }

        private void CheckServerHealth()
        {
            // ── BackgroundWorker: 헬스체크를 백그라운드 스레드에서 실행 ──
            // 이유: Form_Load_Completed에서 호출 → UI 스레드 동기 실행 시
            //       폼이 화면에 나타나기 전 3초간 흰 화면 블록 현상 수정
            var bgw = new System.ComponentModel.BackgroundWorker();
            bgw.DoWork += (s, e) =>
            {
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(URL_HEALTH);
                req.Method  = "GET";
                req.Timeout = 3000;
                req.Proxy   = null;
                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                    e.Result = resp.StatusCode;
            };
            bgw.RunWorkerCompleted += (s, e) =>
            {
                if (e.Error != null || !(e.Result is HttpStatusCode sc) || sc != HttpStatusCode.OK)
                    ShowServerWarning();
            };
            bgw.RunWorkerAsync();
        }

        private void ShowServerWarning()
        {
            AppendAiBubble(true,
                "⚠ PA999S1 챗봇 서버에 연결할 수 없습니다.\n\n"
                + $"서버 주소: {URL_ASK}\n\n"
                + "[조치 방법]\n"
                + "① PA999S1 프로젝트를 Visual Studio에서 실행하거나\n"
                + "② app.config > ChatbotApiUrl 주소를 올바르게 수정하세요.",
                "", "", null);
        }
        #endregion

        #endregion

        #region ▶ 5. Event method part

        private void btnAskAI_Click(object sender, EventArgs e)   { AskAI(); }
        private void ModuleViewer_Load(object sender, EventArgs e) { }

        private void txtQuestion_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && !e.Shift)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                AskAI();
            }
            else if (e.KeyCode == Keys.F5)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                AskAI();
            }
        }

        private void btnResetSession_Click(object sender, EventArgs e)
        {
            // ── 상태 가드: AI 응답 처리 중 초기화 방지 ───────────────────────
            // 이유: BackgroundWorker 실행 중 Controls.Clear() 충돌 시 예외 발생
            if (cIsLoading)
            {
                uniBase.UMessage.DisplayMessageBox("DT9999", MessageBoxButtons.OK,
                    "AI 응답 처리 중입니다.\n완료 후 새 대화를 시작해 주세요.");
                return;
            }

            DialogResult dr = MessageBox.Show(
                "새 대화를 시작하시겠습니까?\n(이전 대화 맥락이 삭제됩니다)",
                "새 대화", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (dr != DialogResult.Yes) return;

            // ── 서버 세션 삭제 (BackgroundWorker — UI 스레드 블로킹 없음) ──
            // 이유: 서버 미응답 시 기존 동기 req.GetResponse()가 5초 UI 동결 유발
            string oldSessionId = cSessionId;
            var bgw = new System.ComponentModel.BackgroundWorker();
            bgw.DoWork += (s2, e2) =>
            {
                try
                {
                    var req = (HttpWebRequest)WebRequest.Create(URL_SESSION + "/" + oldSessionId);
                    req.Method  = "DELETE";
                    req.Timeout = 5000;
                    req.Proxy   = null;
                    using (var resp = req.GetResponse()) { }
                }
                catch { }
            };
            bgw.RunWorkerAsync();

            // 상태 초기화
            cSessionId        = Guid.NewGuid().ToString();
            txtQuestion.Value = string.Empty;
            _loadingPanel     = null;

            // 채팅 버블 초기화 + 높이 재설정 (Clear 후 Height가 남아있으면 빈 공간 발생)
            flpMessages.Controls.Clear();
            SyncFlpWidth();

            // 이력 사이드바 초기화
            pnlHistoryList.Controls.Clear();

            // ── 환영 말풍선: BeginInvoke로 다음 메시지 펌프 사이클에서 실행 ──
            // 이유: Controls.Clear()는 FlowLayoutPanel의 내부 레이아웃 엔진을
            //       비동기적으로 정리함. 동기 호출 시 레이아웃 엔진이 완전히
            //       정착되기 전에 Controls.Add()가 실행되어 NullReferenceException 발생.
            //       BeginInvoke로 현재 이벤트 핸들러가 완전히 종료된 후 실행 보장.
            this.BeginInvoke(new Action(() =>
            {
                try
                {
                    AppendAiBubble(false,
                        "새 대화가 시작되었습니다.\n무엇이든 질문해 보세요!\n\n"
                        + "예) 단양공장(P001) 이번달 생산량 알려줘\n"
                        + "예) 2024년 연간 생산실적을 월별로 보여줘",
                        "", "", null);
                }
                catch { /* 안전 무시: 드문 GDI 상태 전환 중 예외 */ }
            }));
        }

        #endregion

        #region ▶ 7. User-defined method part

        private bool IsNull_DBNull_Empty(object obj)
            => obj == null || obj == DBNull.Value || obj.ToString().Trim() == "";

        // ── 텍스트 높이 측정 (줄바꿈 포함, 100% 신뢰) ────────────────────
        // WordBreak 포함 정확한 줄바꿈 높이 (TextRenderer 사용)
        private static int MeasureH(string text, Font font, int maxWidth)
        {
            if (string.IsNullOrEmpty(text) || maxWidth <= 0) return 20;
            int h = TextRenderer.MeasureText(text, font,
                new Size(maxWidth, 99999),
                TextFormatFlags.WordBreak | TextFormatFlags.Left | TextFormatFlags.NoPadding).Height;
            return Math.Max(h + 6, 20);
        }

        // 단일 행 텍스트 너비 측정
        private static int MeasureW(string text, Font font)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            return TextRenderer.MeasureText(text, font, new Size(9999, 9999),
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width;
        }

        // ── 채팅 버블: flpMessages 너비 동기화 ──────────────────────────
        private void SyncFlpWidth()
        {
            int w = pnlChatScroll.ClientSize.Width;
            if (w > 0)
            {
                flpMessages.Width = w;
                flpMessages.MinimumSize = new Size(w, 0);
            }
            // 높이 초기화: pnlChatScroll 높이 이상 확보
            // AutoSize=false이므로 컨텐츠가 없을 때도 채팅 영역 전체를 채워야 함
            int h = pnlChatScroll.ClientSize.Height;
            if (h > 0 && flpMessages.Height < h)
                flpMessages.Height = h;
        }

        // ── flpMessages 에 패널 추가 후 높이 확장 + 스크롤 맨 아래로 ────
        private void AppendToFlp(Panel p)
        {
            int chatW = pnlChatScroll.ClientSize.Width;
            if (chatW < 200) chatW = flpMessages.Width > 200 ? flpMessages.Width : 800;
            p.Width = Math.Max(chatW - flpMessages.Padding.Horizontal, 200);
            flpMessages.Controls.Add(p);

            // ── flpMessages 높이 확장 ─────────────────────────────────
            // AutoSize=false 이면 Controls 추가 시 패널이 자동으로 커지지 않음
            // → 컨텐츠 총 높이를 수동 계산하여 Height를 갱신해야 말풍선이 보임
            int contentH = flpMessages.Padding.Top + flpMessages.Padding.Bottom;
            foreach (Control c in flpMessages.Controls)
                contentH += c.Height + c.Margin.Top + c.Margin.Bottom;
            int panelH = pnlChatScroll.ClientSize.Height;
            flpMessages.Height = Math.Max(contentH, panelH > 0 ? panelH : flpMessages.Height);

            // 스크롤 맨 아래: 마지막 컨트롤을 뷰에 노출
            flpMessages.PerformLayout();
            if (flpMessages.Controls.Count > 0)
                pnlChatScroll.ScrollControlIntoView(
                    flpMessages.Controls[flpMessages.Controls.Count - 1]);
        }

        // ── "분석 중..." 임시 패널 ──────────────────────────────────────
        private Panel CreateLoadingPanel()
        {
            var p = new Panel { BackColor = Color.Transparent, Height = 40, Margin = new Padding(0, 4, 0, 4) };
            var lbl = new Label
            {
                Text      = "UNIERP AI  |  분석 중입니다...",
                Font      = new Font("맑은 고딕", 9F, FontStyle.Italic),
                ForeColor = Color.FromArgb(120, 120, 140),
                AutoSize  = true,
                Location  = new Point(8, 10)
            };
            p.Controls.Add(lbl);
            return p;
        }

        private void RemoveLoadingPanel()
        {
            if (_loadingPanel != null && flpMessages.Controls.Contains(_loadingPanel))
            {
                flpMessages.Controls.Remove(_loadingPanel);
                _loadingPanel.Dispose();
            }
            _loadingPanel = null;
        }

        // ════════════════════════════════════════════════════════════
        // ■ 사용자 말풍선 추가
        // ════════════════════════════════════════════════════════════
        private void AppendUserBubble(string question)
        {
            int chatW = pnlChatScroll.ClientSize.Width;
            if (chatW < 200) chatW = flpMessages.Width > 200 ? flpMessages.Width : 800;
            int maxBubbleW = (int)(chatW * 0.68);

            var bubbleFont = new Font("맑은 고딕", 9.5F);
            // 실제 텍스트 너비로 말풍선 크기 결정 (maxBubbleW 초과 시 줄바꿈)
            int singleLineW = MeasureW(question, bubbleFont);
            int textW  = Math.Min(singleLineW, maxBubbleW - 28);
            textW      = Math.Max(textW, 60);
            int textH  = MeasureH(question, bubbleFont, textW);

            // 말풍선 내부 레이블
            var lbl = new Label
            {
                Text      = question,
                Font      = bubbleFont,
                ForeColor = Color.White,
                AutoSize  = false,
                Size      = new Size(textW, textH),
                Location  = new Point(12, 8)
            };

            // 파란 말풍선 패널
            var bubble = new Panel
            {
                BackColor = Color.FromArgb(0, 95, 184),
                Width     = textW + 24,
                Height    = textH + 18
            };
            bubble.Controls.Add(lbl);

            // 사용자명 레이블
            string uname = string.IsNullOrEmpty(_userName) ? "나" : _userName;
            var lblUser = new Label
            {
                Text      = uname,
                Font      = new Font("맑은 고딕", 7.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(80, 90, 120),
                AutoSize  = true
            };

            // 행 패널
            var row = new Panel
            {
                BackColor = Color.Transparent,
                Height    = bubble.Height + lblUser.PreferredHeight + 8,
                Margin    = new Padding(0, 4, 0, 4)
            };
            row.Controls.Add(bubble);
            row.Controls.Add(lblUser);

            Action align = () =>
            {
                if (row.IsDisposed || lblUser.IsDisposed || bubble.IsDisposed || row.Width <= 0) return;
                lblUser.Location = new Point(row.Width - lblUser.PreferredWidth - 14, 4);
                bubble.Location  = new Point(row.Width - bubble.Width - 12, lblUser.PreferredHeight + 6);
            };
            align();
            row.Resize += (s, e) => align();

            AppendToFlp(row);
        }

        // ════════════════════════════════════════════════════════════
        // ■ AI 말풍선 추가 (답변 + SQL섹션 + DataGridView)
        // ════════════════════════════════════════════════════════════
        private void AppendAiBubble(bool isError, string answer,
                                    string sql, string tables,
                                    System.Collections.ArrayList gridList,
                                    long? logSeq = null)
        {
            var aiColor = isError ? Color.FromArgb(160, 0, 0) : Color.FromArgb(20, 28, 50);
            var bgColor = isError ? Color.FromArgb(255, 248, 248) : Color.White;
            var timeStr = DateTime.Now.ToString("HH:mm");

            // ── 너비 계산 ──────────────────────────────────────────────
            int chatW  = pnlChatScroll.ClientSize.Width;
            if (chatW < 200) chatW = flpMessages.Width > 200 ? flpMessages.Width : 800;
            int innerW = Math.Max((int)(chatW * 0.92) - 36, 300);
            const int LP = 18; // left padding

            // ── AI 응답 카드 패널 ──────────────────────────────────────
            var pnlAi = new Panel { BackColor = bgColor, Margin = new Padding(0) };
            pnlAi.Paint += (s, e) =>
            {
                if (pnlAi.IsDisposed || pnlAi.Width <= 1 || pnlAi.Height <= 1) return;
                using (var pen = new Pen(Color.FromArgb(200, 207, 222), 1))
                    e.Graphics.DrawRectangle(pen, 0, 0, pnlAi.Width - 1, pnlAi.Height - 1);
                using (var accent = new Pen(Color.FromArgb(0, 95, 184), 1))
                    e.Graphics.DrawLine(accent, 0, 8, 0, pnlAi.Height - 8);
            };

            int y = 12;

            // ── AI 배지 + 헤더 ────────────────────────────────────────
            var lblBadge = new Label
            {
                Text      = "AI",
                Font      = new Font("맑은 고딕", 7.5F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(0, 95, 184),
                TextAlign = ContentAlignment.MiddleCenter,
                Size      = new Size(24, 17),
                Location  = new Point(LP, y + 1)
            };
            var lblHeader = new Label
            {
                Text      = $"UNIERP AI  ·  {timeStr}",
                Font      = new Font("맑은 고딕", 8.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 95, 184),
                AutoSize  = true,
                Location  = new Point(LP + 28, y)
            };
            pnlAi.Controls.Add(lblBadge);
            pnlAi.Controls.Add(lblHeader);
            y += 22;

            // 헤더 구분선
            pnlAi.Controls.Add(new Panel
            {
                BackColor = Color.FromArgb(220, 225, 238),
                Height    = 1,
                Width     = innerW,
                Location  = new Point(LP, y)
            });
            y += 10;

            // ── 답변 텍스트 (MeasureH로 정확한 높이 계산) ─────────────
            var answerFont = new Font("맑은 고딕", 10F, isError ? FontStyle.Regular : FontStyle.Bold);
            int answerH    = MeasureH(answer, answerFont, innerW);
            var lblAnswer  = new Label
            {
                Text      = answer,
                Font      = answerFont,
                ForeColor = aiColor,
                AutoSize  = false,
                Size      = new Size(innerW, answerH),
                Location  = new Point(LP, y)
            };
            pnlAi.Controls.Add(lblAnswer);
            y += answerH + 12;

            // ── SQL 섹션 ──────────────────────────────────────────────
            if (!string.IsNullOrWhiteSpace(sql))
            {
                string tableTag   = string.IsNullOrEmpty(tables) ? "" : "  ·  " + tables.Split(',')[0].Trim();
                string sqlForCopy = sql;

                var pnlSqlHeader = new Panel
                {
                    BackColor = Color.FromArgb(232, 239, 252),
                    Height    = 28,
                    Width     = innerW,
                    Location  = new Point(LP, y)
                };
                pnlSqlHeader.Controls.Add(new Label
                {
                    Text      = "생성된 SQL" + tableTag,
                    Font      = new Font("맑은 고딕", 8.5F, FontStyle.Bold),
                    ForeColor = Color.FromArgb(0, 70, 160),
                    AutoSize  = true,
                    Location  = new Point(8, 5)
                });
                var btnCopy = new Button
                {
                    Text      = "복사",
                    Font      = new Font("맑은 고딕", 8F),
                    FlatStyle = FlatStyle.Flat,
                    ForeColor = Color.FromArgb(0, 95, 184),
                    BackColor = Color.FromArgb(232, 239, 252),
                    Size      = new Size(44, 22),
                    Location  = new Point(innerW - 48, 3)
                };
                btnCopy.FlatAppearance.BorderColor = Color.FromArgb(170, 195, 230);
                btnCopy.FlatAppearance.BorderSize  = 1;
                btnCopy.Click += (s, e) => { try { Clipboard.SetText(sqlForCopy); } catch { } };
                pnlSqlHeader.Controls.Add(btnCopy);
                pnlAi.Controls.Add(pnlSqlHeader);
                y += pnlSqlHeader.Height;

                var rtbSql = new RichTextBox
                {
                    Text        = sql,
                    Font        = new Font("Consolas", 8.5F),
                    ForeColor   = Color.FromArgb(20, 60, 130),
                    BackColor   = Color.FromArgb(246, 249, 255),
                    BorderStyle = BorderStyle.None,
                    ReadOnly    = true,
                    ScrollBars  = RichTextBoxScrollBars.None,
                    Location    = new Point(LP, y),
                    Width       = innerW
                };
                int sqlLines  = sql.Split('\n').Length;
                rtbSql.Height = Math.Min(sqlLines * 17 + 10, 320);
                rtbSql.ContentsResized += (s, e) =>
                    rtbSql.Height = Math.Min(e.NewRectangle.Height + 8, 320);
                pnlAi.Controls.Add(rtbSql);
                y += rtbSql.Height + 12;
            }

            // ── DataGridView ──────────────────────────────────────────
            if (gridList != null && gridList.Count > 0)
            {
                int    totalRows = gridList.Count;
                string rowLabel  = totalRows > 5 ? $"5행 표시  (전체 {totalRows}행)" : $"{totalRows}행";

                pnlAi.Controls.Add(new Label
                {
                    Text      = $"조회 결과        {rowLabel}",
                    Font      = new Font("맑은 고딕", 8.5F, FontStyle.Bold),
                    ForeColor = Color.FromArgb(0, 70, 160),
                    AutoSize  = true,
                    Location  = new Point(LP, y)
                });
                y += 20;

                var dgv = BuildDataGridView(gridList, innerW);
                dgv.Location = new Point(LP, y);
                pnlAi.Controls.Add(dgv);
                y += dgv.Height + 6;
            }

            // ── 사용자 피드백 버튼 (GOOD / BAD) ──────────────────────
            if (logSeq.HasValue && logSeq.Value > 0 && !isError)
            {
                var btnGood = new Button
                {
                    Text      = "GOOD",
                    Font      = new Font("맑은 고딕", 7.5F, FontStyle.Bold),
                    FlatStyle = FlatStyle.Flat,
                    ForeColor = Color.FromArgb(34, 139, 60),
                    BackColor = Color.FromArgb(235, 250, 240),
                    Size      = new Size(62, 24),
                    Location  = new Point(LP, y),
                    Tag       = logSeq.Value,
                    Cursor    = Cursors.Hand
                };
                btnGood.FlatAppearance.BorderColor = Color.FromArgb(180, 220, 180);
                btnGood.FlatAppearance.BorderSize  = 1;

                var btnBad = new Button
                {
                    Text      = "BAD",
                    Font      = new Font("맑은 고딕", 7.5F, FontStyle.Bold),
                    FlatStyle = FlatStyle.Flat,
                    ForeColor = Color.FromArgb(180, 40, 40),
                    BackColor = Color.FromArgb(255, 242, 242),
                    Size      = new Size(56, 24),
                    Location  = new Point(LP + 68, y),
                    Tag       = logSeq.Value,
                    Cursor    = Cursors.Hand
                };
                btnBad.FlatAppearance.BorderColor = Color.FromArgb(220, 180, 180);
                btnBad.FlatAppearance.BorderSize  = 1;

                btnGood.Click += (s, ev) => SubmitUserFeedback(btnGood, btnBad, logSeq.Value, 4);
                btnBad.Click  += (s, ev) => SubmitUserFeedback(btnGood, btnBad, logSeq.Value, 2);

                pnlAi.Controls.Add(btnGood);
                pnlAi.Controls.Add(btnBad);
                y += 30;
            }

            // ── 패널 높이: Controls.Bottom 최대값 기준 (가장 신뢰) ──────
            int finalH = y + 14;
            foreach (Control c in pnlAi.Controls)
                if (c.Bottom + 14 > finalH) finalH = c.Bottom + 14;
            pnlAi.Height = finalH;

            var row = new Panel
            {
                BackColor = Color.Transparent,
                Height    = pnlAi.Height + 8,
                Margin    = new Padding(0, 2, 0, 8)
            };
            pnlAi.Location = new Point(8, 4);
            row.Controls.Add(pnlAi);
            row.Resize += (s, e) =>
            {
                if (!row.IsDisposed && !pnlAi.IsDisposed && row.Width > 0)
                    pnlAi.Width = Math.Max(row.Width - 22, 300);
            };

            AppendToFlp(row);
        }

        // ── DataGridView 생성 ─────────────────────────────────────────
        private DataGridView BuildDataGridView(System.Collections.ArrayList gridList, int maxWidth)
        {
            var dt = new DataTable();
            bool headersSet = false;
            int  rowsToShow = Math.Min(gridList.Count, 500); // 최대 500행 (TOP 50 제한 해제)

            foreach (var item in gridList)
            {
                if (!(item is Dictionary<string, object> row)) continue;
                if (!headersSet)
                {
                    foreach (var key in row.Keys)
                        dt.Columns.Add(key);
                    headersSet = true;
                }
                DataRow dr = dt.NewRow();
                foreach (var kv in row)
                    if (dt.Columns.Contains(kv.Key))
                        dr[kv.Key] = kv.Value ?? DBNull.Value;
                dt.Rows.Add(dr);
                if (dt.Rows.Count >= rowsToShow) break;
            }

            var dgv = new DataGridView
            {
                DataSource                    = dt,
                AllowUserToAddRows            = false,
                AllowUserToDeleteRows         = false,
                ReadOnly                      = true,
                AutoSizeColumnsMode           = DataGridViewAutoSizeColumnsMode.AllCells,
                ColumnHeadersHeightSizeMode   = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
                BackgroundColor               = Color.FromArgb(248, 250, 253),
                BorderStyle                   = BorderStyle.FixedSingle,
                Font                          = new Font("맑은 고딕", 8.5F),
                RowHeadersVisible             = false,
                SelectionMode                 = DataGridViewSelectionMode.FullRowSelect,
                Width                         = maxWidth,
                ScrollBars                    = ScrollBars.Horizontal
            };
            // 높이: 헤더 + 최대 8행
            int rowH   = dgv.RowTemplate.Height;
            int hdrH   = dgv.ColumnHeadersHeight;
            int visRows = Math.Min(dt.Rows.Count, 8);
            dgv.Height  = hdrH + visRows * rowH + 2;

            // 컬럼 헤더 스타일 (파란 배경)
            dgv.ColumnHeadersDefaultCellStyle.BackColor  = Color.FromArgb(0, 95, 184);
            dgv.ColumnHeadersDefaultCellStyle.ForeColor  = Color.White;
            dgv.ColumnHeadersDefaultCellStyle.Font       = new Font("맑은 고딕", 8.5F, FontStyle.Bold);
            dgv.ColumnHeadersDefaultCellStyle.Alignment  = DataGridViewContentAlignment.MiddleCenter;
            dgv.EnableHeadersVisualStyles = false;

            // 홀짝 행 색상
            dgv.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(245, 249, 255);

            return dgv;
        }

        // ── 이력 사이드바 아이템 추가 ────────────────────────────────────
        private void AddHistoryItem(string question, DateTime dt, string aiResponse = "")
        {
            string shortQ  = question.Length > 18 ? question.Substring(0, 18) + "..." : question;
            // 오늘이면 시간만, 과거면 날짜+시간
            string timeStr = (dt.Date == DateTime.Today)
                ? dt.ToString("HH:mm")
                : dt.ToString("MM/dd HH:mm");

            // 카테고리 감지
            string category; Color catColor;
            if (question.Contains("생산") || question.Contains("공장") || question.Contains("레미콘") || question.Contains("PROD"))
            { category = "P_생산"; catColor = Color.FromArgb(0, 95, 184); }
            else if (question.Contains("매출") || question.Contains("영업") || question.Contains("수주") || question.Contains("판매"))
            { category = "S_영업"; catColor = Color.FromArgb(34, 139, 60); }
            else if (question.Contains("구매") || question.Contains("발주") || question.Contains("자재"))
            { category = "M_구매"; catColor = Color.FromArgb(160, 80, 0); }
            else if (question.Contains("품질") || question.Contains("불량") || question.Contains("검사"))
            { category = "Q_품질"; catColor = Color.FromArgb(140, 20, 140); }
            else
            { category = "기 타"; catColor = Color.FromArgb(100, 108, 130); }

            // Tag에 질문+답변 쌍 저장 (클릭 시 대화 표시용)
            var pnlItem = new Panel
            {
                BackColor = Color.White,
                Height    = 60,
                Dock      = DockStyle.Top,
                Cursor    = Cursors.Hand,
                Tag       = new string[] { question, aiResponse }
            };
            pnlItem.Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(208, 212, 222), 1))
                    e.Graphics.DrawLine(pen, 0, pnlItem.Height - 1, pnlItem.Width, pnlItem.Height - 1);
            };
            pnlItem.MouseEnter += (s, e) => pnlItem.BackColor = Color.FromArgb(232, 238, 250);
            pnlItem.MouseLeave += (s, e) => pnlItem.BackColor = Color.White;

            var lblQ = new Label
            {
                Text      = shortQ,
                Font      = new Font("맑은 고딕", 8.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(25, 32, 55),
                AutoSize  = false,
                Size      = new Size(165, 18),
                Location  = new Point(10, 8),
                TextAlign = ContentAlignment.MiddleLeft
            };
            var lblTime = new Label
            {
                Text      = timeStr,
                Font      = new Font("맑은 고딕", 7.5F),
                ForeColor = Color.FromArgb(130, 138, 160),
                AutoSize  = true,
                Location  = new Point(10, 30)
            };
            var lblCat = new Label
            {
                Text      = category,
                Font      = new Font("맑은 고딕", 7F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = catColor,
                AutoSize  = false,
                Size      = new Size(44, 15),
                TextAlign = ContentAlignment.MiddleCenter,
                Location  = new Point(130, 32)
            };

            pnlItem.Controls.Add(lblQ);
            pnlItem.Controls.Add(lblTime);
            pnlItem.Controls.Add(lblCat);

            pnlItem.Click  += HistoryItem_Click;
            lblQ.Click     += HistoryItem_Click;
            lblTime.Click  += HistoryItem_Click;
            lblCat.Click   += HistoryItem_Click;

            pnlHistoryList.Controls.Add(pnlItem);
            pnlItem.BringToFront();
        }

        private void HistoryItem_Click(object sender, EventArgs e)
        {
            Control c = sender as Control;
            if (c == null) return;
            Panel p = (c is Panel) ? (Panel)c : c.Parent as Panel;
            if (p?.Tag == null) return;

            string[] pair = p.Tag as string[];
            if (pair == null || pair.Length < 2) return;

            string question   = pair[0] ?? "";
            string aiResponse = pair[1] ?? "";

            // 채팅 영역에 해당 대화 표시
            flpMessages.SuspendLayout();
            foreach (Control ctrl in flpMessages.Controls)
                ctrl.Dispose();
            flpMessages.Controls.Clear();
            flpMessages.ResumeLayout(true);

            // 사용자 질문 버블
            AppendUserBubble(question);

            // AI 답변 버블 (답변이 있는 경우)
            if (!string.IsNullOrWhiteSpace(aiResponse))
                AppendAiBubble(false, aiResponse, "", "", null);

            // 질문 입력란에도 질문 채워넣기 (재질문 편의)
            txtQuestion.Value = question;
        }

        // ── 이력 헤더 하단 구분선 Paint ──────────────────────────────────
        private void PnlHistoryHeader_Paint(object sender, PaintEventArgs e)
        {
            if (pnlHistoryHeader.IsDisposed || pnlHistoryHeader.Width <= 0) return;
            // 1px 구분선 — 헤더와 이력 목록 영역 구분
            using (var pen = new Pen(Color.FromArgb(0, 95, 184), 1))
                e.Graphics.DrawLine(pen, 0, pnlHistoryHeader.Height - 1,
                    pnlHistoryHeader.Width, pnlHistoryHeader.Height - 1);
        }

        #region ■ BindAiDataGrid (하위호환 — 현재 미사용)
        private void BindAiDataGrid(System.Collections.ArrayList gridList) { }
        #endregion

        #region ■ SubmitUserFeedback (현업 GOOD/BAD)
        /// <summary>
        /// 현업 사용자 피드백 전송 (GOOD=4점, BAD=2점)
        /// BackgroundWorker로 비동기 PATCH 호출 → UI 블로킹 없음
        /// </summary>
        private void SubmitUserFeedback(Button btnGood, Button btnBad, long logSeq, int score)
        {
            btnGood.Enabled = false;
            btnBad.Enabled  = false;

            // 선택된 버튼 하이라이트, 반대쪽 숨김
            if (score == 4)
            {
                btnGood.BackColor = Color.FromArgb(34, 139, 60);
                btnGood.ForeColor = Color.White;
                btnGood.Text      = "GOOD ✓";
                btnBad.Visible    = false;
            }
            else
            {
                btnBad.BackColor = Color.FromArgb(180, 40, 40);
                btnBad.ForeColor = Color.White;
                btnBad.Text      = "BAD ✓";
                btnGood.Visible  = false;
            }

            string label = score == 4 ? "[USER] GOOD" : "[USER] BAD";
            var feedbackObj = new Dictionary<string, object>
            {
                { "perfScore",    score },
                { "devFeedback",  label },
                { "feedbackBy",   CommonVariable.gUsrID ?? "" },
                { "feedbackType", "U" }
            };
            string jsonBody = new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(feedbackObj);
            string url = URL_FEEDBACK + "/" + logSeq + "/feedback";

            var bgw = new System.ComponentModel.BackgroundWorker();
            bgw.DoWork += (s, ev) =>
            {
                try
                {
                    ev.Result = PatchFeedback(url, jsonBody, 10000);
                }
                catch (Exception ex)
                {
                    ev.Result = "ERROR:" + ex.Message;
                }
            };
            bgw.RunWorkerCompleted += (s, ev) =>
            {
                try
                {
                    string result = ev.Result?.ToString() ?? "";
                    if (ev.Error != null || result.StartsWith("ERROR:"))
                    {
                        // 실패 시 버튼 복원 (재시도 가능)
                        if (!btnGood.IsDisposed) { btnGood.Enabled = true; btnGood.Visible = true;
                            btnGood.BackColor = Color.FromArgb(235, 250, 240);
                            btnGood.ForeColor = Color.FromArgb(34, 139, 60); btnGood.Text = "GOOD"; }
                        if (!btnBad.IsDisposed) { btnBad.Enabled = true; btnBad.Visible = true;
                            btnBad.BackColor = Color.FromArgb(255, 242, 242);
                            btnBad.ForeColor = Color.FromArgb(180, 40, 40); btnBad.Text = "BAD"; }
                    }
                }
                catch { /* 폼이 이미 닫힌 경우 무시 */ }
            };
            bgw.RunWorkerAsync();
        }

        /// <summary>PATCH HTTP 헬퍼 (피드백 전송용)</summary>
        private string PatchFeedback(string url, string jsonBody, int timeoutMs)
        {
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.Method      = "PATCH";
            req.ContentType = "application/json; charset=utf-8";
            req.Accept      = "application/json";
            req.Timeout     = timeoutMs;
            req.Proxy       = null;
            byte[] bodyBytes = System.Text.Encoding.UTF8.GetBytes(jsonBody);
            req.ContentLength = bodyBytes.Length;
            using (System.IO.Stream reqStream = req.GetRequestStream())
                reqStream.Write(bodyBytes, 0, bodyBytes.Length);
            using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
            using (System.IO.StreamReader sr = new System.IO.StreamReader(
                resp.GetResponseStream(), System.Text.Encoding.UTF8))
                return sr.ReadToEnd();
        }
        #endregion

        #region ■ LoadChatHistory (기존 대화 이력 로드)
        /// <summary>
        /// PA999_CHAT_LOG에서 로그인 사용자의 최근 대화 이력을 조회하여
        /// 사이드바에 표시. 현재 세션 외 과거 세션 질문도 모두 포함.
        /// </summary>
        private void LoadChatHistory()
        {
            try
            {
                string sSafeId = (CommonVariable.gUsrID ?? "").Replace("'", "''");
                if (string.IsNullOrEmpty(sSafeId)) return;

                string sql = string.Format(@"
                    SELECT TOP 50
                        LOG_SEQ,
                        USER_QUERY,
                        AI_RESPONSE,
                        CONVERT(NVARCHAR(19), CREATED_DT, 120) AS CREATED_DT,
                        SESSION_ID
                    FROM PA999_CHAT_LOG (NOLOCK)
                    WHERE USER_ID = '{0}'
                      AND IS_ERROR = 0
                      AND USER_QUERY IS NOT NULL
                      AND LEN(USER_QUERY) > 0
                    ORDER BY LOG_SEQ DESC", sSafeId);

                DataSet ds = uniBase.UDataAccess.CommonQuerySQL(sql);
                if (ds == null || ds.Tables.Count == 0 || ds.Tables[0].Rows.Count == 0)
                    return;

                // 사이드바에 기존 이력 추가 (최신순) — 질문+답변 쌍으로 저장
                foreach (DataRow r in ds.Tables[0].Rows)
                {
                    string query    = r["USER_QUERY"]?.ToString() ?? "";
                    string response = r["AI_RESPONSE"]?.ToString() ?? "";
                    if (string.IsNullOrWhiteSpace(query)) continue;

                    string dtStr = r["CREATED_DT"]?.ToString() ?? "";
                    DateTime dt;
                    if (!DateTime.TryParse(dtStr, out dt))
                        dt = DateTime.Now;

                    AddHistoryItem(query, dt, response);
                }
            }
            catch { /* 이력 로드 실패해도 챗봇 기능에 영향 없음 */ }
        }
        #endregion

        #region ■ FetchUserOrgInfo
        private void FetchUserOrgInfo()
        {
            try
            {
                string sSafeId = (CommonVariable.gUsrID ?? "").Replace("'", "''");
                if (string.IsNullOrEmpty(sSafeId)) return;

                string sql = string.Format(@"
                    SELECT TOP 1 ORG_TYPE, ORG_CD
                    FROM Z_USR_ORG_MAST (NOLOCK)
                    WHERE USR_ID = '{0}' AND USE_YN = 'Y'
                    ORDER BY ORG_TYPE DESC", sSafeId);

                DataSet ds = uniBase.UDataAccess.CommonQuerySQL(sql);
                if (ds != null && ds.Tables.Count > 0 && ds.Tables[0].Rows.Count > 0)
                {
                    DataRow r = ds.Tables[0].Rows[0];
                    _orgType  = IsNull_DBNull_Empty(r["ORG_TYPE"]) ? "" : r["ORG_TYPE"].ToString().Trim();
                    _orgCd    = IsNull_DBNull_Empty(r["ORG_CD"])   ? "" : r["ORG_CD"].ToString().Trim();
                    _userName = (CommonVariable.gUsrID ?? "").Trim();
                }
            }
            catch { _orgType = ""; _orgCd = ""; }
        }
        #endregion

        #endregion
    }
}
