namespace Bizentro.App.UI.PP.PA999M1_CKO087
{
    partial class ModuleViewer
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();

            // Infragistics appearances (uniGrid1 바인딩용)
            Infragistics.Win.Appearance appearance1  = new Infragistics.Win.Appearance();
            Infragistics.Win.Appearance appearance2  = new Infragistics.Win.Appearance();
            Infragistics.Win.Appearance appearance3  = new Infragistics.Win.Appearance();
            Infragistics.Win.Appearance appearance4  = new Infragistics.Win.Appearance();
            Infragistics.Win.Appearance appearance5  = new Infragistics.Win.Appearance();
            Infragistics.Win.Appearance appearance6  = new Infragistics.Win.Appearance();

            // ── 프레임워크 필수 레이아웃 패널 ────────────────────────────
            this.uniTBL_OuterMost     = new Bizentro.AppFramework.UI.Controls.uniTableLayoutPanel(this.components);
            this.uniTBL_MainReference = new Bizentro.AppFramework.UI.Controls.uniTableLayoutPanel(this.components);
            this.uniTBL_MainCondition = new Bizentro.AppFramework.UI.Controls.uniTableLayoutPanel(this.components);
            this.uniTBL_ChatInput     = new Bizentro.AppFramework.UI.Controls.uniTableLayoutPanel(this.components); // 0px 숨김
            this.uniTBL_ChatAnswer    = new Bizentro.AppFramework.UI.Controls.uniTableLayoutPanel(this.components); // 메인 컨텐츠 영역
            this.uniTBL_MainData      = new Bizentro.AppFramework.UI.Controls.uniTableLayoutPanel(this.components); // 0px (uniGrid1 바인딩 보관)

            // ── 프레임워크 요구 컨트롤 (hidden, 바인딩 유지) ────────────
            this.lblQuestion     = new Bizentro.AppFramework.UI.Controls.uniLabel(this.components);
            this.lblAnswer       = new Bizentro.AppFramework.UI.Controls.uniLabel(this.components);
            this.lblHistory      = new Bizentro.AppFramework.UI.Controls.uniLabel(this.components);
            this.lblAiData       = new Bizentro.AppFramework.UI.Controls.uniLabel(this.components);
            this.txtAiAnswer     = new System.Windows.Forms.RichTextBox();
            this.dgvAiResult     = new System.Windows.Forms.DataGridView();
            this.uniGrid1        = new Bizentro.AppFramework.UI.Controls.uniGrid(this.components);

            // ── 새 챗 UI 컨트롤 ──────────────────────────────────────────
            this.tblContent       = new System.Windows.Forms.TableLayoutPanel();   // 좌/우 2열 분할
            this.pnlLeft          = new System.Windows.Forms.Panel();               // 좌: 대화 이력
            this.pnlHistoryHeader = new System.Windows.Forms.Panel();               // 좌 상단 헤더
            this.lblHistoryTitle  = new System.Windows.Forms.Label();               // "대화 이력"
            this.pnlHistoryList   = new System.Windows.Forms.Panel();               // 이력 아이템 영역
            this.tblRight         = new System.Windows.Forms.TableLayoutPanel();    // 우: 채팅+입력 2행 분할
            this.pnlChatScroll    = new System.Windows.Forms.Panel();               // 채팅 버블 스크롤
            this.flpMessages      = new System.Windows.Forms.FlowLayoutPanel();    // 채팅 버블 컨테이너
            this.pnlInputBar      = new System.Windows.Forms.Panel();               // 입력 바
            this.txtQuestion      = new Bizentro.AppFramework.UI.Controls.uniTextBox(this.components);
            this.btnAskAI         = new System.Windows.Forms.Button();
            this.btnResetSession  = new System.Windows.Forms.Button();
            this.pnlRowDiv        = new System.Windows.Forms.Panel();  // 채팅↔입력 1px 구분선 전용 패널
            this.pnlColDiv        = new System.Windows.Forms.Panel();  // 사이드바↔채팅 1px 열 구분선 전용 패널

            // SuspendLayout
            this.uniTBL_OuterMost.SuspendLayout();
            this.uniTBL_MainReference.SuspendLayout();
            this.uniTBL_ChatInput.SuspendLayout();
            this.uniTBL_ChatAnswer.SuspendLayout();
            this.uniTBL_MainData.SuspendLayout();
            this.tblContent.SuspendLayout();
            this.pnlLeft.SuspendLayout();
            this.pnlHistoryHeader.SuspendLayout();
            this.tblRight.SuspendLayout();
            this.pnlChatScroll.SuspendLayout();
            this.pnlInputBar.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.txtQuestion)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.uniGrid1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.dgvAiResult)).BeginInit();
            this.SuspendLayout();

            // ══════════════════════════════════════════════════════════
            // uniTBL_OuterMost  —  Row4·5·3 을 0px 으로 축소
            //   Row 0 (21px)  : uniTBL_MainReference
            //   Row 1 ( 6px)  : gap
            //   Row 2 ( 0px)  : uniTBL_MainCondition (hidden)
            //   Row 3 ( 0px)  : gap (collapsed)
            //   Row 4 ( 0px)  : uniTBL_ChatInput (collapsed)
            //   Row 5 ( 0px)  : gap (collapsed)
            //   Row 6 (fill)  : uniTBL_ChatAnswer  ← 메인 콘텐츠
            //   Row 7 ( 0px)  : gap
            //   Row 8 ( 0px)  : uniTBL_MainData (uniGrid1 바인딩용, 숨김)
            //   Row 9 ( 0px)  : gap
            // ══════════════════════════════════════════════════════════
            this.uniTBL_OuterMost.AutoFit = false;
            this.uniTBL_OuterMost.AutoFitColumnCount = 4;
            this.uniTBL_OuterMost.AutoFitRowCount = 4;
            this.uniTBL_OuterMost.BackColor = System.Drawing.Color.Transparent;
            this.uniTBL_OuterMost.BizentroTableLayout = Bizentro.AppFramework.UI.Controls.BizentroTableLayOutStyle.DefaultTableLayout;
            this.uniTBL_OuterMost.ColumnCount = 1;
            this.uniTBL_OuterMost.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.uniTBL_OuterMost.Controls.Add(this.uniTBL_MainReference, 0, 0);
            this.uniTBL_OuterMost.Controls.Add(this.uniTBL_MainCondition, 0, 2);
            this.uniTBL_OuterMost.Controls.Add(this.uniTBL_ChatInput,     0, 4);
            this.uniTBL_OuterMost.Controls.Add(this.uniTBL_ChatAnswer,    0, 6);
            this.uniTBL_OuterMost.Controls.Add(this.uniTBL_MainData,      0, 8);
            this.uniTBL_OuterMost.DefaultRowSize = 23;
            this.uniTBL_OuterMost.Dock = System.Windows.Forms.DockStyle.Fill;
            this.uniTBL_OuterMost.EasyBaseBatchType = Bizentro.AppFramework.UI.Controls.EasyBaseTBType.NONE;
            this.uniTBL_OuterMost.HEIGHT_TYPE_00_REFERENCE = 21F;
            this.uniTBL_OuterMost.HEIGHT_TYPE_01 = 6F;
            this.uniTBL_OuterMost.HEIGHT_TYPE_01_CONDITION = 38F;
            this.uniTBL_OuterMost.HEIGHT_TYPE_02 = 9F;
            this.uniTBL_OuterMost.HEIGHT_TYPE_02_DATA = 0F;
            this.uniTBL_OuterMost.HEIGHT_TYPE_03 = 3F;
            this.uniTBL_OuterMost.HEIGHT_TYPE_03_BOTTOM = 28F;
            this.uniTBL_OuterMost.HEIGHT_TYPE_04 = 1F;
            this.uniTBL_OuterMost.Location = new System.Drawing.Point(1, 10);
            this.uniTBL_OuterMost.Margin = new System.Windows.Forms.Padding(0);
            this.uniTBL_OuterMost.Name = "uniTBL_OuterMost";
            this.uniTBL_OuterMost.PanelType = Bizentro.AppFramework.UI.Variables.enumDef.PanelType.Default;
            this.uniTBL_OuterMost.RowCount = 10;
            this.uniTBL_OuterMost.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 21F));  // 0 reference
            this.uniTBL_OuterMost.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute,  6F));  // 1 gap
            this.uniTBL_OuterMost.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute,  0F));  // 2 condition (숨김)
            this.uniTBL_OuterMost.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute,  0F));  // 3 gap (축소)
            this.uniTBL_OuterMost.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute,  0F));  // 4 chat input (축소)
            this.uniTBL_OuterMost.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute,  0F));  // 5 gap (축소)
            this.uniTBL_OuterMost.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));  // 6 main content (fill)
            this.uniTBL_OuterMost.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute,  0F));  // 7 gap
            this.uniTBL_OuterMost.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute,  0F));  // 8 data (숨김)
            this.uniTBL_OuterMost.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute,  0F));  // 9 gap
            this.uniTBL_OuterMost.Size = new System.Drawing.Size(1263, 650);
            this.uniTBL_OuterMost.SizeTD5 = 14F;
            this.uniTBL_OuterMost.SizeTD6 = 36F;
            this.uniTBL_OuterMost.TabIndex = 0;
            this.uniTBL_OuterMost.uniLR_SPACE_TYPE = Bizentro.AppFramework.UI.Controls.LR_SPACE_TYPE.LR_SPACE_TYPE_00;

            // ══════════════════════════════════════════════════════════
            // uniTBL_MainReference (경로 표시줄)
            // ══════════════════════════════════════════════════════════
            this.uniTBL_MainReference.AutoFit = false;
            this.uniTBL_MainReference.AutoFitColumnCount = 4;
            this.uniTBL_MainReference.AutoFitRowCount = 4;
            this.uniTBL_MainReference.BackColor = System.Drawing.Color.Transparent;
            this.uniTBL_MainReference.BizentroTableLayout = Bizentro.AppFramework.UI.Controls.BizentroTableLayOutStyle.DefaultTableLayout;
            this.uniTBL_MainReference.ColumnCount = 1;
            this.uniTBL_MainReference.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.uniTBL_MainReference.DefaultRowSize = 23;
            this.uniTBL_MainReference.Dock = System.Windows.Forms.DockStyle.Fill;
            this.uniTBL_MainReference.EasyBaseBatchType = Bizentro.AppFramework.UI.Controls.EasyBaseTBType.NONE;
            this.uniTBL_MainReference.HEIGHT_TYPE_00_REFERENCE = 21F;
            this.uniTBL_MainReference.HEIGHT_TYPE_01 = 6F;
            this.uniTBL_MainReference.HEIGHT_TYPE_01_CONDITION = 38F;
            this.uniTBL_MainReference.HEIGHT_TYPE_02 = 9F;
            this.uniTBL_MainReference.HEIGHT_TYPE_02_DATA = 0F;
            this.uniTBL_MainReference.HEIGHT_TYPE_03 = 3F;
            this.uniTBL_MainReference.HEIGHT_TYPE_03_BOTTOM = 28F;
            this.uniTBL_MainReference.HEIGHT_TYPE_04 = 1F;
            this.uniTBL_MainReference.Location = new System.Drawing.Point(0, 0);
            this.uniTBL_MainReference.Margin = new System.Windows.Forms.Padding(0);
            this.uniTBL_MainReference.Name = "uniTBL_MainReference";
            this.uniTBL_MainReference.PanelType = Bizentro.AppFramework.UI.Variables.enumDef.PanelType.Default;
            this.uniTBL_MainReference.RowCount = 1;
            this.uniTBL_MainReference.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.uniTBL_MainReference.Size = new System.Drawing.Size(1263, 21);
            this.uniTBL_MainReference.SizeTD5 = 14F;
            this.uniTBL_MainReference.SizeTD6 = 36F;
            this.uniTBL_MainReference.TabIndex = 1;
            this.uniTBL_MainReference.uniLR_SPACE_TYPE = Bizentro.AppFramework.UI.Controls.LR_SPACE_TYPE.LR_SPACE_TYPE_00;

            // ══════════════════════════════════════════════════════════
            // uniTBL_MainCondition (조건부, 0px 숨김)
            // ══════════════════════════════════════════════════════════
            this.uniTBL_MainCondition.AutoFit = false;
            this.uniTBL_MainCondition.AutoFitColumnCount = 4;
            this.uniTBL_MainCondition.AutoFitRowCount = 4;
            this.uniTBL_MainCondition.BackColor = System.Drawing.Color.FromArgb(244, 245, 247);
            this.uniTBL_MainCondition.BizentroTableLayout = Bizentro.AppFramework.UI.Controls.BizentroTableLayOutStyle.DefaultTableLayout;
            this.uniTBL_MainCondition.ColumnCount = 1;
            this.uniTBL_MainCondition.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.uniTBL_MainCondition.DefaultRowSize = 23;
            this.uniTBL_MainCondition.Dock = System.Windows.Forms.DockStyle.Fill;
            this.uniTBL_MainCondition.EasyBaseBatchType = Bizentro.AppFramework.UI.Controls.EasyBaseTBType.NONE;
            this.uniTBL_MainCondition.HEIGHT_TYPE_00_REFERENCE = 21F;
            this.uniTBL_MainCondition.HEIGHT_TYPE_01 = 6F;
            this.uniTBL_MainCondition.HEIGHT_TYPE_01_CONDITION = 38F;
            this.uniTBL_MainCondition.HEIGHT_TYPE_02 = 9F;
            this.uniTBL_MainCondition.HEIGHT_TYPE_02_DATA = 0F;
            this.uniTBL_MainCondition.HEIGHT_TYPE_03 = 3F;
            this.uniTBL_MainCondition.HEIGHT_TYPE_03_BOTTOM = 28F;
            this.uniTBL_MainCondition.HEIGHT_TYPE_04 = 1F;
            this.uniTBL_MainCondition.Location = new System.Drawing.Point(0, 27);
            this.uniTBL_MainCondition.Margin = new System.Windows.Forms.Padding(0);
            this.uniTBL_MainCondition.Name = "uniTBL_MainCondition";
            this.uniTBL_MainCondition.Padding = new System.Windows.Forms.Padding(0, 7, 0, 0);
            this.uniTBL_MainCondition.PanelType = Bizentro.AppFramework.UI.Variables.enumDef.PanelType.Condition;
            this.uniTBL_MainCondition.RowCount = 1;
            this.uniTBL_MainCondition.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.uniTBL_MainCondition.Size = new System.Drawing.Size(1263, 38);
            this.uniTBL_MainCondition.SizeTD5 = 14F;
            this.uniTBL_MainCondition.SizeTD6 = 36F;
            this.uniTBL_MainCondition.TabIndex = 2;
            this.uniTBL_MainCondition.uniLR_SPACE_TYPE = Bizentro.AppFramework.UI.Controls.LR_SPACE_TYPE.LR_SPACE_TYPE_00;

            // ══════════════════════════════════════════════════════════
            // uniTBL_ChatInput (0px 축소 — 컨트롤만 보관)
            // ══════════════════════════════════════════════════════════
            this.uniTBL_ChatInput.AutoFit = false;
            this.uniTBL_ChatInput.AutoFitColumnCount = 4;
            this.uniTBL_ChatInput.AutoFitRowCount = 4;
            this.uniTBL_ChatInput.BackColor = System.Drawing.Color.Transparent;
            this.uniTBL_ChatInput.BizentroTableLayout = Bizentro.AppFramework.UI.Controls.BizentroTableLayOutStyle.DefaultTableLayout;
            this.uniTBL_ChatInput.ColumnCount = 1;
            this.uniTBL_ChatInput.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.uniTBL_ChatInput.Controls.Add(this.lblQuestion, 0, 0);  // hidden
            this.uniTBL_ChatInput.DefaultRowSize = 23;
            this.uniTBL_ChatInput.Dock = System.Windows.Forms.DockStyle.Fill;
            this.uniTBL_ChatInput.EasyBaseBatchType = Bizentro.AppFramework.UI.Controls.EasyBaseTBType.NONE;
            this.uniTBL_ChatInput.HEIGHT_TYPE_00_REFERENCE = 21F;
            this.uniTBL_ChatInput.HEIGHT_TYPE_01 = 6F;
            this.uniTBL_ChatInput.HEIGHT_TYPE_01_CONDITION = 38F;
            this.uniTBL_ChatInput.HEIGHT_TYPE_02 = 9F;
            this.uniTBL_ChatInput.HEIGHT_TYPE_02_DATA = 0F;
            this.uniTBL_ChatInput.HEIGHT_TYPE_03 = 3F;
            this.uniTBL_ChatInput.HEIGHT_TYPE_03_BOTTOM = 28F;
            this.uniTBL_ChatInput.HEIGHT_TYPE_04 = 1F;
            this.uniTBL_ChatInput.Location = new System.Drawing.Point(0, 27);
            this.uniTBL_ChatInput.Margin = new System.Windows.Forms.Padding(0);
            this.uniTBL_ChatInput.Name = "uniTBL_ChatInput";
            this.uniTBL_ChatInput.PanelType = Bizentro.AppFramework.UI.Variables.enumDef.PanelType.Default;
            this.uniTBL_ChatInput.RowCount = 1;
            this.uniTBL_ChatInput.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.uniTBL_ChatInput.Size = new System.Drawing.Size(1263, 0);
            this.uniTBL_ChatInput.SizeTD5 = 14F;
            this.uniTBL_ChatInput.SizeTD6 = 36F;
            this.uniTBL_ChatInput.TabIndex = 3;
            this.uniTBL_ChatInput.uniLR_SPACE_TYPE = Bizentro.AppFramework.UI.Controls.LR_SPACE_TYPE.LR_SPACE_TYPE_00;

            // lblQuestion (숨김)
            this.lblQuestion.AutoPopupID = null;
            this.lblQuestion.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblQuestion.LabelType = Bizentro.AppFramework.UI.Variables.enumDef.LabelType.Title;
            this.lblQuestion.Name = "lblQuestion";
            this.lblQuestion.Size = new System.Drawing.Size(0, 0);
            this.lblQuestion.StyleSetName = "Default";
            this.lblQuestion.TabIndex = 0;
            this.lblQuestion.Text = "";
            this.lblQuestion.UseMnemonic = false;

            // txtAiAnswer (숨김, 참조 유지)
            this.txtAiAnswer.Name = "txtAiAnswer";
            this.txtAiAnswer.Visible = false;
            this.txtAiAnswer.Size = new System.Drawing.Size(0, 0);

            // dgvAiResult (숨김, 참조 유지)
            this.dgvAiResult.Name = "dgvAiResult";
            this.dgvAiResult.Visible = false;
            this.dgvAiResult.Size = new System.Drawing.Size(0, 0);
            ((System.ComponentModel.ISupportInitialize)(this.dgvAiResult)).EndInit();

            // lblAnswer (숨김)
            this.lblAnswer.AutoPopupID = null;
            this.lblAnswer.Name = "lblAnswer";
            this.lblAnswer.Visible = false;
            this.lblAnswer.Size = new System.Drawing.Size(0, 0);
            this.lblAnswer.StyleSetName = "Default";
            this.lblAnswer.TabIndex = 99;
            this.lblAnswer.Text = "";
            this.lblAnswer.UseMnemonic = false;

            // lblAiData (숨김)
            this.lblAiData.AutoPopupID = null;
            this.lblAiData.Name = "lblAiData";
            this.lblAiData.Visible = false;
            this.lblAiData.Size = new System.Drawing.Size(0, 0);
            this.lblAiData.StyleSetName = "Default";
            this.lblAiData.TabIndex = 99;
            this.lblAiData.Text = "";
            this.lblAiData.UseMnemonic = false;

            // ══════════════════════════════════════════════════════════
            // uniTBL_ChatAnswer  →  메인 컨텐츠 영역 (fill)
            //   Row 0 (fill): tblContent (좌우 2열 챗 레이아웃)
            // ══════════════════════════════════════════════════════════
            this.uniTBL_ChatAnswer.AutoFit = false;
            this.uniTBL_ChatAnswer.AutoFitColumnCount = 4;
            this.uniTBL_ChatAnswer.AutoFitRowCount = 4;
            this.uniTBL_ChatAnswer.BackColor = System.Drawing.Color.Transparent;
            this.uniTBL_ChatAnswer.BizentroTableLayout = Bizentro.AppFramework.UI.Controls.BizentroTableLayOutStyle.DefaultTableLayout;
            this.uniTBL_ChatAnswer.ColumnCount = 1;
            this.uniTBL_ChatAnswer.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.uniTBL_ChatAnswer.Controls.Add(this.tblContent, 0, 0);
            this.uniTBL_ChatAnswer.DefaultRowSize = 23;
            this.uniTBL_ChatAnswer.Dock = System.Windows.Forms.DockStyle.Fill;
            this.uniTBL_ChatAnswer.EasyBaseBatchType = Bizentro.AppFramework.UI.Controls.EasyBaseTBType.NONE;
            this.uniTBL_ChatAnswer.HEIGHT_TYPE_00_REFERENCE = 21F;
            this.uniTBL_ChatAnswer.HEIGHT_TYPE_01 = 6F;
            this.uniTBL_ChatAnswer.HEIGHT_TYPE_01_CONDITION = 38F;
            this.uniTBL_ChatAnswer.HEIGHT_TYPE_02 = 9F;
            this.uniTBL_ChatAnswer.HEIGHT_TYPE_02_DATA = 0F;
            this.uniTBL_ChatAnswer.HEIGHT_TYPE_03 = 3F;
            this.uniTBL_ChatAnswer.HEIGHT_TYPE_03_BOTTOM = 28F;
            this.uniTBL_ChatAnswer.HEIGHT_TYPE_04 = 1F;
            this.uniTBL_ChatAnswer.Location = new System.Drawing.Point(0, 27);
            this.uniTBL_ChatAnswer.Margin = new System.Windows.Forms.Padding(0);
            this.uniTBL_ChatAnswer.Name = "uniTBL_ChatAnswer";
            this.uniTBL_ChatAnswer.PanelType = Bizentro.AppFramework.UI.Variables.enumDef.PanelType.Default;
            this.uniTBL_ChatAnswer.RowCount = 1;
            this.uniTBL_ChatAnswer.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.uniTBL_ChatAnswer.Size = new System.Drawing.Size(1263, 623);
            this.uniTBL_ChatAnswer.SizeTD5 = 14F;
            this.uniTBL_ChatAnswer.SizeTD6 = 36F;
            this.uniTBL_ChatAnswer.TabIndex = 4;
            this.uniTBL_ChatAnswer.uniLR_SPACE_TYPE = Bizentro.AppFramework.UI.Controls.LR_SPACE_TYPE.LR_SPACE_TYPE_00;

            // ══════════════════════════════════════════════════════════
            // tblContent  —  2열 (좌 185px 이력 | 우 fill 채팅)
            // ══════════════════════════════════════════════════════════
            this.tblContent.ColumnCount = 3;
            this.tblContent.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 185F)); // 좌: 이력 사이드바
            this.tblContent.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute,   1F)); // 열 구분선 1px
            this.tblContent.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));  // 우: 채팅
            this.tblContent.Controls.Add(this.pnlLeft,   0, 0);
            this.tblContent.Controls.Add(this.pnlColDiv, 1, 0);
            this.tblContent.Controls.Add(this.tblRight,  2, 0);
            this.tblContent.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tblContent.Margin = new System.Windows.Forms.Padding(0);
            this.tblContent.Name = "tblContent";
            this.tblContent.RowCount = 1;
            this.tblContent.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tblContent.TabIndex = 0;
            this.tblContent.BackColor = System.Drawing.Color.FromArgb(0, 95, 184); // 외곽 테두리 색 (Padding 1px 영역에 노출)
            // Padding=1: 자식 패널이 1px 안쪽에 배치 → BackColor가 외곽 테두리로 노출
            // tblContent.Paint 이벤트로 외곽 사각형 명시적 렌더링 (ModuleViewer.cs)
            this.tblContent.Padding = new System.Windows.Forms.Padding(1);

            // ══════════════════════════════════════════════════════════
            // pnlLeft  —  대화 이력 사이드바
            // ══════════════════════════════════════════════════════════
            this.pnlLeft.BackColor = System.Drawing.Color.White; // 컨테이너 (자식이 전체 채움 — 열 구분선은 pnlColDiv 담당)
            this.pnlLeft.Controls.Add(this.pnlHistoryList);
            this.pnlLeft.Controls.Add(this.pnlHistoryHeader);
            this.pnlLeft.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlLeft.Margin = new System.Windows.Forms.Padding(0);
            this.pnlLeft.Name = "pnlLeft";
            this.pnlLeft.TabIndex = 0;

            // pnlHistoryHeader  (상단 38px — 버튼과 레이블 수직 여유 확보)
            this.pnlHistoryHeader.BackColor = System.Drawing.Color.White;
            this.pnlHistoryHeader.Controls.Add(this.btnResetSession);
            this.pnlHistoryHeader.Controls.Add(this.lblHistoryTitle);
            this.pnlHistoryHeader.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlHistoryHeader.Height = 38;
            this.pnlHistoryHeader.Name = "pnlHistoryHeader";
            this.pnlHistoryHeader.Padding = new System.Windows.Forms.Padding(10, 0, 6, 0);
            this.pnlHistoryHeader.TabIndex = 0;
            // bottom border 효과: Paint 이벤트로 처리

            // lblHistoryTitle  "대화 이력"
            this.lblHistoryTitle.AutoSize = false;
            this.lblHistoryTitle.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblHistoryTitle.Font = new System.Drawing.Font("맑은 고딕", 9.5F, System.Drawing.FontStyle.Bold);
            this.lblHistoryTitle.ForeColor = System.Drawing.Color.FromArgb(50, 50, 60);
            this.lblHistoryTitle.Name = "lblHistoryTitle";
            this.lblHistoryTitle.Text = "대화 이력";
            this.lblHistoryTitle.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.lblHistoryTitle.TabIndex = 0;

            // btnResetSession  "+ 새 대화"
            this.btnResetSession.Dock = System.Windows.Forms.DockStyle.Right;
            this.btnResetSession.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnResetSession.FlatAppearance.BorderSize = 1;
            this.btnResetSession.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(0, 95, 184);
            // Font: 9.5F — lblHistoryTitle과 동일 크기·서체로 수직 기준선 일치
            this.btnResetSession.Font = new System.Drawing.Font("맑은 고딕", 9.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.btnResetSession.ForeColor = System.Drawing.Color.FromArgb(0, 95, 184);
            this.btnResetSession.Name = "btnResetSession";
            this.btnResetSession.Size = new System.Drawing.Size(88, 30); // 텍스트 잘림 방지: 너비 88, 높이 30
            this.btnResetSession.TabIndex = 1;
            this.btnResetSession.Text = "+ 새 대화"; // 전각 ＋ 제거 → 반각 + 사용 (렌더링 안전)
            this.btnResetSession.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.btnResetSession.UseVisualStyleBackColor = false;
            this.btnResetSession.BackColor = System.Drawing.Color.White;
            this.btnResetSession.Click += new System.EventHandler(this.btnResetSession_Click);

            // pnlHistoryList  (이력 아이템 스크롤)
            this.pnlHistoryList.AutoScroll = true;
            this.pnlHistoryList.BackColor = System.Drawing.Color.FromArgb(225, 228, 238);
            this.pnlHistoryList.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlHistoryList.Name = "pnlHistoryList";
            // 상단 5px: 헤더 하단 구분선(3px)과 첫 번째 이력 아이템 사이 간격 — 구분선이 내용에 가려지지 않도록
            // 하단 4px: 리스트 최하단 여백
            this.pnlHistoryList.Padding = new System.Windows.Forms.Padding(0, 5, 0, 4);
            this.pnlHistoryList.TabIndex = 1;

            // ══════════════════════════════════════════════════════════
            // tblRight  —  우측 2행 (채팅 스크롤 fill | 입력바 75px)
            // ══════════════════════════════════════════════════════════
            this.tblRight.ColumnCount = 1;
            this.tblRight.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tblRight.Controls.Add(this.pnlChatScroll, 0, 0);
            this.tblRight.Controls.Add(this.pnlRowDiv,     0, 1);  // 1px 구분선 전용 패널
            this.tblRight.Controls.Add(this.pnlInputBar,   0, 2);
            this.tblRight.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tblRight.Margin = new System.Windows.Forms.Padding(0);
            this.tblRight.Name = "tblRight";
            this.tblRight.RowCount = 3;
            this.tblRight.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));  // row 0: 채팅 영역
            this.tblRight.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute,   1F));  // row 1: 구분선 1px
            this.tblRight.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute,  92F));  // row 2: 입력 바
            this.tblRight.TabIndex = 1;
            this.tblRight.BackColor = System.Drawing.Color.Transparent;

            // pnlRowDiv — 채팅 영역과 입력바 사이 1px 파란 구분선 전용 패널
            this.pnlRowDiv.BackColor = System.Drawing.Color.FromArgb(0, 95, 184);
            this.pnlRowDiv.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlRowDiv.Name = "pnlRowDiv";
            this.pnlRowDiv.TabIndex = 2;

            // pnlColDiv — 사이드바와 채팅 영역 사이 1px 파란 열 구분선 전용 패널
            this.pnlColDiv.BackColor = System.Drawing.Color.FromArgb(0, 95, 184);
            this.pnlColDiv.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlColDiv.Name = "pnlColDiv";
            this.pnlColDiv.TabIndex = 3;

            // ══════════════════════════════════════════════════════════
            // pnlChatScroll  —  채팅 버블 스크롤 영역
            // ══════════════════════════════════════════════════════════
            this.pnlChatScroll.AutoScroll = true;
            this.pnlChatScroll.BackColor = System.Drawing.Color.FromArgb(242, 244, 247);
            this.pnlChatScroll.Controls.Add(this.flpMessages);
            this.pnlChatScroll.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlChatScroll.Name = "pnlChatScroll";
            this.pnlChatScroll.TabIndex = 0;

            // flpMessages  —  채팅 버블 흐름 컨테이너 (TopDown)
            this.flpMessages.AutoSize = false;
            this.flpMessages.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            this.flpMessages.WrapContents = false;
            this.flpMessages.BackColor = System.Drawing.Color.Transparent;
            this.flpMessages.Name = "flpMessages";
            this.flpMessages.Padding = new System.Windows.Forms.Padding(8, 8, 8, 16);
            this.flpMessages.TabIndex = 0;
            this.flpMessages.Width = 1078; // 초기값; Resize 이벤트에서 조정

            // ══════════════════════════════════════════════════════════
            // pnlInputBar  —  하단 입력 바 (75px)
            // ══════════════════════════════════════════════════════════
            this.pnlInputBar.BackColor = System.Drawing.Color.FromArgb(238, 245, 255);
            this.pnlInputBar.Controls.Add(this.btnAskAI);
            this.pnlInputBar.Controls.Add(this.txtQuestion);
            this.pnlInputBar.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlInputBar.Name = "pnlInputBar";
            this.pnlInputBar.Padding = new System.Windows.Forms.Padding(10, 8, 10, 8);
            this.pnlInputBar.TabIndex = 1;

            // txtQuestion  (입력 텍스트박스)
            Infragistics.Win.Appearance appearanceQ = new Infragistics.Win.Appearance();
            appearanceQ.TextVAlignAsString = "Middle";
            this.txtQuestion.Appearance = appearanceQ;
            this.txtQuestion.Anchor = System.Windows.Forms.AnchorStyles.Top
                                    | System.Windows.Forms.AnchorStyles.Left
                                    | System.Windows.Forms.AnchorStyles.Right
                                    | System.Windows.Forms.AnchorStyles.Bottom;
            this.txtQuestion.FieldType = Bizentro.AppFramework.UI.Variables.enumDef.FieldType.Default;
            this.txtQuestion.Location = new System.Drawing.Point(10, 28);
            this.txtQuestion.LockedField = false;
            this.txtQuestion.Margin = new System.Windows.Forms.Padding(0);
            this.txtQuestion.MaxLength = 500;
            this.txtQuestion.Name = "txtQuestion";
            this.txtQuestion.NullText = "궁금한 것을 입력하세요  (예: 단양공장 이번달 생산량 알려줘)";
            this.txtQuestion.QueryIfEnterKeyPressed = false;
            this.txtQuestion.RequiredField = false;
            this.txtQuestion.Size = new System.Drawing.Size(850, 56);
            this.txtQuestion.Style = Bizentro.AppFramework.UI.Controls.TextBox_Style.Default;
            this.txtQuestion.StyleSetName = "Default";
            this.txtQuestion.TabIndex = 10;
            this.txtQuestion.uniALT = null;
            this.txtQuestion.uniCharacterCasing = System.Windows.Forms.CharacterCasing.Normal;
            this.txtQuestion.UseDynamicFormat = false;
            this.txtQuestion.KeyDown += new System.Windows.Forms.KeyEventHandler(this.txtQuestion_KeyDown);

            // btnAskAI
            this.btnAskAI.Anchor = System.Windows.Forms.AnchorStyles.Top
                                 | System.Windows.Forms.AnchorStyles.Right
                                 | System.Windows.Forms.AnchorStyles.Bottom;
            this.btnAskAI.BackColor = System.Drawing.Color.FromArgb(0, 95, 184);
            this.btnAskAI.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnAskAI.FlatAppearance.BorderSize = 0;
            this.btnAskAI.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Bold);
            this.btnAskAI.ForeColor = System.Drawing.Color.White;
            this.btnAskAI.Location = new System.Drawing.Point(870, 28);
            this.btnAskAI.Margin = new System.Windows.Forms.Padding(0);
            this.btnAskAI.Name = "btnAskAI";
            this.btnAskAI.Size = new System.Drawing.Size(90, 56);
            this.btnAskAI.TabIndex = 11;
            this.btnAskAI.Text = "AI 질문\r\n(F5)";
            this.btnAskAI.UseVisualStyleBackColor = false;
            this.btnAskAI.Click += new System.EventHandler(this.btnAskAI_Click);

            // ══════════════════════════════════════════════════════════
            // uniTBL_MainData (0px — uniGrid1 바인딩 보관)
            // ══════════════════════════════════════════════════════════
            this.uniTBL_MainData.AutoFit = false;
            this.uniTBL_MainData.AutoFitColumnCount = 4;
            this.uniTBL_MainData.AutoFitRowCount = 4;
            this.uniTBL_MainData.BackColor = System.Drawing.Color.Transparent;
            this.uniTBL_MainData.BizentroTableLayout = Bizentro.AppFramework.UI.Controls.BizentroTableLayOutStyle.DefaultTableLayout;
            this.uniTBL_MainData.ColumnCount = 1;
            this.uniTBL_MainData.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.uniTBL_MainData.Controls.Add(this.lblHistory, 0, 0);
            this.uniTBL_MainData.Controls.Add(this.uniGrid1,   0, 1);
            this.uniTBL_MainData.DefaultRowSize = 23;
            this.uniTBL_MainData.Dock = System.Windows.Forms.DockStyle.Fill;
            this.uniTBL_MainData.EasyBaseBatchType = Bizentro.AppFramework.UI.Controls.EasyBaseTBType.NONE;
            this.uniTBL_MainData.HEIGHT_TYPE_00_REFERENCE = 21F;
            this.uniTBL_MainData.HEIGHT_TYPE_01 = 6F;
            this.uniTBL_MainData.HEIGHT_TYPE_01_CONDITION = 38F;
            this.uniTBL_MainData.HEIGHT_TYPE_02 = 9F;
            this.uniTBL_MainData.HEIGHT_TYPE_02_DATA = 0F;
            this.uniTBL_MainData.HEIGHT_TYPE_03 = 3F;
            this.uniTBL_MainData.HEIGHT_TYPE_03_BOTTOM = 28F;
            this.uniTBL_MainData.HEIGHT_TYPE_04 = 1F;
            this.uniTBL_MainData.Location = new System.Drawing.Point(0, 303);
            this.uniTBL_MainData.Margin = new System.Windows.Forms.Padding(0);
            this.uniTBL_MainData.Name = "uniTBL_MainData";
            this.uniTBL_MainData.PanelType = Bizentro.AppFramework.UI.Variables.enumDef.PanelType.Data;
            this.uniTBL_MainData.RowCount = 2;
            this.uniTBL_MainData.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 18F));
            this.uniTBL_MainData.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.uniTBL_MainData.Size = new System.Drawing.Size(1263, 0);
            this.uniTBL_MainData.SizeTD5 = 14F;
            this.uniTBL_MainData.SizeTD6 = 36F;
            this.uniTBL_MainData.TabIndex = 5;
            this.uniTBL_MainData.uniLR_SPACE_TYPE = Bizentro.AppFramework.UI.Controls.LR_SPACE_TYPE.LR_SPACE_TYPE_00;

            // lblHistory (숨김)
            this.lblHistory.AutoPopupID = null;
            this.lblHistory.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblHistory.LabelType = Bizentro.AppFramework.UI.Variables.enumDef.LabelType.Title;
            this.lblHistory.Name = "lblHistory";
            this.lblHistory.Size = new System.Drawing.Size(0, 0);
            this.lblHistory.StyleSetName = "Default";
            this.lblHistory.TabIndex = 0;
            this.lblHistory.Text = "";
            this.lblHistory.UseMnemonic = false;

            // uniGrid1 (숨김, 프레임워크 바인딩용)
            this.uniGrid1.AddEmptyRow = false;
            this.uniGrid1.DirectPaste = false;
            appearance1.BackColor = System.Drawing.SystemColors.Window;
            appearance1.BorderColor = System.Drawing.SystemColors.InactiveCaption;
            this.uniGrid1.DisplayLayout.Appearance = appearance1;
            this.uniGrid1.DisplayLayout.BorderStyle = Infragistics.Win.UIElementBorderStyle.Solid;
            this.uniGrid1.DisplayLayout.CaptionVisible = Infragistics.Win.DefaultableBoolean.False;
            appearance2.BackColor = System.Drawing.SystemColors.ActiveBorder;
            appearance2.BackColor2 = System.Drawing.SystemColors.ControlDark;
            appearance2.BackGradientStyle = Infragistics.Win.GradientStyle.Vertical;
            appearance2.BorderColor = System.Drawing.SystemColors.Window;
            this.uniGrid1.DisplayLayout.GroupByBox.Appearance = appearance2;
            appearance3.ForeColor = System.Drawing.SystemColors.GrayText;
            this.uniGrid1.DisplayLayout.GroupByBox.BandLabelAppearance = appearance3;
            this.uniGrid1.DisplayLayout.GroupByBox.BorderStyle = Infragistics.Win.UIElementBorderStyle.Solid;
            appearance4.BackColor = System.Drawing.SystemColors.ControlLightLight;
            appearance4.BackColor2 = System.Drawing.SystemColors.Control;
            appearance4.BackGradientStyle = Infragistics.Win.GradientStyle.Horizontal;
            appearance4.ForeColor = System.Drawing.SystemColors.GrayText;
            this.uniGrid1.DisplayLayout.GroupByBox.PromptAppearance = appearance4;
            this.uniGrid1.DisplayLayout.MaxColScrollRegions = 1;
            this.uniGrid1.DisplayLayout.MaxRowScrollRegions = 1;
            this.uniGrid1.DisplayLayout.Override.AllowAddNew = Infragistics.Win.UltraWinGrid.AllowAddNew.No;
            this.uniGrid1.DisplayLayout.Override.BorderStyleCell = Infragistics.Win.UIElementBorderStyle.Dotted;
            this.uniGrid1.DisplayLayout.Override.BorderStyleRow  = Infragistics.Win.UIElementBorderStyle.Dotted;
            appearance5.BackColor = System.Drawing.SystemColors.Window;
            this.uniGrid1.DisplayLayout.Override.CardAreaAppearance = appearance5;
            appearance6.BorderColor = System.Drawing.Color.Silver;
            appearance6.TextTrimming = Infragistics.Win.TextTrimming.EllipsisCharacter;
            this.uniGrid1.DisplayLayout.Override.CellAppearance = appearance6;
            this.uniGrid1.DisplayLayout.Override.CellClickAction = Infragistics.Win.UltraWinGrid.CellClickAction.CellSelect;
            this.uniGrid1.DisplayLayout.Override.CellPadding = 0;
            this.uniGrid1.DisplayLayout.Override.HeaderClickAction = Infragistics.Win.UltraWinGrid.HeaderClickAction.SortMulti;
            this.uniGrid1.DisplayLayout.Override.HeaderStyle = Infragistics.Win.HeaderStyle.WindowsXPCommand;
            this.uniGrid1.DisplayLayout.Override.RowSelectors = Infragistics.Win.DefaultableBoolean.False;
            this.uniGrid1.DisplayLayout.ScrollBounds = Infragistics.Win.UltraWinGrid.ScrollBounds.ScrollToFill;
            this.uniGrid1.DisplayLayout.ScrollStyle  = Infragistics.Win.UltraWinGrid.ScrollStyle.Immediate;
            this.uniGrid1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.uniGrid1.EnableContextMenu = true;
            this.uniGrid1.EnableGridFilterMenu = false;
            this.uniGrid1.EnableGridInfoContextMenu = true;
            this.uniGrid1.ExceptInExcel = false;
            this.uniGrid1.Font = new System.Drawing.Font("Arial", 9F);
            this.uniGrid1.gComNumDec = Bizentro.AppFramework.UI.Variables.enumDef.ComDec.Decimal;
            this.uniGrid1.GridStyle = Bizentro.AppFramework.UI.Variables.enumDef.GridStyle.AppendDetail;
            this.uniGrid1.Location  = new System.Drawing.Point(0, 0);
            this.uniGrid1.Margin    = new System.Windows.Forms.Padding(0);
            this.uniGrid1.Name      = "uniGrid1";
            this.uniGrid1.OutlookGroupBy = Bizentro.AppFramework.UI.Variables.enumDef.IsOutlookGroupBy.No;
            this.uniGrid1.PopupDeleteMenuVisible = true;
            this.uniGrid1.PopupInsertMenuVisible = true;
            this.uniGrid1.PopupUndoMenuVisible   = true;
            this.uniGrid1.Search = Bizentro.AppFramework.UI.Variables.enumDef.IsSearch.Yes;
            this.uniGrid1.ShowHeaderCheck = false;
            this.uniGrid1.Size = new System.Drawing.Size(0, 0);
            this.uniGrid1.StyleSetName = "Default";
            this.uniGrid1.TabIndex = 20;
            this.uniGrid1.UseDynamicFormat = false;
            ((System.ComponentModel.ISupportInitialize)(this.uniGrid1)).EndInit();

            // ══════════════════════════════════════════════════════════
            // ModuleViewer
            // ══════════════════════════════════════════════════════════
            this.uniLabel_Path.LabelType = Bizentro.AppFramework.UI.Variables.enumDef.LabelType.PathInfo;
            this.uniLabel_Path.Size = new System.Drawing.Size(500, 14);

            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.Controls.Add(this.uniTBL_OuterMost);
            this.MinimumSize = new System.Drawing.Size(0, 0);
            this.Name = "ModuleViewer";
            this.Size = new System.Drawing.Size(1274, 660);
            this.Controls.SetChildIndex(this.uniTBL_OuterMost, 0);
            this.Controls.SetChildIndex(this.uniLabel_Path, 0);
            this.Load += new System.EventHandler(this.ModuleViewer_Load);

            // ResumeLayout
            this.pnlInputBar.ResumeLayout(false);
            this.pnlChatScroll.ResumeLayout(false);
            this.pnlChatScroll.PerformLayout();
            this.tblRight.ResumeLayout(false);
            this.pnlHistoryHeader.ResumeLayout(false);
            this.pnlLeft.ResumeLayout(false);
            this.tblContent.ResumeLayout(false);
            this.uniTBL_ChatAnswer.ResumeLayout(false);
            this.uniTBL_MainData.ResumeLayout(false);
            this.uniTBL_ChatInput.ResumeLayout(false);
            this.uniTBL_MainReference.ResumeLayout(false);
            this.uniTBL_OuterMost.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.txtQuestion)).EndInit();
            this.ResumeLayout(false);
        }

        #endregion

        // ── 컨트롤 선언 ────────────────────────────────────────────────
        // 프레임워크 필수
        private Bizentro.AppFramework.UI.Controls.uniTableLayoutPanel uniTBL_OuterMost;
        private Bizentro.AppFramework.UI.Controls.uniTableLayoutPanel uniTBL_MainReference;
        private Bizentro.AppFramework.UI.Controls.uniTableLayoutPanel uniTBL_MainCondition;
        private Bizentro.AppFramework.UI.Controls.uniTableLayoutPanel uniTBL_ChatInput;
        private Bizentro.AppFramework.UI.Controls.uniTableLayoutPanel uniTBL_ChatAnswer;
        private Bizentro.AppFramework.UI.Controls.uniTableLayoutPanel uniTBL_MainData;
        private Bizentro.AppFramework.UI.Controls.uniLabel  lblQuestion;
        private Bizentro.AppFramework.UI.Controls.uniLabel  lblAnswer;
        private Bizentro.AppFramework.UI.Controls.uniLabel  lblHistory;
        private Bizentro.AppFramework.UI.Controls.uniLabel  lblAiData;
        private System.Windows.Forms.RichTextBox            txtAiAnswer;     // 참조 유지 (hidden)
        private System.Windows.Forms.DataGridView           dgvAiResult;     // 참조 유지 (hidden)
        private Bizentro.AppFramework.UI.Controls.uniGrid   uniGrid1;        // 바인딩용 (hidden)

        // 새 챗 UI
        private System.Windows.Forms.TableLayoutPanel       tblContent;
        private System.Windows.Forms.Panel                  pnlLeft;
        private System.Windows.Forms.Panel                  pnlHistoryHeader;
        private System.Windows.Forms.Label                  lblHistoryTitle;
        private System.Windows.Forms.Panel                  pnlHistoryList;
        private System.Windows.Forms.TableLayoutPanel       tblRight;
        private System.Windows.Forms.Panel                  pnlChatScroll;
        private System.Windows.Forms.FlowLayoutPanel        flpMessages;
        private System.Windows.Forms.Panel                  pnlRowDiv;      // 채팅↔입력 1px 행 구분선
        private System.Windows.Forms.Panel                  pnlColDiv;      // 사이드바↔채팅 1px 열 구분선
        private System.Windows.Forms.Panel                  pnlInputBar;
        private Bizentro.AppFramework.UI.Controls.uniTextBox txtQuestion;
        private System.Windows.Forms.Button                 btnAskAI;
        private System.Windows.Forms.Button                 btnResetSession;
    }
}
