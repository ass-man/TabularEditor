using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

internal partial class MeasureKillerViewerForm : Form
{
    private const string JsonFolder = @"C:\Users\gamer\AppData\Local\Programs\Measure Killer";
    private const string JsonSearchPattern = "*.json";

    private readonly PluginHostContext context;
    private readonly TextBox txtFilter;
    private readonly Label lblStatus;
    private readonly Label lblSelection;
    private readonly CheckBox chkSelectedObject;
    private readonly TreeView tree;
    private readonly TextBox txtDetails;
    private readonly DataGridView gridObjects;
    private readonly ComboBox cboStatus;
    private readonly ComboBox cboObjectType;
    private readonly ComboBox cboTable;
    private readonly TextBox txtSummary;
    private readonly TextBox txtRaw;
    private readonly CheckBox chkFullDetails;
    private readonly Panel visualPreview;
    private readonly RowStyle visualPreviewRow;
    private readonly Button btnPreviewPrevious;
    private readonly Button btnPreviewNext;
    private readonly Label lblPreviewPage;
    private readonly Panel visualPreviewContainer;
    private readonly List<VisualBox> visualBoxes = new List<VisualBox>();
    private readonly List<VisualBox> allPreviewBoxes = new List<VisualBox>();
    private readonly List<VisualBox> allReportVisualBoxes = new List<VisualBox>();
    private readonly List<string> visualPreviewPages = new List<string>();
    private string visualPreviewTitle = "Visual coordinates";
    private int visualPreviewPageIndex;
    private readonly Timer selectionTimer;
    private readonly Timer filterTimer;
    private readonly List<CheckBox> impactFilterChecks = new List<CheckBox>();
    private readonly List<CheckBox> objectTypeFilterChecks = new List<CheckBox>();
    private readonly List<MkNode> allRoots = new List<MkNode>();
    private readonly List<LineageObject> lineageObjects = new List<LineageObject>();
    private readonly Dictionary<string, LineageObject> lineageByKey = new Dictionary<string, LineageObject>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<EffectiveUsage, Label> statusCountLabels = new Dictionary<EffectiveUsage, Label>();
    private readonly HashSet<string> rememberedExpandedPaths = new HashSet<string>();
    private string currentJsonPath = "";
    private string lastSelectionKey = "";
    private string cachedSelectedAffectKey = "";
    private HashSet<string> cachedSelectedAffectedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private bool expandAfterSelectionChange;
    private bool suppressExpansionTracking;
    private bool suppressGridSelectionChanged;

    public MeasureKillerViewerForm(PluginHostContext context)
    {
        this.context = context;

        Text = "Measure Killer JSON Viewer";
        Width = 1180;
        Height = 760;
        StartPosition = FormStartPosition.CenterParent;

        var rootLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4
        };
        rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 74));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(rootLayout);

        var toolStrip = new ToolStrip { Dock = DockStyle.Fill };
        var btnRefresh = new ToolStripButton("Refresh");
        var btnOpenJson = new ToolStripButton("Open JSON");
        btnRefresh.Click += delegate { LoadJson(); };
        btnOpenJson.Click += delegate { OpenJson(); };
        chkFullDetails = new CheckBox { AutoSize = true, Text = "Full details", Checked = false };
        chkFullDetails.CheckedChanged += delegate { UpdateSelectedObjectDetails(); };
        toolStrip.Items.Add(btnRefresh);
        toolStrip.Items.Add(btnOpenJson);
        rootLayout.Controls.Add(toolStrip, 0, 0);

        var filterLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 11,
            RowCount = 2,
            Padding = new Padding(8, 6, 8, 4)
        };
        filterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        filterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
        filterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        filterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 155));
        filterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        filterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        filterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        filterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        filterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        filterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        filterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65));

        filterLayout.Controls.Add(new Label { Text = "Search:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 5, 6, 0) }, 0, 0);
        txtFilter = new TextBox { Dock = DockStyle.Fill };
        txtFilter.TextChanged += delegate { ScheduleFilter(); };
        filterLayout.Controls.Add(txtFilter, 1, 0);

        filterLayout.Controls.Add(new Label { Text = "Status:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(12, 5, 6, 0) }, 2, 0);
        cboStatus = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        cboStatus.Items.AddRange(new object[] { "All", "Keep", "Cascade candidate", "Relationship only", "Relationship island", "Duplicate visual", "Unused", "Review" });
        cboStatus.SelectedIndex = 0;
        cboStatus.SelectedIndexChanged += delegate { ApplyFilters(); };
        filterLayout.Controls.Add(cboStatus, 3, 0);

        filterLayout.Controls.Add(new Label { Text = "Type:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(12, 5, 6, 0) }, 4, 0);
        cboObjectType = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        cboObjectType.Items.AddRange(new object[] { "All", "Table", "Column", "Measure", "Relationship", "Visual" });
        cboObjectType.SelectedIndex = 0;
        cboObjectType.SelectedIndexChanged += delegate { ApplyFilters(); };
        filterLayout.Controls.Add(cboObjectType, 5, 0);

        filterLayout.Controls.Add(new Label { Text = "Table:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(12, 5, 6, 0) }, 6, 0);
        cboTable = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        cboTable.SelectedIndexChanged += delegate { ApplyFilters(); };
        filterLayout.Controls.Add(cboTable, 7, 0);

        chkSelectedObject = new CheckBox { AutoSize = true, Checked = false, Text = "Selected object only", Margin = new Padding(12, 4, 6, 0) };
        chkSelectedObject.CheckedChanged += delegate { InvalidateSelectedAffectedCache(); ApplyFilters(); UpdateSelectionLabel(); };
        filterLayout.Controls.Add(chkSelectedObject, 8, 0);
        chkFullDetails.Margin = new Padding(12, 4, 6, 0);
        filterLayout.Controls.Add(chkFullDetails, 9, 0);

        lblStatus = new Label { AutoSize = true, Dock = DockStyle.Fill, Text = "Loading...", Margin = new Padding(0, 8, 0, 0) };
        filterLayout.Controls.Add(lblStatus, 1, 1);
        filterLayout.SetColumnSpan(lblStatus, 7);
        lblSelection = new Label { AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(12, 8, 0, 0) };
        filterLayout.Controls.Add(lblSelection, 8, 1);
        filterLayout.SetColumnSpan(lblSelection, 3);
        rootLayout.Controls.Add(filterLayout, 0, 1);

        var summaryStrip = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(8, 5, 8, 4)
        };
        AddStatusCount(summaryStrip, EffectiveUsage.Keep, "Keep");
        AddStatusCount(summaryStrip, EffectiveUsage.CascadeCandidate, "Cascade");
        AddStatusCount(summaryStrip, EffectiveUsage.RelationshipOnly, "Relationship only");
        AddStatusCount(summaryStrip, EffectiveUsage.RelationshipIsland, "Relationship island");
        AddStatusCount(summaryStrip, EffectiveUsage.DuplicateVisual, "Duplicate visuals");
        AddStatusCount(summaryStrip, EffectiveUsage.Unused, "Unused");
        AddStatusCount(summaryStrip, EffectiveUsage.Review, "Review");
        rootLayout.Controls.Add(summaryStrip, 0, 2);

        tree = new TreeView
        {
            Dock = DockStyle.Fill,
            HideSelection = false,
            ShowNodeToolTips = true,
            FullRowSelect = true,
            Indent = 24,
            ItemHeight = 20,
            BorderStyle = BorderStyle.FixedSingle
        };
        tree.AfterSelect += delegate { UpdatePreviewFromUsedByTreeSelection(); };
        tree.AfterExpand += delegate(object sender, TreeViewEventArgs e)
        {
            if (!suppressExpansionTracking && !string.IsNullOrEmpty(e.Node.Name)) rememberedExpandedPaths.Add(e.Node.Name);
        };
        tree.AfterCollapse += delegate(object sender, TreeViewEventArgs e)
        {
            if (!suppressExpansionTracking) RemoveRememberedExpansion(e.Node);
        };

        txtSummary = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            WordWrap = true,
            Font = new Font("Segoe UI", 9f)
        };
        txtDetails = txtSummary;
        txtRaw = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            Font = new Font("Consolas", 9f)
        };

        visualPreview = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Visible = false
        };
        visualPreview.Paint += PaintVisualPreview;

        btnPreviewPrevious = new Button { Text = "<", Dock = DockStyle.Fill, Width = 28 };
        btnPreviewNext = new Button { Text = ">", Dock = DockStyle.Fill, Width = 28 };
        lblPreviewPage = new Label { Text = "", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
        btnPreviewPrevious.Click += delegate { ChangePreviewPage(-1); };
        btnPreviewNext.Click += delegate { ChangePreviewPage(1); };

        var previewNav = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 26,
            ColumnCount = 3,
            RowCount = 1
        };
        previewNav.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 30));
        previewNav.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 30));
        previewNav.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        previewNav.Controls.Add(btnPreviewPrevious, 0, 0);
        previewNav.Controls.Add(btnPreviewNext, 1, 0);
        previewNav.Controls.Add(lblPreviewPage, 2, 0);

        visualPreviewContainer = new Panel
        {
            Dock = DockStyle.Fill,
            Visible = false
        };
        visualPreviewContainer.Controls.Add(visualPreview);
        visualPreviewContainer.Controls.Add(previewNav);

        var detailsLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 1
        };
        detailsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        detailsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        detailsLayout.Controls.Add(txtSummary, 0, 0);

        var tabs = new TabControl { Dock = DockStyle.Fill };
        var tabSummary = new TabPage("Summary");
        var tabChain = new TabPage("Used By Chain");
        var tabRaw = new TabPage("Raw Details");
        tabSummary.Controls.Add(detailsLayout);
        tabChain.Controls.Add(tree);
        tabRaw.Controls.Add(txtRaw);
        tabs.TabPages.Add(tabSummary);
        tabs.TabPages.Add(tabChain);
        tabs.TabPages.Add(tabRaw);

        var rightLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        rightLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        rightLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        visualPreviewRow = new RowStyle(SizeType.Absolute, 0);
        rightLayout.RowStyles.Add(visualPreviewRow);
        rightLayout.Controls.Add(tabs, 0, 0);
        rightLayout.Controls.Add(visualPreviewContainer, 0, 1);

        gridObjects = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            MultiSelect = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            RowHeadersVisible = false,
            BorderStyle = BorderStyle.FixedSingle
        };
        gridObjects.Columns.Add("Status", "Status");
        gridObjects.Columns.Add("ObjectType", "Object Type");
        gridObjects.Columns.Add("Table", "Table");
        gridObjects.Columns.Add("Object", "Object");
        gridObjects.Columns.Add("DirectUsage", "Direct Usage");
        gridObjects.Columns.Add("UsedBy", "Used By");
        gridObjects.Columns.Add("Reason", "Reason");
        gridObjects.Columns["Status"].FillWeight = 80;
        gridObjects.Columns["ObjectType"].FillWeight = 70;
        gridObjects.Columns["Table"].FillWeight = 95;
        gridObjects.Columns["Object"].FillWeight = 130;
        gridObjects.Columns["DirectUsage"].FillWeight = 115;
        gridObjects.Columns["UsedBy"].FillWeight = 60;
        gridObjects.Columns["Reason"].FillWeight = 190;
        gridObjects.SelectionChanged += delegate { if (!suppressGridSelectionChanged) UpdateSelectedObjectDetails(); };
        gridObjects.CellMouseDown += GridObjects_CellMouseDown;
        gridObjects.ContextMenuStrip = BuildGridContextMenu();

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 760
        };
        split.Panel1.Controls.Add(gridObjects);
        split.Panel2.Controls.Add(rightLayout);
        rootLayout.Controls.Add(split, 0, 3);

        selectionTimer = new Timer { Interval = 500 };
        selectionTimer.Tick += delegate { RefreshSelectionFilterIfNeeded(); };
        selectionTimer.Start();
        filterTimer = new Timer { Interval = 220 };
        filterTimer.Tick += delegate { filterTimer.Stop(); ApplyFilters(); };
        FormClosed += delegate { selectionTimer.Stop(); selectionTimer.Dispose(); filterTimer.Stop(); filterTimer.Dispose(); };
        Load += delegate { LoadJson(); UpdateSelectionLabel(); };
    }

    private void AddFilterCheck(FlowLayoutPanel panel, List<CheckBox> target, string text, object tag)
    {
        var check = new CheckBox
        {
            AutoSize = true,
            Checked = true,
            Text = text,
            Tag = tag,
            Margin = new Padding(0, 3, 12, 0)
        };
        check.CheckedChanged += delegate { RenderTree(true, false); };
        target.Add(check);
        panel.Controls.Add(check);
    }

    private void AddStatusCount(FlowLayoutPanel panel, EffectiveUsage status, string label)
    {
        var count = new Label
        {
            AutoSize = false,
            Width = 150,
            Height = 26,
            TextAlign = ContentAlignment.MiddleCenter,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = BackColorForUsage(status),
            ForeColor = Color.Black,
            Text = label + ": 0",
            Margin = new Padding(0, 2, 8, 0)
        };
        count.Tag = label;
        statusCountLabels[status] = count;
        panel.Controls.Add(count);
    }

    private ContextMenuStrip BuildGridContextMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Copy object name", null, delegate { CopySelectedObjectName(); });
        menu.Items.Add("Copy full lineage", null, delegate { CopySelectedLineage(); });
        menu.Items.Add("Copy removal recommendation", null, delegate { CopySelectedRecommendation(); });
        menu.Items.Add("Expand used-by chain", null, delegate { tree.ExpandAll(); });
        menu.Items.Add("Filter to this table", null, delegate { FilterToSelectedTable(); });
        menu.Items.Add("Filter to this status", null, delegate { FilterToSelectedStatus(); });
        return menu;
    }

    private void GridObjects_CellMouseDown(object sender, DataGridViewCellMouseEventArgs e)
    {
        if (e.Button != MouseButtons.Right || e.RowIndex < 0) return;
        gridObjects.ClearSelection();
        gridObjects.Rows[e.RowIndex].Selected = true;
        gridObjects.CurrentCell = gridObjects.Rows[e.RowIndex].Cells[Math.Max(0, e.ColumnIndex)];
    }

}
