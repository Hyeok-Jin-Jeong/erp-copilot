namespace Bizentro.App.UI.PP.CB990M1_CKO087
{
    partial class ModuleViewer
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            try
            {
                if (disposing && (components != null)) components.Dispose();
            }
            catch { }
            try { base.Dispose(disposing); } catch { }
        }

        #region Component Designer generated code
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            Infragistics.Win.Appearance appearance1 = new Infragistics.Win.Appearance();
            Infragistics.Win.Appearance appearance2 = new Infragistics.Win.Appearance();
            Infragistics.Win.Appearance appearance3 = new Infragistics.Win.Appearance();

            // ── 컨트롤 인스턴스화 ──────────────────────────────────────────
            // [검색 패널]
            this.pnlSearch        = new System.Windows.Forms.Panel();
            this.tlpSearch        = new System.Windows.Forms.TableLayoutPanel();
            this.lblFrom          = new System.Windows.Forms.Label();
            this.uniDtmFrom       = new Infragistics.Win.UltraWinEditors.UltraDateTimeEditor();
            this.lblTilde         = new System.Windows.Forms.Label();
            this.uniDtmTo         = new Infragistics.Win.UltraWinEditors.UltraDateTimeEditor();
            this.lblUser          = new System.Windows.Forms.Label();
            this.txtUsrId         = new Bizentro.AppFramework.UI.Controls.uniTextBox(this.components);

            // [하단 평가 입력 패널]
            this.pnlEvalBar       = new System.Windows.Forms.Panel();
            this.tlpEval          = new System.Windows.Forms.TableLayoutPanel();
            this.lblSelInfo       = new System.Windows.Forms.Label();
            this.lblScoreLabel    = new System.Windows.Forms.Label();
            this.txtScore         = new System.Windows.Forms.TextBox();
            this.lblFeedbackLabel = new System.Windows.Forms.Label();
            this.txtFeedback      = new System.Windows.Forms.TextBox();
            this.lblSqlLabel      = new System.Windows.Forms.Label();
            this.txtCorrectSql   = new System.Windows.Forms.TextBox();
            this.btnSave          = new Infragistics.Win.Misc.UltraButton();
            this.btnRegPattern    = new Infragistics.Win.Misc.UltraButton();

            // [메인 분할: 채팅(위) + 그리드(아래)]
            this.splitMain        = new System.Windows.Forms.SplitContainer();
            this.pnlChatScroll    = new System.Windows.Forms.Panel();
            this.flpMessages      = new System.Windows.Forms.FlowLayoutPanel();
            this.pnlGridContainer = new System.Windows.Forms.Panel();
            this.pnlGridHeader    = new System.Windows.Forms.Panel();
            this.lblGridTitle     = new System.Windows.Forms.Label();
            this.uniGrid1         = new Bizentro.AppFramework.UI.Controls.uniGrid(this.components);

            // ── SuspendLayout ──────────────────────────────────────────────
            this.pnlSearch.SuspendLayout();
            this.tlpSearch.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.uniDtmFrom)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.uniDtmTo)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.txtUsrId)).BeginInit();
            this.pnlEvalBar.SuspendLayout();
            this.tlpEval.SuspendLayout();
            // txtScore, txtFeedback: 표준 TextBox — BeginInit 불필요
            ((System.ComponentModel.ISupportInitialize)(this.splitMain)).BeginInit();
            this.splitMain.Panel1.SuspendLayout();
            this.splitMain.Panel2.SuspendLayout();
            this.splitMain.SuspendLayout();
            this.pnlChatScroll.SuspendLayout();
            this.pnlGridContainer.SuspendLayout();
            this.pnlGridHeader.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.uniGrid1)).BeginInit();
            this.SuspendLayout();

            // ── uniLabel_Path (ViewBase 기반 컨트롤) ─────────────────────
            this.uniLabel_Path.Size = new System.Drawing.Size(571, 10);

            // ════════════════════════════════════════════════════════════
            //  pnlSearch — 상단 검색 조건 패널
            // ════════════════════════════════════════════════════════════
            this.pnlSearch.Controls.Add(this.tlpSearch);
            this.pnlSearch.Dock      = System.Windows.Forms.DockStyle.Top;
            this.pnlSearch.Location  = new System.Drawing.Point(1, 12);
            this.pnlSearch.Name      = "pnlSearch";
            this.pnlSearch.Size      = new System.Drawing.Size(1400, 44);
            this.pnlSearch.TabIndex  = 0;

            // tlpSearch
            this.tlpSearch.ColumnCount = 7;
            this.tlpSearch.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 80F));
            this.tlpSearch.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 112F));
            this.tlpSearch.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 16F));
            this.tlpSearch.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 112F));
            this.tlpSearch.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 65F));
            this.tlpSearch.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 120F));
            this.tlpSearch.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tlpSearch.Controls.Add(this.lblFrom,    0, 0);
            this.tlpSearch.Controls.Add(this.uniDtmFrom, 1, 0);
            this.tlpSearch.Controls.Add(this.lblTilde,   2, 0);
            this.tlpSearch.Controls.Add(this.uniDtmTo,   3, 0);
            this.tlpSearch.Controls.Add(this.lblUser,    4, 0);
            this.tlpSearch.Controls.Add(this.txtUsrId,   5, 0);
            this.tlpSearch.Dock      = System.Windows.Forms.DockStyle.Fill;
            this.tlpSearch.Location  = new System.Drawing.Point(0, 0);
            this.tlpSearch.Name      = "tlpSearch";
            this.tlpSearch.RowCount  = 1;
            this.tlpSearch.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tlpSearch.Size      = new System.Drawing.Size(1400, 44);
            this.tlpSearch.TabIndex  = 0;

            // lblFrom
            this.lblFrom.Dock      = System.Windows.Forms.DockStyle.Fill;
            this.lblFrom.Name      = "lblFrom";
            this.lblFrom.Padding   = new System.Windows.Forms.Padding(0, 0, 4, 0);
            this.lblFrom.Text      = "조회기간";
            this.lblFrom.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.lblFrom.TabIndex  = 0;

            // uniDtmFrom
            this.uniDtmFrom.Dock     = System.Windows.Forms.DockStyle.Fill;
            this.uniDtmFrom.Margin   = new System.Windows.Forms.Padding(0, 8, 0, 8);
            this.uniDtmFrom.Name     = "uniDtmFrom";
            this.uniDtmFrom.TabIndex = 1;

            // lblTilde
            this.lblTilde.Dock      = System.Windows.Forms.DockStyle.Fill;
            this.lblTilde.Name      = "lblTilde";
            this.lblTilde.Text      = "~";
            this.lblTilde.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.lblTilde.TabIndex  = 2;

            // uniDtmTo
            this.uniDtmTo.Dock     = System.Windows.Forms.DockStyle.Fill;
            this.uniDtmTo.Margin   = new System.Windows.Forms.Padding(0, 8, 4, 8);
            this.uniDtmTo.Name     = "uniDtmTo";
            this.uniDtmTo.TabIndex = 3;

            // lblUser
            this.lblUser.Dock      = System.Windows.Forms.DockStyle.Fill;
            this.lblUser.Name      = "lblUser";
            this.lblUser.Padding   = new System.Windows.Forms.Padding(0, 0, 4, 0);
            this.lblUser.Text      = "사용자";
            this.lblUser.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.lblUser.TabIndex  = 4;

            // txtUsrId
            appearance1.TextVAlignAsString      = "Bottom";
            this.txtUsrId.Appearance            = appearance1;
            this.txtUsrId.AutoSize              = false;
            this.txtUsrId.FieldType             = Bizentro.AppFramework.UI.Variables.enumDef.FieldType.Default;
            this.txtUsrId.Margin                = new System.Windows.Forms.Padding(0, 8, 0, 8);
            this.txtUsrId.LockedField           = false;
            this.txtUsrId.Name                  = "txtUsrId";
            this.txtUsrId.QueryIfEnterKeyPressed = true;
            this.txtUsrId.RequiredField         = false;
            this.txtUsrId.Size                  = new System.Drawing.Size(120, 28);
            this.txtUsrId.Style                 = Bizentro.AppFramework.UI.Controls.TextBox_Style.Default;
            this.txtUsrId.StyleSetName          = "Default";
            this.txtUsrId.TabIndex              = 5;
            this.txtUsrId.uniALT                = null;
            this.txtUsrId.uniCharacterCasing    = System.Windows.Forms.CharacterCasing.Normal;
            this.txtUsrId.UseDynamicFormat      = false;

            // ════════════════════════════════════════════════════════════
            //  pnlEvalBar — 하단 평가 입력 패널 (고정 90px)
            // ════════════════════════════════════════════════════════════
            this.pnlEvalBar.Controls.Add(this.tlpEval);
            this.pnlEvalBar.Dock      = System.Windows.Forms.DockStyle.Bottom;
            this.pnlEvalBar.Name      = "pnlEvalBar";
            this.pnlEvalBar.Size      = new System.Drawing.Size(1400, 90);
            this.pnlEvalBar.TabIndex  = 1;
            this.pnlEvalBar.BackColor = System.Drawing.Color.FromArgb(240, 241, 246);
            this.pnlEvalBar.Paint    += new System.Windows.Forms.PaintEventHandler(this.pnlEvalBar_Paint);

            // tlpEval — 2행 레이아웃: 상단(점수+메모) / 하단(SQL+교훈+버튼)
            this.tlpEval.ColumnCount = 9;
            this.tlpEval.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 170F));   // col 0: 선택항목 정보
            this.tlpEval.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 60F));    // col 1: 점수 label
            this.tlpEval.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 42F));    // col 2: txtScore
            this.tlpEval.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 62F));    // col 3: 메모 label
            this.tlpEval.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent,  25F));    // col 4: txtFeedback
            this.tlpEval.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 72F));    // col 5: SQL label
            this.tlpEval.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent,  45F));    // col 6: txtCorrectSql
            this.tlpEval.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 82F));    // col 7: btnSave
            this.tlpEval.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 100F));   // col 8: btnRegPattern
            this.tlpEval.Controls.Add(this.lblSelInfo,       0, 0);
            this.tlpEval.Controls.Add(this.lblScoreLabel,    1, 0);
            this.tlpEval.Controls.Add(this.txtScore,         2, 0);
            this.tlpEval.Controls.Add(this.lblFeedbackLabel, 3, 0);
            this.tlpEval.Controls.Add(this.txtFeedback,      4, 0);
            this.tlpEval.Controls.Add(this.lblSqlLabel,      5, 0);
            this.tlpEval.Controls.Add(this.txtCorrectSql,    6, 0);
            this.tlpEval.Controls.Add(this.btnSave,          7, 0);
            this.tlpEval.Controls.Add(this.btnRegPattern,    8, 0);
            this.tlpEval.Dock      = System.Windows.Forms.DockStyle.Fill;
            this.tlpEval.Location  = new System.Drawing.Point(0, 0);
            this.tlpEval.Name      = "tlpEval";
            this.tlpEval.Padding   = new System.Windows.Forms.Padding(8, 10, 8, 10);
            this.tlpEval.RowCount  = 1;
            this.tlpEval.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tlpEval.TabIndex  = 0;

            // lblSelInfo — 선택된 항목 요약 (읽기 전용)
            this.lblSelInfo.Dock      = System.Windows.Forms.DockStyle.Fill;
            this.lblSelInfo.Font      = new System.Drawing.Font("맑은 고딕", 8F);
            this.lblSelInfo.ForeColor = System.Drawing.Color.FromArgb(80, 90, 110);
            this.lblSelInfo.Name      = "lblSelInfo";
            this.lblSelInfo.Padding   = new System.Windows.Forms.Padding(4, 0, 8, 0);
            this.lblSelInfo.Text      = "항목을 선택하면 여기에 정보가 표시됩니다.";
            this.lblSelInfo.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.lblSelInfo.TabIndex  = 0;

            // lblScoreLabel
            this.lblScoreLabel.Dock      = System.Windows.Forms.DockStyle.Fill;
            this.lblScoreLabel.Font      = new System.Drawing.Font("맑은 고딕", 8.25F);
            this.lblScoreLabel.Name      = "lblScoreLabel";
            this.lblScoreLabel.Padding   = new System.Windows.Forms.Padding(0, 0, 4, 0);
            this.lblScoreLabel.Text      = "평가점수\r\n(1~5점)";
            this.lblScoreLabel.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.lblScoreLabel.TabIndex  = 1;

            // txtScore — 표준 TextBox
            this.txtScore.Dock      = System.Windows.Forms.DockStyle.Fill;
            this.txtScore.Margin    = new System.Windows.Forms.Padding(0, 10, 8, 10);
            this.txtScore.Name      = "txtScore";
            this.txtScore.Font      = new System.Drawing.Font("맑은 고딕", 9F);
            this.txtScore.TabIndex  = 2;

            // lblFeedbackLabel
            this.lblFeedbackLabel.Dock      = System.Windows.Forms.DockStyle.Fill;
            this.lblFeedbackLabel.Font      = new System.Drawing.Font("맑은 고딕", 8.25F);
            this.lblFeedbackLabel.Name      = "lblFeedbackLabel";
            this.lblFeedbackLabel.Padding   = new System.Windows.Forms.Padding(0, 0, 4, 0);
            this.lblFeedbackLabel.Text      = "피드백 메모";
            this.lblFeedbackLabel.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.lblFeedbackLabel.TabIndex  = 3;

            // txtFeedback — 표준 TextBox
            this.txtFeedback.Dock      = System.Windows.Forms.DockStyle.Fill;
            this.txtFeedback.Margin    = new System.Windows.Forms.Padding(0, 10, 4, 10);
            this.txtFeedback.Name      = "txtFeedback";
            this.txtFeedback.Font      = new System.Drawing.Font("맑은 고딕", 8.5F);
            this.txtFeedback.TabIndex  = 4;

            // lblSqlLabel
            this.lblSqlLabel.Dock      = System.Windows.Forms.DockStyle.Fill;
            this.lblSqlLabel.Font      = new System.Drawing.Font("맑은 고딕", 8.25F);
            this.lblSqlLabel.Name      = "lblSqlLabel";
            this.lblSqlLabel.Padding   = new System.Windows.Forms.Padding(0, 0, 2, 0);
            this.lblSqlLabel.Text      = "올바른SQL\r\n/교훈";
            this.lblSqlLabel.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.lblSqlLabel.TabIndex  = 20;

            // txtCorrectSql — 표준 TextBox 사용 (uniTextBox 프레임워크 라이프사이클 이슈 회피)
            this.txtCorrectSql.Dock      = System.Windows.Forms.DockStyle.Fill;
            this.txtCorrectSql.Margin    = new System.Windows.Forms.Padding(0, 10, 4, 10);
            this.txtCorrectSql.Name      = "txtCorrectSql";
            this.txtCorrectSql.Font      = new System.Drawing.Font("맑은 고딕", 8.5F);
            this.txtCorrectSql.TabIndex  = 21;

            // btnSave
            this.btnSave.Dock     = System.Windows.Forms.DockStyle.Fill;
            this.btnSave.Margin   = new System.Windows.Forms.Padding(0, 8, 4, 8);
            this.btnSave.Name     = "btnSave";
            this.btnSave.TabIndex = 5;
            this.btnSave.Text     = "저장 (F4)";
            this.btnSave.Click   += new System.EventHandler(this.btnSave_Click);

            // btnRegPattern
            this.btnRegPattern.Dock     = System.Windows.Forms.DockStyle.Fill;
            this.btnRegPattern.Margin   = new System.Windows.Forms.Padding(0, 8, 4, 8);
            this.btnRegPattern.Name     = "btnRegPattern";
            this.btnRegPattern.TabIndex = 6;
            this.btnRegPattern.Text     = "AI 패턴 등록";
            this.btnRegPattern.Click   += new System.EventHandler(this.btnRegPattern_Click);

            // ════════════════════════════════════════════════════════════
            //  표준 UNIERP 그리드 레이아웃 (채팅 UI 제거)
            //  pnlSearch(Top) → uniGrid1(Fill) → pnlEvalBar(Bottom)
            // ════════════════════════════════════════════════════════════

            // splitMain — 사용하지 않음 (컴파일 호환용 최소 설정)
            this.splitMain.Dock    = System.Windows.Forms.DockStyle.None;
            this.splitMain.Visible = false;
            this.splitMain.Name    = "splitMain";
            this.splitMain.Size    = new System.Drawing.Size(0, 0);

            // ── uniGrid1 ─────────────────────────────────────────────────
            this.uniGrid1.AddEmptyRow   = false;
            this.uniGrid1.DirectPaste   = false;
            this.uniGrid1.DisplayLayout.Override.AllowAddNew      = Infragistics.Win.UltraWinGrid.AllowAddNew.No;
            this.uniGrid1.DisplayLayout.Override.CellClickAction  = Infragistics.Win.UltraWinGrid.CellClickAction.CellSelect;
            this.uniGrid1.Dock          = System.Windows.Forms.DockStyle.Fill;
            this.uniGrid1.EnableContextMenu       = true;
            this.uniGrid1.EnableGridFilterMenu    = false;
            this.uniGrid1.EnableGridInfoContextMenu = true;
            this.uniGrid1.ExceptInExcel = false;
            this.uniGrid1.Font          = new System.Drawing.Font("Arial", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.uniGrid1.gComNumDec    = Bizentro.AppFramework.UI.Variables.enumDef.ComDec.Decimal;
            this.uniGrid1.GridStyle     = Bizentro.AppFramework.UI.Variables.enumDef.GridStyle.Master;
            this.uniGrid1.Margin        = new System.Windows.Forms.Padding(0);
            this.uniGrid1.Name          = "uniGrid1";
            this.uniGrid1.OutlookGroupBy      = Bizentro.AppFramework.UI.Variables.enumDef.IsOutlookGroupBy.No;
            this.uniGrid1.PopupDeleteMenuVisible = true;
            this.uniGrid1.PopupInsertMenuVisible = true;
            this.uniGrid1.PopupUndoMenuVisible   = true;
            this.uniGrid1.Search        = Bizentro.AppFramework.UI.Variables.enumDef.IsSearch.Yes;
            this.uniGrid1.ShowHeaderCheck = true;
            this.uniGrid1.StyleSetName  = "uniGrid_Query";
            this.uniGrid1.TabIndex      = 1;
            this.uniGrid1.UseDynamicFormat = false;
            this.uniGrid1.AfterCellUpdate  += new Infragistics.Win.UltraWinGrid.CellEventHandler(this.uniGrid1_AfterCellUpdate);
            this.uniGrid1.CellChange       += new Infragistics.Win.UltraWinGrid.CellEventHandler(this.uniGrid1_CellChange);
            this.uniGrid1.AfterSelectChange += new Infragistics.Win.UltraWinGrid.AfterSelectChangeEventHandler(this.uniGrid1_AfterSelectChange);

            // ════════════════════════════════════════════════════════════
            //  ModuleViewer (this)
            // ════════════════════════════════════════════════════════════
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 15F);
            // ★ 표준 레이아웃: 검색(Top) → 그리드(Fill) → 평가바(Bottom)
            this.Controls.Add(this.uniGrid1);       // Fill (가운데 전체)
            this.Controls.Add(this.pnlEvalBar);     // Bottom
            this.Controls.Add(this.pnlSearch);      // Top
            this.Name = "ModuleViewer";
            this.Size = new System.Drawing.Size(1400, 900);
            this.Controls.SetChildIndex(this.uniLabel_Path, 0);
            this.Controls.SetChildIndex(this.pnlSearch,     0);
            this.Controls.SetChildIndex(this.pnlEvalBar,    0);
            this.Controls.SetChildIndex(this.uniGrid1,      0);

            // ── ResumeLayout ──────────────────────────────────────────────
            this.pnlSearch.ResumeLayout(false);
            this.tlpSearch.ResumeLayout(false);
            this.tlpSearch.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.uniDtmFrom)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.uniDtmTo)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.txtUsrId)).EndInit();
            this.pnlEvalBar.ResumeLayout(false);
            this.tlpEval.ResumeLayout(false);
            // txtScore, txtFeedback: 표준 TextBox — EndInit 불필요
            this.splitMain.Panel1.ResumeLayout(false);
            this.splitMain.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitMain)).EndInit();
            this.splitMain.ResumeLayout(false);
            this.pnlChatScroll.ResumeLayout(false);
            this.pnlGridContainer.ResumeLayout(false);
            this.pnlGridHeader.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.uniGrid1)).EndInit();
            this.ResumeLayout(false);
        }
        #endregion

        // ── 컨트롤 필드 선언 ───────────────────────────────────────────────
        // 검색 패널
        private System.Windows.Forms.Panel                                    pnlSearch;
        private System.Windows.Forms.TableLayoutPanel                         tlpSearch;
        private System.Windows.Forms.Label                                    lblFrom;
        private Infragistics.Win.UltraWinEditors.UltraDateTimeEditor          uniDtmFrom;
        private System.Windows.Forms.Label                                    lblTilde;
        private Infragistics.Win.UltraWinEditors.UltraDateTimeEditor          uniDtmTo;
        private System.Windows.Forms.Label                                    lblUser;
        private Bizentro.AppFramework.UI.Controls.uniTextBox                  txtUsrId;
        // 평가 입력 패널
        private System.Windows.Forms.Panel                                    pnlEvalBar;
        private System.Windows.Forms.TableLayoutPanel                         tlpEval;
        private System.Windows.Forms.Label                                    lblSelInfo;
        private System.Windows.Forms.Label                                    lblScoreLabel;
        private System.Windows.Forms.TextBox                                  txtScore;
        private System.Windows.Forms.Label                                    lblFeedbackLabel;
        private System.Windows.Forms.TextBox                                  txtFeedback;
        private System.Windows.Forms.Label                                    lblSqlLabel;
        private System.Windows.Forms.TextBox                                  txtCorrectSql;
        private Infragistics.Win.Misc.UltraButton                             btnSave;
        private Infragistics.Win.Misc.UltraButton                             btnRegPattern;
        // 분할 컨테이너
        private System.Windows.Forms.SplitContainer                          splitMain;
        // 채팅 영역
        private System.Windows.Forms.Panel                                    pnlChatScroll;
        private System.Windows.Forms.FlowLayoutPanel                          flpMessages;
        // 그리드 영역
        private System.Windows.Forms.Panel                                    pnlGridContainer;
        private System.Windows.Forms.Panel                                    pnlGridHeader;
        private System.Windows.Forms.Label                                    lblGridTitle;
        private Bizentro.AppFramework.UI.Controls.uniGrid                     uniGrid1;
    }
}
