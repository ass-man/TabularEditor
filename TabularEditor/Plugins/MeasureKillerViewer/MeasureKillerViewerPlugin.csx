using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

public class MeasureKillerViewerPlugin : IRuntimeWindowPlugin
{
    public Form CreateWindow(PluginHostContext context)
    {
        return new MeasureKillerViewerForm(context);
    }
}

internal class MeasureKillerViewerForm : Form
{
    private const string JsonPath = @"C:\Users\gamer\AppData\Local\Programs\Measure Killer\WWI_test_20260509_200325.json";

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
    private readonly List<VisualBox> visualBoxes = new List<VisualBox>();
    private readonly Timer selectionTimer;
    private readonly List<CheckBox> impactFilterChecks = new List<CheckBox>();
    private readonly List<CheckBox> objectTypeFilterChecks = new List<CheckBox>();
    private readonly List<MkNode> allRoots = new List<MkNode>();
    private readonly List<LineageObject> lineageObjects = new List<LineageObject>();
    private readonly Dictionary<string, LineageObject> lineageByKey = new Dictionary<string, LineageObject>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<EffectiveUsage, Label> statusCountLabels = new Dictionary<EffectiveUsage, Label>();
    private readonly HashSet<string> rememberedExpandedPaths = new HashSet<string>();
    private string lastSelectionKey = "";
    private bool expandAfterSelectionChange;
    private bool suppressExpansionTracking;

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
        txtFilter.TextChanged += delegate { ApplyFilters(); };
        filterLayout.Controls.Add(txtFilter, 1, 0);

        filterLayout.Controls.Add(new Label { Text = "Status:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(12, 5, 6, 0) }, 2, 0);
        cboStatus = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        cboStatus.Items.AddRange(new object[] { "All", "Keep", "Cascade candidate", "Relationship only", "Relationship island", "Unused", "Review" });
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
        chkSelectedObject.CheckedChanged += delegate { ApplyFilters(); UpdateSelectionLabel(); };
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
        rightLayout.Controls.Add(visualPreview, 0, 1);

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
        gridObjects.SelectionChanged += delegate { UpdateSelectedObjectDetails(); };
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
        FormClosed += delegate { selectionTimer.Stop(); selectionTimer.Dispose(); };
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

    private void LoadJson()
    {
        allRoots.Clear();
        tree.Nodes.Clear();
        lineageObjects.Clear();
        lineageByKey.Clear();

        if (!File.Exists(JsonPath))
        {
            lblStatus.Text = "JSON file not found.";
            return;
        }

        try
        {
            var root = Json.ReadObject(File.ReadAllText(JsonPath));
            BuildLineageModel(root);
            PopulateTableFilter();
            ApplyFilters();
            lblStatus.Text = "Loaded JSON export. Objects: " + lineageObjects.Count + ", relationships: " + Json.Array(root, "relationships").Count + ".";
            return;

            var modelName = Json.Str(root, "model_name");
            var model = new MkNode("Model: " + modelName, false, "Export date: " + Json.Str(root, "export_date"));
            allRoots.Add(model);
            AddLegend(model);

            var tablesFolder = model.Add("Tables", false, "");
            var objectCount = 0;
            var unusedCount = 0;
            var unusedMeasures = BuildUnusedMeasureKeys(root);

            foreach (Dictionary<string, object> table in Json.Array(root, "tables"))
            {
                var tableName = Json.Str(table, "name");
                var tableImpact = ClassifyTable(table, unusedMeasures);
                var tableNode = tablesFolder.Add(
                    tableName + "  [Table]  " + ImpactLabel(tableImpact),
                    tableImpact,
                    "Storage: " + Json.Str(table, "storage_mode") + "\r\n" + ImpactDescription(tableImpact));
                tableNode.ObjectType = "Table";
                tableNode.SourceObject = table;
                tableNode.SearchText = TermsText(tableName, "table", Json.Str(table, "storage_mode"));
                objectCount++;
                if (tableNode.IsUnused) unusedCount++;

                var columnsFolder = tableNode.Add("Columns (" + Json.Array(table, "columns").Count + ")", false, "");
                foreach (Dictionary<string, object> column in Json.Array(table, "columns"))
                {
                    var used = Json.Str(column, "is_used");
                    var impact = ClassifyObject(column, table, unusedMeasures);
                    var node = columnsFolder.Add(
                        Json.Str(column, "name") + "  [Column]  " + ImpactLabel(impact),
                        impact,
                        BuildTooltip(column, impact));
                    node.ObjectType = "Column";
                    node.SourceObject = column;
                    node.SearchText = TermsText(Json.Str(column, "name"), tableName, "column", used, Json.Str(column, "description"));
                    AddImpactChildren(node, column);
                    objectCount++;
                    if (node.IsUnused) unusedCount++;
                }

                var measuresFolder = tableNode.Add("Measures (" + Json.Array(table, "measures").Count + ")", false, "");
                foreach (Dictionary<string, object> measure in Json.Array(table, "measures"))
                {
                    var used = Json.Str(measure, "is_used");
                    var impact = ClassifyObject(measure, table, unusedMeasures);
                    var node = measuresFolder.Add(
                        Json.Str(measure, "name") + "  [Measure]  " + ImpactLabel(impact),
                        impact,
                        BuildTooltip(measure, impact));
                    node.ObjectType = "Measure";
                    node.SourceObject = measure;
                    node.SearchText = TermsText(Json.Str(measure, "name"), tableName, "measure", used, Json.Str(measure, "expression"));
                    AddImpactChildren(node, measure);
                    objectCount++;
                    if (node.IsUnused) unusedCount++;
                }

                var partitionsFolder = tableNode.Add("Partitions (" + Json.Array(table, "partitions").Count + ")", false, "");
                foreach (Dictionary<string, object> partition in Json.Array(table, "partitions"))
                {
                    var state = Json.Str(partition, "state_str");
                    var node = partitionsFolder.Add(
                        Json.Str(partition, "name") + "  [Partition]  " + state,
                        false,
                        Json.Str(partition, "expression"));
                    node.ObjectType = "Partition";
                    node.SourceObject = partition;
                    node.SearchText = TermsText(Json.Str(partition, "name"), tableName, "partition", state, Json.Str(partition, "type_str"));
                    objectCount++;
                }
            }

            var relationshipsFolder = model.Add("Relationships (" + Json.Array(root, "relationships").Count + ")", false, "");
            foreach (Dictionary<string, object> relationship in Json.Array(root, "relationships"))
            {
                var used = FirstNonEmpty(Json.Str(relationship, "is_used"), Json.Str(relationship, "status"));
                var name = FirstNonEmpty(Json.Str(relationship, "name"), Json.Str(relationship, "from_column") + " -> " + Json.Str(relationship, "to_column"));
                var impact = IsUnused(used) ? ImpactKind.Unused : ImpactKind.Relationship;
                var node = relationshipsFolder.Add(name + "  [Relationship]  " + ImpactLabel(impact), impact, ImpactDescription(impact));
                node.ObjectType = "Relationship";
                node.SourceObject = relationship;
                node.SearchText = TermsText(name, "relationship", used, Json.Str(relationship, "from_column"), Json.Str(relationship, "to_column"));
                objectCount++;
                if (node.IsUnused) unusedCount++;
            }

            var bestPracticesFolder = model.Add("Best Practices", false, "");
            AddBestPracticeNodes(bestPracticesFolder, Json.Value(root, "best_practices"));

            lblStatus.Text = "Loaded JSON export. Objects: " + objectCount + ", unused: " + unusedCount + ", relationships: " + Json.Array(root, "relationships").Count + ".";
            rememberedExpandedPaths.Clear();
            RenderTree(true, true);
        }
        catch (Exception ex)
        {
            lblStatus.Text = "Failed to load JSON: " + ex.Message;
        }
    }

    private void BuildLineageModel(Dictionary<string, object> root)
    {
        var unusedMeasures = BuildUnusedMeasureKeys(root);

        foreach (Dictionary<string, object> table in Json.Array(root, "tables"))
        {
            var tableName = Json.Str(table, "name");
            AddLineageObject("Table", tableName, tableName, table);

            foreach (Dictionary<string, object> column in Json.Array(table, "columns"))
                AddLineageObject("Column", tableName, Json.Str(column, "name"), column);

            foreach (Dictionary<string, object> measure in Json.Array(table, "measures"))
                AddLineageObject("Measure", tableName, Json.Str(measure, "name"), measure);
        }

        foreach (Dictionary<string, object> relationship in Json.Array(root, "relationships"))
        {
            var name = FirstNonEmpty(Json.Str(relationship, "name"), RelationshipName(relationship));
            AddLineageObject("Relationship", "", name, relationship);
        }

        foreach (var obj in lineageObjects.ToArray())
        {
            var raw = obj.SourceObject as Dictionary<string, object>;
            if (raw == null || obj.ObjectType == "Visual") continue;
            AddConsumerEdges(obj, raw);
        }

        foreach (var obj in lineageObjects)
            obj.DirectUsage = BuildDirectUsage(obj);

        foreach (var obj in lineageObjects)
        {
            if (obj.ObjectType == "Visual")
            {
                obj.EffectiveUsage = EffectiveUsage.Keep;
                obj.Reason = "This is a report visual consumer.";
                obj.Recommendation = "Keep the model objects that feed this visual unless the report visual is removed.";
                continue;
            }
            ClassifyLineageObject(obj, unusedMeasures);
        }

        foreach (var table in lineageObjects)
            if (table.ObjectType == "Table") ClassifyTableLineage(table);
    }

    private LineageObject AddLineageObject(string objectType, string tableName, string name, Dictionary<string, object> raw)
    {
        var key = ObjectKey(objectType, tableName, name);
        LineageObject existing;
        if (lineageByKey.TryGetValue(key, out existing)) return existing;

        var obj = new LineageObject();
        obj.Key = key;
        obj.ObjectType = objectType;
        obj.TableName = tableName ?? "";
        obj.Name = string.IsNullOrWhiteSpace(name) ? "(unnamed)" : name;
        obj.SourceObject = raw;
        obj.RawProperties = ScalarProperties(raw);
        obj.SearchText = NormalizeForSearch(objectType + " " + tableName + " " + name + " " + Json.Str(raw, "expression") + " " + Json.Str(raw, "description"));
        obj.EffectiveUsage = EffectiveUsage.Review;
        obj.Reason = "Measure Killer did not provide enough lineage to classify this object confidently.";
        obj.Recommendation = "Review manually before deleting or renaming.";
        lineageByKey[key] = obj;
        lineageObjects.Add(obj);
        return obj;
    }

    private LineageObject GetOrCreateConsumer(string objectType, string tableName, string name, Dictionary<string, object> raw, EffectiveUsage defaultStatus)
    {
        var obj = AddLineageObject(objectType, tableName, name, raw);
        if (obj.SourceObject == null) obj.SourceObject = raw;
        if (defaultStatus == EffectiveUsage.Keep && obj.ObjectType == "Visual") obj.EffectiveUsage = EffectiveUsage.Keep;
        return obj;
    }

    private void AddConsumerEdges(LineageObject obj, Dictionary<string, object> raw)
    {
        AddVisualEdges(obj, raw, "visual_dependencies", LineageEdgeKind.Visual);
        AddVisualEdges(obj, raw, "visual_level_filters_dependencies", LineageEdgeKind.VisualFilter);
        AddVisualEdges(obj, raw, "page_level_filters_dependencies", LineageEdgeKind.PageFilter);
        AddVisualEdges(obj, raw, "report_level_filters_dependencies", LineageEdgeKind.ReportFilter);
        AddVisualEdges(obj, raw, "visual_calculations_dependencies", LineageEdgeKind.Visual);
        AddVisualEdges(obj, raw, "mobile_layout_dependencies", LineageEdgeKind.Visual);

        foreach (Dictionary<string, object> dep in Json.Array(raw, "artifact_dependencies"))
        {
            var type = FirstNonEmpty(Json.Str(dep, "reference_type"), "Artifact");
            var table = Json.Str(dep, "table_name");
            var name = Json.Str(dep, "artifact_name");
            if (string.IsNullOrWhiteSpace(name)) name = DependencyText(dep, new[] { "report_name", "artifact_name", "reference_type" });
            var consumerType = type.Equals("Measure", StringComparison.OrdinalIgnoreCase) ? "Measure" : "Artifact";
            var consumer = GetOrCreateConsumer(consumerType, table, name, dep, EffectiveUsage.Review);
            AddEdge(obj, consumer, consumerType == "Measure" ? LineageEdgeKind.MeasureDependency : LineageEdgeKind.Artifact, type);
        }

        foreach (Dictionary<string, object> dep in Json.Array(raw, "relationship_dependencies"))
        {
            var name = FirstNonEmpty(Json.Str(dep, "name"), RelationshipName(dep));
            var consumer = GetOrCreateConsumer("Relationship", "", name, dep, EffectiveUsage.RelationshipOnly);
            AddEdge(obj, consumer, LineageEdgeKind.Relationship, "Relationship");
        }

        AddMetadataEdges(obj, raw, "sorting_dependencies", LineageEdgeKind.SortBy, "Sort by");
        AddMetadataEdges(obj, raw, "hierarchy_dependencies", LineageEdgeKind.Hierarchy, "Hierarchy");
        AddMetadataEdges(obj, raw, "field_parameter_dependencies", LineageEdgeKind.FieldParameter, "Field parameter");
        AddMetadataEdges(obj, raw, "calculation_item_dependencies", LineageEdgeKind.CalculationItem, "Calculation item");
        AddMetadataEdges(obj, raw, "row_level_security_dependencies", LineageEdgeKind.RowLevelSecurity, "RLS");
        AddMetadataEdges(obj, raw, "model_dependencies", LineageEdgeKind.Unknown, "Model dependency");
    }

    private void AddVisualEdges(LineageObject obj, Dictionary<string, object> raw, string key, LineageEdgeKind kind)
    {
        foreach (Dictionary<string, object> dep in Json.Array(raw, key))
        {
            var visualName = VisualName(dep, kind);
            var visual = GetOrCreateConsumer("Visual", Json.Str(dep, "page"), visualName, dep, EffectiveUsage.Keep);
            visual.DirectUsage = UsageLabel(kind);
            AddEdge(obj, visual, kind, UsageLabel(kind));
        }
    }

    private void AddMetadataEdges(LineageObject obj, Dictionary<string, object> raw, string key, LineageEdgeKind kind, string label)
    {
        foreach (Dictionary<string, object> dep in Json.Array(raw, key))
        {
            var name = DependencyText(dep, new[] { "name", "object_name", "hierarchy_name", "level_name", "column", "sorted_column", "table_name" });
            var consumer = GetOrCreateConsumer(label, Json.Str(dep, "table_name"), name, dep, EffectiveUsage.Review);
            AddEdge(obj, consumer, kind, label);
        }
    }

    private static void AddEdge(LineageObject from, LineageObject to, LineageEdgeKind kind, string label)
    {
        if (from == null || to == null) return;
        foreach (var edge in from.UsedBy)
            if (edge.ToKey == to.Key && edge.Kind == kind) return;
        from.UsedBy.Add(new LineageEdge { FromKey = from.Key, ToKey = to.Key, Kind = kind, Label = label });
    }

    private void ClassifyLineageObject(LineageObject obj, HashSet<string> unusedMeasures)
    {
        var raw = obj.SourceObject as Dictionary<string, object>;
        var isUnused = raw != null && IsUnused(Json.Str(raw, "is_used"));
        if (ReachesRealConsumer(obj, new HashSet<string>(StringComparer.OrdinalIgnoreCase)))
        {
            obj.EffectiveUsage = EffectiveUsage.Keep;
            obj.Reason = "This object's dependency chain reaches a report visual, report filter, RLS rule, field parameter, calculation item, or external artifact.";
            obj.Recommendation = "Keep unless you also update the downstream report/model consumers.";
            return;
        }

        if (obj.UsedBy.Count > 0 && AllEdgesAre(obj, LineageEdgeKind.Relationship))
        {
            obj.EffectiveUsage = EffectiveUsage.RelationshipOnly;
            obj.Reason = "This object is only consumed by relationships. A relationship by itself is not real report usage.";
            obj.Recommendation = "Safe only if the connected table island is not needed.";
            return;
        }

        if (obj.UsedBy.Count > 0 && OnlyCascadeConsumers(obj, new HashSet<string>(StringComparer.OrdinalIgnoreCase)))
        {
            obj.EffectiveUsage = EffectiveUsage.CascadeCandidate;
            obj.Reason = "This object is only consumed by measures or artifacts that do not reach real report consumers.";
            obj.Recommendation = "Safe only if removing the dependent unused chain together.";
            return;
        }

        if (isUnused || obj.UsedBy.Count == 0)
        {
            obj.EffectiveUsage = EffectiveUsage.Unused;
            obj.Reason = "Measure Killer found no effective model or report consumers for this object.";
            obj.Recommendation = "Likely safe to remove after confirming it is not used outside the exported report scope.";
            return;
        }

        obj.EffectiveUsage = EffectiveUsage.Review;
        obj.Reason = "The object has model-only or unknown dependencies that do not prove report usage.";
        obj.Recommendation = "Review the Used By Chain and raw details before changing it.";
    }

    private void ClassifyTableLineage(LineageObject table)
    {
        var children = lineageObjects.Where(o => !ReferenceEquals(o, table) && o.TableName.Equals(table.Name, StringComparison.OrdinalIgnoreCase)).ToList();
        if (children.Count == 0) return;
        if (children.Any(o => o.EffectiveUsage == EffectiveUsage.Keep)) return;

        var hasRelationshipOnly = children.Any(o => o.EffectiveUsage == EffectiveUsage.RelationshipOnly || o.ObjectType == "Relationship");
        var allCleanup = children.All(o =>
            o.EffectiveUsage == EffectiveUsage.RelationshipOnly ||
            o.EffectiveUsage == EffectiveUsage.CascadeCandidate ||
            o.EffectiveUsage == EffectiveUsage.Unused ||
            o.EffectiveUsage == EffectiveUsage.RelationshipIsland);

        if (hasRelationshipOnly && allCleanup)
        {
            table.EffectiveUsage = EffectiveUsage.RelationshipIsland;
            table.Reason = "No child object reaches real report usage; the remaining usage is relationship-only or cascade-removable.";
            table.Recommendation = "Candidate for removing the whole relationship/table island together.";
        }
    }

    private bool ReachesRealConsumer(LineageObject obj, HashSet<string> visited)
    {
        if (obj == null || !visited.Add(obj.Key)) return false;
        foreach (var edge in obj.UsedBy)
        {
            if (IsRealConsumer(edge.Kind)) return true;
            LineageObject next;
            if (lineageByKey.TryGetValue(edge.ToKey, out next) && ReachesRealConsumer(next, visited)) return true;
        }
        return false;
    }

    private bool OnlyCascadeConsumers(LineageObject obj, HashSet<string> visited)
    {
        if (obj == null || !visited.Add(obj.Key)) return true;
        if (obj.UsedBy.Count == 0) return true;
        foreach (var edge in obj.UsedBy)
        {
            if (IsRealConsumer(edge.Kind) || edge.Kind == LineageEdgeKind.Relationship) return false;
            if (edge.Kind != LineageEdgeKind.MeasureDependency && edge.Kind != LineageEdgeKind.Artifact && edge.Kind != LineageEdgeKind.Unknown) return false;
            LineageObject next;
            if (lineageByKey.TryGetValue(edge.ToKey, out next) && !OnlyCascadeConsumers(next, visited)) return false;
        }
        return true;
    }

    private static bool IsRealConsumer(LineageEdgeKind kind)
    {
        return kind == LineageEdgeKind.Visual ||
               kind == LineageEdgeKind.VisualFilter ||
               kind == LineageEdgeKind.PageFilter ||
               kind == LineageEdgeKind.ReportFilter ||
               kind == LineageEdgeKind.RowLevelSecurity ||
               kind == LineageEdgeKind.FieldParameter ||
               kind == LineageEdgeKind.CalculationItem ||
               kind == LineageEdgeKind.Artifact;
    }

    private static bool AllEdgesAre(LineageObject obj, LineageEdgeKind kind)
    {
        if (obj.UsedBy.Count == 0) return false;
        foreach (var edge in obj.UsedBy)
            if (edge.Kind != kind) return false;
        return true;
    }

    private void PopulateTableFilter()
    {
        cboTable.Items.Clear();
        cboTable.Items.Add("All tables");
        foreach (var table in lineageObjects.Where(o => o.ObjectType == "Table").Select(o => o.Name).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x))
            cboTable.Items.Add(table);
        cboTable.SelectedIndex = 0;
    }

    private void ApplyFilters()
    {
        if (gridObjects == null) return;
        var searchTerms = Terms(txtFilter.Text);
        var status = Convert.ToString(cboStatus.SelectedItem ?? "All");
        var type = Convert.ToString(cboObjectType.SelectedItem ?? "All");
        var table = Convert.ToString(cboTable.SelectedItem ?? "All tables");
        var selectedTerms = chkSelectedObject.Checked ? GetSelectedObjectTerms() : new List<string[]>();

        gridObjects.Rows.Clear();
        foreach (var obj in lineageObjects)
        {
            if (!StatusMatches(obj, status)) continue;
            if (type != "All" && !obj.ObjectType.Equals(type, StringComparison.OrdinalIgnoreCase)) continue;
            if (table != "All tables" && !obj.TableName.Equals(table, StringComparison.OrdinalIgnoreCase) && !(obj.ObjectType == "Table" && obj.Name.Equals(table, StringComparison.OrdinalIgnoreCase))) continue;
            if (searchTerms.Length > 0 && !ContainsAllTerms(obj.SearchText, searchTerms)) continue;
            if (selectedTerms.Count > 0 && !SelectedTermsMatch(obj, selectedTerms)) continue;

            var index = gridObjects.Rows.Add(
                UsageLabel(obj.EffectiveUsage),
                obj.ObjectType,
                obj.TableName,
                obj.Name,
                obj.DirectUsage,
                obj.UsedBy.Count,
                obj.Reason);
            var row = gridObjects.Rows[index];
            row.Tag = obj;
            row.DefaultCellStyle.BackColor = BackColorForUsage(obj.EffectiveUsage);
            row.DefaultCellStyle.ForeColor = Color.Black;
        }
        UpdateStatusCounts();
        if (gridObjects.Rows.Count > 0 && gridObjects.CurrentCell == null)
            gridObjects.CurrentCell = gridObjects.Rows[0].Cells[0];
        UpdateSelectedObjectDetails();
    }

    private static bool StatusMatches(LineageObject obj, string status)
    {
        return status == "All" || UsageLabel(obj.EffectiveUsage).Equals(status, StringComparison.OrdinalIgnoreCase);
    }

    private static bool SelectedTermsMatch(LineageObject obj, List<string[]> selectedTerms)
    {
        foreach (var terms in selectedTerms)
            if (ContainsAllTerms(obj.SearchText, terms)) return true;
        return false;
    }

    private void UpdateStatusCounts()
    {
        foreach (var pair in statusCountLabels)
        {
            var count = lineageObjects.Count(o => o.EffectiveUsage == pair.Key);
            pair.Value.Text = Convert.ToString(pair.Value.Tag) + ": " + count;
        }
    }

    private LineageObject SelectedLineageObject()
    {
        if (gridObjects.SelectedRows.Count == 0) return null;
        return gridObjects.SelectedRows[0].Tag as LineageObject;
    }

    private void UpdateSelectedObjectDetails()
    {
        var obj = SelectedLineageObject();
        if (obj == null)
        {
            txtSummary.Text = "";
            txtRaw.Text = "";
            tree.Nodes.Clear();
            UpdateVisualPreview((LineageObject)null);
            return;
        }

        txtSummary.Text = BuildSummaryText(obj);
        txtRaw.Text = chkFullDetails.Checked ? RawDetails(obj) : "Enable Show full details to inspect the raw JSON fields.";
        BuildUsedByTree(obj);
        UpdateVisualPreview(obj);
    }

    private void UpdatePreviewFromUsedByTreeSelection()
    {
        var node = tree.SelectedNode;
        var obj = node == null ? null : node.Tag as LineageObject;
        if (obj == null) return;
        UpdateVisualPreview(obj);
    }

    private void UpdateVisualPreview(LineageObject obj)
    {
        visualBoxes.Clear();
        if (obj != null) CollectVisualBoxes(obj.SourceObject, visualBoxes);
        visualPreview.Visible = visualBoxes.Count > 0;
        visualPreviewRow.Height = visualBoxes.Count > 0 ? 190 : 0;
        visualPreview.Invalidate();
    }

    private string BuildSummaryText(LineageObject obj)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Object");
        builder.AppendLine("------");
        builder.AppendLine(obj.ObjectType + " / " + (string.IsNullOrWhiteSpace(obj.TableName) ? "" : obj.TableName + " / ") + obj.Name);
        builder.AppendLine();
        builder.AppendLine("Status");
        builder.AppendLine("------");
        builder.AppendLine(UsageLabel(obj.EffectiveUsage));
        builder.AppendLine();
        builder.AppendLine("Recommendation");
        builder.AppendLine("--------------");
        builder.AppendLine(WrapText(obj.Recommendation, 110));
        builder.AppendLine();
        builder.AppendLine("Reason");
        builder.AppendLine("------");
        builder.AppendLine(WrapText(obj.Reason, 110));
        builder.AppendLine();
        builder.AppendLine("Direct usage");
        builder.AppendLine("------------");
        builder.AppendLine(obj.DirectUsage);
        builder.AppendLine("Used by: " + obj.UsedBy.Count);
        builder.AppendLine();
        builder.AppendLine("Lineage");
        builder.AppendLine("-------");
        AppendLineageSummary(builder, obj, new HashSet<string>(StringComparer.OrdinalIgnoreCase), 0);
        return builder.ToString();
    }

    private void AppendLineageSummary(StringBuilder builder, LineageObject obj, HashSet<string> visited, int depth)
    {
        if (!visited.Add(obj.Key)) return;
        if (depth == 0) builder.AppendLine(obj.Name);
        if (obj.UsedBy.Count == 0)
        {
            builder.AppendLine("-> No real report consumers found");
            return;
        }
        foreach (var edge in obj.UsedBy)
        {
            LineageObject next;
            if (!lineageByKey.TryGetValue(edge.ToKey, out next)) continue;
            builder.AppendLine(new string(' ', Math.Min(depth + 1, 8) * 2) + "-> " + next.Name);
            if (depth < 6) AppendLineageSummary(builder, next, visited, depth + 1);
        }
    }

    private void BuildUsedByTree(LineageObject obj)
    {
        tree.BeginUpdate();
        tree.Nodes.Clear();
        var root = new TreeNode(NodeText(obj));
        root.Tag = obj;
        root.BackColor = BackColorForUsage(obj.EffectiveUsage);
        root.ForeColor = Color.Black;
        tree.Nodes.Add(root);
        AddUsedByNodes(root, obj, new HashSet<string>(StringComparer.OrdinalIgnoreCase), 0);
        root.Expand();
        tree.EndUpdate();
    }

    private void AddUsedByNodes(TreeNode parent, LineageObject obj, HashSet<string> visited, int depth)
    {
        if (!visited.Add(obj.Key) || depth > 8) return;
        if (obj.UsedBy.Count == 0)
        {
            parent.Nodes.Add(new TreeNode("[UNUSED] No real report consumers found"));
            return;
        }
        foreach (var edge in obj.UsedBy)
        {
            LineageObject next;
            if (!lineageByKey.TryGetValue(edge.ToKey, out next)) continue;
            var node = new TreeNode(NodeText(next));
            node.Tag = next;
            node.ToolTipText = next.ObjectType + " / " + next.TableName + " / " + next.Name + "\r\n" + next.Reason;
            node.BackColor = BackColorForUsage(next.EffectiveUsage);
            node.ForeColor = Color.Black;
            parent.Nodes.Add(node);
            AddUsedByNodes(node, next, visited, depth + 1);
        }
    }

    private string RawDetails(LineageObject obj)
    {
        var builder = new StringBuilder();
        if (obj.SourceObject != null) AppendValue(builder, obj.SourceObject, 0);
        return builder.ToString();
    }

    private void CopySelectedObjectName()
    {
        var obj = SelectedLineageObject();
        if (obj != null) Clipboard.SetText(obj.Name);
    }

    private void CopySelectedLineage()
    {
        var obj = SelectedLineageObject();
        if (obj != null) Clipboard.SetText(BuildSummaryText(obj));
    }

    private void CopySelectedRecommendation()
    {
        var obj = SelectedLineageObject();
        if (obj != null) Clipboard.SetText(obj.ObjectType + " / " + obj.TableName + " / " + obj.Name + "\r\n" + UsageLabel(obj.EffectiveUsage) + "\r\n" + obj.Recommendation + "\r\n" + obj.Reason);
    }

    private void FilterToSelectedTable()
    {
        var obj = SelectedLineageObject();
        if (obj == null || string.IsNullOrWhiteSpace(obj.TableName)) return;
        cboTable.SelectedItem = obj.TableName;
    }

    private void FilterToSelectedStatus()
    {
        var obj = SelectedLineageObject();
        if (obj == null) return;
        cboStatus.SelectedItem = UsageLabel(obj.EffectiveUsage);
    }

    private static Dictionary<string, string> ScalarProperties(Dictionary<string, object> raw)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (raw == null) return result;
        foreach (var pair in raw)
            if (!(pair.Value is Dictionary<string, object>) && !(pair.Value is ArrayList))
                result[pair.Key] = pair.Value == null ? "" : Convert.ToString(pair.Value);
        return result;
    }

    private static string ObjectKey(string type, string table, string name)
    {
        return (type ?? "") + "|" + (table ?? "") + "|" + (name ?? "");
    }

    private static string RelationshipName(Dictionary<string, object> dep)
    {
        return FirstNonEmpty(Json.Str(dep, "from_column"), Json.Str(dep, "column1")) + " -> " + FirstNonEmpty(Json.Str(dep, "to_column"), Json.Str(dep, "column2"));
    }

    private static string VisualName(Dictionary<string, object> dep, LineageEdgeKind kind)
    {
        var title = Json.Str(dep, "visual_title");
        if (string.IsNullOrWhiteSpace(title)) title = Json.Str(dep, "visual_type");
        if (string.IsNullOrWhiteSpace(title)) title = Json.Str(dep, "visual_id");
        return FirstNonEmpty(Json.Str(dep, "page"), Json.Str(dep, "report_name")) + " / " + UsageLabel(kind) + " / " + title;
    }

    private static string BuildDirectUsage(LineageObject obj)
    {
        var raw = obj.SourceObject as Dictionary<string, object>;
        if (raw == null) return obj.DirectUsage ?? "";
        return "Measure Killer is_used: " + FirstNonEmpty(Json.Str(raw, "is_used"), Json.Str(raw, "status"), "(blank)") +
               "; Visual dependencies: " + Json.Array(raw, "visual_dependencies").Count +
               "; Measure/artifact dependencies: " + Json.Array(raw, "artifact_dependencies").Count +
               "; Relationship dependencies: " + Json.Array(raw, "relationship_dependencies").Count;
    }

    private static string NodeText(LineageObject obj)
    {
        return "[" + Prefix(obj.EffectiveUsage) + "] " + obj.Name;
    }

    private static string Prefix(EffectiveUsage usage)
    {
        switch (usage)
        {
            case EffectiveUsage.Keep: return "KEEP";
            case EffectiveUsage.CascadeCandidate: return "CASCADE";
            case EffectiveUsage.RelationshipOnly: return "REL";
            case EffectiveUsage.RelationshipIsland: return "ISLAND";
            case EffectiveUsage.Unused: return "UNUSED";
            default: return "REVIEW";
        }
    }

    private static string UsageLabel(EffectiveUsage usage)
    {
        switch (usage)
        {
            case EffectiveUsage.Keep: return "Keep";
            case EffectiveUsage.CascadeCandidate: return "Cascade candidate";
            case EffectiveUsage.RelationshipOnly: return "Relationship only";
            case EffectiveUsage.RelationshipIsland: return "Relationship island";
            case EffectiveUsage.Unused: return "Unused";
            default: return "Review";
        }
    }

    private static string UsageLabel(LineageEdgeKind kind)
    {
        switch (kind)
        {
            case LineageEdgeKind.Visual: return "Visual";
            case LineageEdgeKind.VisualFilter: return "Visual filter";
            case LineageEdgeKind.PageFilter: return "Page filter";
            case LineageEdgeKind.ReportFilter: return "Report filter";
            case LineageEdgeKind.Relationship: return "Relationship";
            case LineageEdgeKind.MeasureDependency: return "Measure dependency";
            case LineageEdgeKind.SortBy: return "Sort by";
            case LineageEdgeKind.Hierarchy: return "Hierarchy";
            case LineageEdgeKind.FieldParameter: return "Field parameter";
            case LineageEdgeKind.CalculationItem: return "Calculation item";
            case LineageEdgeKind.RowLevelSecurity: return "RLS";
            case LineageEdgeKind.Artifact: return "Artifact";
            default: return "Unknown";
        }
    }

    private static Color BackColorForUsage(EffectiveUsage usage)
    {
        switch (usage)
        {
            case EffectiveUsage.Keep: return Color.Honeydew;
            case EffectiveUsage.CascadeCandidate: return Color.LemonChiffon;
            case EffectiveUsage.RelationshipOnly: return Color.LightYellow;
            case EffectiveUsage.RelationshipIsland: return Color.AliceBlue;
            case EffectiveUsage.Unused: return Color.MistyRose;
            default: return Color.Gainsboro;
        }
    }

    private static void AddBestPracticeNodes(MkNode parent, object value)
    {
        var countBefore = parent.Children.Count;
        AddBestPracticeNodesRecursive(parent, value, "");
        if (parent.Children.Count == countBefore)
            parent.Add("(No best practice rows found)", false, "");
    }

    private static void AddBestPracticeNodesRecursive(MkNode parent, object value, string path)
    {
        var dict = value as Dictionary<string, object>;
        if (dict != null)
        {
            if (LooksLikeBestPracticeRow(dict))
            {
                var rule = FirstNonEmpty(Json.Str(dict, "rule_name"), Json.Str(dict, "name"), path);
                var obj = FirstNonEmpty(Json.Str(dict, "object_name"), Json.Str(dict, "object"));
                var node = parent.Add(rule + (string.IsNullOrWhiteSpace(obj) ? "" : " - " + obj), false, FirstNonEmpty(Json.Str(dict, "description"), Json.Str(dict, "message")));
                node.ObjectType = "BestPractice";
                node.SourceObject = dict;
                node.SearchText = TermsText(rule, obj, Json.Str(dict, "severity"), Json.Str(dict, "object_type"), Json.Str(dict, "table_name"));
                return;
            }

            foreach (var pair in dict)
                AddBestPracticeNodesRecursive(parent, pair.Value, string.IsNullOrWhiteSpace(path) ? pair.Key : path + "." + pair.Key);
            return;
        }

        var list = value as ArrayList;
        if (list != null)
            foreach (var item in list) AddBestPracticeNodesRecursive(parent, item, path);
    }

    private static bool LooksLikeBestPracticeRow(Dictionary<string, object> row)
    {
        return row.ContainsKey("severity") || row.ContainsKey("rule_name") || row.ContainsKey("object_name") || row.ContainsKey("message");
    }

    private static void AddLegend(MkNode model)
    {
        var legend = model.Add("Legend / impact colors", ImpactKind.Neutral, "");
        legend.Add("Red: unused by model and reports", ImpactKind.Unused, "Delete/rename should have no known downstream impact.");
        legend.Add("Orange: relationship-key table", ImpactKind.RelationshipOnlyIsolated, "The table's used columns are only relationship keys; other table objects are unused.");
        legend.Add("Yellow: relationship-focused table", ImpactKind.RelationshipOnly, "The table's used columns are only relationship keys, but the table has additional non-column usage.");
        legend.Add("Pink: only used by unused measure", ImpactKind.MeasureOnlyDeadEnd, "Only unused measures reference this object.");
        legend.Add("Purple: report-facing", ImpactKind.Report, "Used by visuals, filters, report artifacts, Excel, mobile, or paginated output.");
        legend.Add("Blue: model dependency", ImpactKind.Model, "Used by model-only dependencies such as sorting, hierarchy, Power Query, RLS, calc tables, or field parameters.");
        legend.Add("Green: used, no special risk category detected", ImpactKind.Used, "");
    }

    private static HashSet<string> BuildUnusedMeasureKeys(Dictionary<string, object> root)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Dictionary<string, object> table in Json.Array(root, "tables"))
        {
            var tableName = Json.Str(table, "name");
            foreach (Dictionary<string, object> measure in Json.Array(table, "measures"))
            {
                if (!IsUnused(Json.Str(measure, "is_used"))) continue;
                var name = Json.Str(measure, "name");
                result.Add(name);
                result.Add(tableName + "." + name);
                result.Add(tableName + "[" + name + "]");
            }
        }
        return result;
    }

    private static ImpactKind ClassifyTable(Dictionary<string, object> table, HashSet<string> unusedMeasures)
    {
        if (TableHasChildReportImpact(table)) return ImpactKind.Report;
        if (TableHasChildMeasureOnlyDeadEnd(table, unusedMeasures)) return ImpactKind.MeasureOnlyDeadEnd;
        if (HasReportImpact(table)) return ImpactKind.Report;
        if (TableColumnsOnlyRelationshipReferences(table))
            return TableHasOnlyUnusedObjectsOrRelationshipColumns(table) ? ImpactKind.RelationshipOnlyIsolated : ImpactKind.RelationshipOnly;
        if (TableHasChildModelImpact(table)) return ImpactKind.Model;
        if (HasModelImpact(table)) return ImpactKind.Model;
        if (TableHasAnyUsedObject(table)) return ImpactKind.Used;
        if (IsUnused(Json.Str(table, "is_used")) || IsUnused(Json.Str(table, "is_referenced"))) return ImpactKind.Unused;
        return ImpactKind.Used;
    }

    private static bool TableHasChildReportImpact(Dictionary<string, object> table)
    {
        foreach (Dictionary<string, object> column in Json.Array(table, "columns"))
            if (HasReportImpact(column)) return true;
        foreach (Dictionary<string, object> measure in Json.Array(table, "measures"))
            if (HasReportImpact(measure)) return true;
        return false;
    }

    private static bool TableHasChildModelImpact(Dictionary<string, object> table)
    {
        foreach (Dictionary<string, object> column in Json.Array(table, "columns"))
            if (!IsOnlyRelationshipUse(column) && HasModelImpact(column)) return true;
        foreach (Dictionary<string, object> measure in Json.Array(table, "measures"))
            if (HasModelImpact(measure)) return true;
        return false;
    }

    private static bool TableHasChildMeasureOnlyDeadEnd(Dictionary<string, object> table, HashSet<string> unusedMeasures)
    {
        foreach (Dictionary<string, object> column in Json.Array(table, "columns"))
            if (IsOnlyUnusedMeasureUse(column, unusedMeasures)) return true;
        foreach (Dictionary<string, object> measure in Json.Array(table, "measures"))
            if (IsOnlyUnusedMeasureUse(measure, unusedMeasures)) return true;
        return false;
    }

    private static bool TableHasAnyUsedObject(Dictionary<string, object> table)
    {
        foreach (Dictionary<string, object> column in Json.Array(table, "columns"))
            if (!IsUnused(Json.Str(column, "is_used"))) return true;
        foreach (Dictionary<string, object> measure in Json.Array(table, "measures"))
            if (!IsUnused(Json.Str(measure, "is_used"))) return true;
        return false;
    }

    private static ImpactKind ClassifyObject(Dictionary<string, object> obj, Dictionary<string, object> table, HashSet<string> unusedMeasures)
    {
        if (IsUnused(Json.Str(obj, "is_used"))) return ImpactKind.Unused;
        if (HasReportImpact(obj)) return ImpactKind.Report;
        if (IsOnlyUnusedMeasureUse(obj, unusedMeasures)) return ImpactKind.MeasureOnlyDeadEnd;
        if (HasModelImpact(obj)) return ImpactKind.Model;
        return ImpactKind.Used;
    }

    private static bool TableColumnsOnlyRelationshipReferences(Dictionary<string, object> table)
    {
        var hasRelationshipColumn = false;
        foreach (Dictionary<string, object> column in Json.Array(table, "columns"))
        {
            if (IsUnused(Json.Str(column, "is_used"))) continue;
            if (!IsOnlyRelationshipUse(column)) return false;
            hasRelationshipColumn = true;
        }
        return hasRelationshipColumn;
    }

    private static bool TableHasOnlyUnusedObjectsOrRelationshipColumns(Dictionary<string, object> table)
    {
        foreach (Dictionary<string, object> column in Json.Array(table, "columns"))
            if (!IsUnused(Json.Str(column, "is_used")) && !IsOnlyRelationshipUse(column)) return false;
        foreach (Dictionary<string, object> measure in Json.Array(table, "measures"))
            if (!IsUnused(Json.Str(measure, "is_used"))) return false;
        return true;
    }

    private static bool IsOnlyRelationshipUse(Dictionary<string, object> obj)
    {
        if (!Bool(obj, "is_used_relationships") && Json.Array(obj, "relationship_dependencies").Count == 0) return false;
        return !HasAnyUseExcept(obj, new[] { "is_used_relationships" }) &&
               CountDependencyArraysExcept(obj, new[] { "relationship_dependencies" }) == 0;
    }

    private static bool IsOnlyUnusedMeasureUse(Dictionary<string, object> obj, HashSet<string> unusedMeasures)
    {
        var artifactDependencies = Json.Array(obj, "artifact_dependencies");
        if (artifactDependencies.Count == 0) return false;
        if (HasAnyUseExcept(obj, new[] { "is_used_artifacts" })) return false;
        if (CountDependencyArraysExcept(obj, new[] { "artifact_dependencies" }) != 0) return false;

        foreach (Dictionary<string, object> dependency in artifactDependencies)
        {
            if (!Json.Str(dependency, "reference_type").Equals("Measure", StringComparison.OrdinalIgnoreCase)) return false;
            var measure = Json.Str(dependency, "artifact_name");
            var table = Json.Str(dependency, "table_name");
            if (!unusedMeasures.Contains(measure) && !unusedMeasures.Contains(table + "." + measure) && !unusedMeasures.Contains(table + "[" + measure + "]"))
                return false;
        }
        return true;
    }

    private static bool HasReportImpact(Dictionary<string, object> obj)
    {
        return Bool(obj, "is_used_visuals") ||
               Bool(obj, "is_used_visual_level_filters") ||
               Bool(obj, "is_used_page_level_filters") ||
               Bool(obj, "is_used_report_level_filters") ||
               Bool(obj, "is_used_visual_calculations") ||
               Bool(obj, "is_used_mobile") ||
               Bool(obj, "is_used_paginated") ||
               Bool(obj, "is_used_excel") ||
               Json.Array(obj, "visual_dependencies").Count > 0 ||
               Json.Array(obj, "visual_level_filters_dependencies").Count > 0 ||
               Json.Array(obj, "page_level_filters_dependencies").Count > 0 ||
               Json.Array(obj, "report_level_filters_dependencies").Count > 0 ||
               Json.Array(obj, "visual_calculations_dependencies").Count > 0 ||
               Json.Array(obj, "mobile_layout_dependencies").Count > 0;
    }

    private static bool HasModelImpact(Dictionary<string, object> obj)
    {
        return Bool(obj, "is_used_model") ||
               Bool(obj, "is_used_power_query") ||
               Bool(obj, "is_used_sorting") ||
               Bool(obj, "is_used_calc_tables") ||
               Bool(obj, "is_used_hierarchy") ||
               Bool(obj, "is_used_field_parameters") ||
               Bool(obj, "is_used_calculation_items") ||
               Bool(obj, "is_used_model_extension") ||
               Bool(obj, "is_used_key") ||
               Bool(obj, "is_used_default_label") ||
               Bool(obj, "is_used_row_level_security") ||
               Bool(obj, "is_used_dynamic_param") ||
               Bool(obj, "is_used_change_detection") ||
               Bool(obj, "is_used_udf") ||
               Bool(obj, "is_used_calendars") ||
               Bool(obj, "is_used_kpis") ||
               Json.Array(obj, "model_dependencies").Count > 0 ||
               Json.Array(obj, "power_query_dependencies").Count > 0 ||
               Json.Array(obj, "sorting_dependencies").Count > 0 ||
               Json.Array(obj, "calc_table_dependencies").Count > 0 ||
               Json.Array(obj, "hierarchy_dependencies").Count > 0 ||
               Json.Array(obj, "field_parameter_dependencies").Count > 0 ||
               Json.Array(obj, "calculation_item_dependencies").Count > 0 ||
               Json.Array(obj, "model_extension_dependencies").Count > 0 ||
               Json.Array(obj, "key_column_dependencies").Count > 0 ||
               Json.Array(obj, "default_label_dependencies").Count > 0 ||
               Json.Array(obj, "row_level_security_dependencies").Count > 0 ||
               Json.Array(obj, "dynamic_param_dependencies").Count > 0 ||
               Json.Array(obj, "change_detection_dependencies").Count > 0 ||
               Json.Array(obj, "udf_dependencies").Count > 0 ||
               Json.Array(obj, "calendar_dependencies").Count > 0 ||
               Json.Array(obj, "kpi_dependencies").Count > 0;
    }

    private static bool HasAnyUseExcept(Dictionary<string, object> obj, string[] allowed)
    {
        var allowedSet = new HashSet<string>(allowed, StringComparer.OrdinalIgnoreCase);
        foreach (var pair in obj)
        {
            if (!pair.Key.StartsWith("is_used_", StringComparison.OrdinalIgnoreCase)) continue;
            if (allowedSet.Contains(pair.Key)) continue;
            if (BoolValue(pair.Value)) return true;
        }
        return false;
    }

    private static int CountDependencyArraysExcept(Dictionary<string, object> obj, string[] allowed)
    {
        var allowedSet = new HashSet<string>(allowed, StringComparer.OrdinalIgnoreCase);
        var count = 0;
        foreach (var pair in obj)
        {
            if (!pair.Key.EndsWith("_dependencies", StringComparison.OrdinalIgnoreCase)) continue;
            if (allowedSet.Contains(pair.Key)) continue;
            var list = pair.Value as ArrayList;
            if (list != null) count += list.Count;
        }
        return count;
    }

    private static bool Bool(Dictionary<string, object> obj, string key)
    {
        return BoolValue(Json.Value(obj, key));
    }

    private static bool BoolValue(object value)
    {
        if (value is bool) return (bool)value;
        return value != null && Convert.ToString(value).Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildTooltip(Dictionary<string, object> obj, ImpactKind impact)
    {
        var parts = new List<string>();
        parts.Add(ImpactDescription(impact));
        var description = Json.Str(obj, "description");
        if (!string.IsNullOrWhiteSpace(description)) parts.Add(description);
        var expression = Json.Str(obj, "expression");
        if (!string.IsNullOrWhiteSpace(expression)) parts.Add(expression);
        return string.Join("\r\n\r\n", parts.ToArray());
    }

    private static string ImpactLabel(ImpactKind impact)
    {
        switch (impact)
        {
            case ImpactKind.Unused: return "Unused";
            case ImpactKind.RelationshipOnlyIsolated: return "Relationship-key table";
            case ImpactKind.RelationshipOnly: return "Relationship-focused table";
            case ImpactKind.MeasureOnlyDeadEnd: return "Only unused measure";
            case ImpactKind.Report: return "Report impact";
            case ImpactKind.Model: return "Model impact";
            case ImpactKind.Relationship: return "Relationship";
            default: return "Used";
        }
    }

    private static string ImpactDescription(ImpactKind impact)
    {
        switch (impact)
        {
            case ImpactKind.Unused: return "No known model or report dependency.";
            case ImpactKind.RelationshipOnlyIsolated: return "The table's used columns are only relationship keys; other table objects are unused.";
            case ImpactKind.RelationshipOnly: return "The table's used columns are only relationship keys, but the table has additional non-column usage.";
            case ImpactKind.MeasureOnlyDeadEnd: return "Only unused measures depend on this object.";
            case ImpactKind.Report: return "Report artifacts or visuals depend on this object. Rename/delete can affect report pages, visuals, or filters.";
            case ImpactKind.Model: return "Model metadata depends on this object. Rename/delete can affect sorting, hierarchy, Power Query, RLS, calc tables, field parameters, or similar model behavior.";
            case ImpactKind.Relationship: return "Relationship is used in the model.";
            default: return "Used, but no specific dependency category was identified.";
        }
    }

    private static void AddImpactChildren(MkNode node, Dictionary<string, object> obj)
    {
        AddDependencyFolder(node, "Report visuals", ImpactKind.Report, obj, "visual_dependencies", new[] { "report_name", "page", "visual_type", "used_as" });
        AddDependencyFolder(node, "Visual filters", ImpactKind.Report, obj, "visual_level_filters_dependencies", new[] { "report_name", "page", "visual_type", "filter_type" });
        AddDependencyFolder(node, "Report filters", ImpactKind.Report, obj, "page_level_filters_dependencies", new[] { "report_name", "page", "filter_type" });
        AddDependencyFolder(node, "Report-level filters", ImpactKind.Report, obj, "report_level_filters_dependencies", new[] { "report_name", "filter_type" });
        AddDependencyFolder(node, "Relationships", ImpactKind.Relationship, obj, "relationship_dependencies", new[] { "table1", "column1", "table2", "column2", "relationship_type" });
        AddDependencyFolder(node, "Measure/artifact dependencies", ImpactKind.MeasureOnlyDeadEnd, obj, "artifact_dependencies", new[] { "reference_type", "table_name", "artifact_name", "report_name" });
        AddDependencyFolder(node, "Sorting", ImpactKind.Model, obj, "sorting_dependencies", new[] { "from_table", "sorted_column", "table", "column" });
        AddDependencyFolder(node, "Power Query", ImpactKind.Model, obj, "power_query_dependencies", new[] { "table_name", "name", "expression" });
        AddDependencyFolder(node, "Hierarchy", ImpactKind.Model, obj, "hierarchy_dependencies", new[] { "table_name", "hierarchy_name", "level_name" });
        AddDependencyFolder(node, "Model dependencies", ImpactKind.Model, obj, "model_dependencies", new[] { "table_name", "object_name", "object_type", "name" });
    }

    private static void AddDependencyFolder(MkNode node, string title, ImpactKind impact, Dictionary<string, object> obj, string key, string[] fields)
    {
        var dependencies = Json.Array(obj, key);
        if (dependencies.Count == 0) return;
            var folder = node.Add(title + " (" + dependencies.Count + ")", impact, title);
            foreach (Dictionary<string, object> dependency in dependencies)
            {
                var text = DependencyText(dependency, fields);
                var child = folder.Add(text, impact, text);
                child.SourceObject = dependency;
                child.SearchText = TermsText(text, title);
            }
    }

    private static string DependencyText(Dictionary<string, object> dependency, string[] fields)
    {
        var values = new List<string>();
        foreach (var field in fields)
        {
            var value = Json.Str(dependency, field);
            if (!string.IsNullOrWhiteSpace(value)) values.Add(value);
        }
        return values.Count == 0 ? "(dependency)" : string.Join(" / ", values.ToArray());
    }

    private void RenderTree(bool expandVisible, bool scrollToRoot)
    {
        RememberCurrentExpansion();
        var selectedPath = tree.SelectedNode == null ? "" : tree.SelectedNode.Name;

        suppressExpansionTracking = true;
        tree.BeginUpdate();
        try
        {
            tree.Nodes.Clear();
            var textTerms = Terms(txtFilter.Text);
            var selectedTerms = GetSelectedObjectTerms();
            foreach (var root in allRoots)
            {
                var node = BuildTreeNode(root, rememberedExpandedPaths, textTerms, selectedTerms, false);
                if (node != null) tree.Nodes.Add(node);
            }
            if (expandVisible || expandAfterSelectionChange)
            {
                tree.ExpandAll();
                RememberCurrentExpansion();
                expandAfterSelectionChange = false;
            }
            if (!string.IsNullOrEmpty(selectedPath)) SelectNodeByPath(tree.Nodes, selectedPath);
        }
        finally
        {
            tree.EndUpdate();
            suppressExpansionTracking = false;
        }

        if (scrollToRoot) ScrollTreeToRoot();
        else if (tree.SelectedNode != null) tree.SelectedNode.EnsureVisible();
    }

    private void ScrollTreeToRoot()
    {
        if (tree.Nodes.Count == 0) return;
        if (tree.IsHandleCreated)
        {
            tree.BeginInvoke(new Action(delegate
            {
                if (tree.Nodes.Count == 0) return;
                tree.TopNode = tree.Nodes[0];
                tree.SelectedNode = tree.Nodes[0];
                tree.Nodes[0].EnsureVisible();
            }));
        }
    }

    private TreeNode BuildTreeNode(MkNode source, HashSet<string> expanded, string[] textTerms, List<string[]> selectedTerms, bool includeWholeSubtree)
    {
        var childNodes = new List<TreeNode>();
        var sourceMatches = MatchesFilter(source, textTerms, selectedTerms);
        var includeChildrenUnfiltered = includeWholeSubtree || (selectedTerms.Count > 0 && sourceMatches);
        foreach (var child in source.Children)
        {
            var childNode = BuildTreeNode(child, expanded, textTerms, selectedTerms, includeChildrenUnfiltered);
            if (childNode != null) childNodes.Add(childNode);
        }

        var includeThisNode = includeWholeSubtree ? CheckboxFiltersAllow(source) : sourceMatches;
        if (!includeThisNode && childNodes.Count == 0) return null;

        var node = new TreeNode(source.Text);
        node.Tag = source;
        node.Name = source.Path;
        node.ToolTipText = source.Tooltip ?? "";
        node.BackColor = BackColorForImpact(source.Impact);
        node.ForeColor = ForeColorForImpact(source.Impact);
        foreach (var childNode in childNodes) node.Nodes.Add(childNode);
        if (includeWholeSubtree || includeChildrenUnfiltered || expanded.Contains(source.Path)) node.Expand();
        return node;
    }

    private static Color BackColorForImpact(ImpactKind impact)
    {
        switch (impact)
        {
            case ImpactKind.Unused: return Color.MistyRose;
            case ImpactKind.RelationshipOnlyIsolated: return Color.PeachPuff;
            case ImpactKind.RelationshipOnly: return Color.LemonChiffon;
            case ImpactKind.MeasureOnlyDeadEnd: return Color.LavenderBlush;
            case ImpactKind.Report: return Color.Plum;
            case ImpactKind.Model: return Color.LightSteelBlue;
            case ImpactKind.Relationship: return Color.Khaki;
            case ImpactKind.Neutral: return Color.WhiteSmoke;
            default: return Color.Honeydew;
        }
    }

    private static Color ForeColorForImpact(ImpactKind impact)
    {
        switch (impact)
        {
            case ImpactKind.Unused: return Color.DarkRed;
            case ImpactKind.RelationshipOnlyIsolated: return Color.SaddleBrown;
            case ImpactKind.RelationshipOnly: return Color.DarkGoldenrod;
            case ImpactKind.MeasureOnlyDeadEnd: return Color.MediumVioletRed;
            case ImpactKind.Report: return Color.Indigo;
            case ImpactKind.Model: return Color.DarkBlue;
            case ImpactKind.Relationship: return Color.DarkGoldenrod;
            case ImpactKind.Neutral: return Color.Black;
            default: return Color.DarkGreen;
        }
    }

    private bool MatchesFilter(MkNode node, string[] textTerms, List<string[]> selected)
    {
        var search = NormalizeForSearch(node.SearchText + " " + node.Text);

        if (textTerms.Length > 0 && !ContainsAllTerms(search, textTerms)) return false;
        if (!CheckboxFiltersAllow(node)) return false;

        if (selected.Count > 0)
        {
            foreach (var terms in selected)
                if (ContainsAllTerms(search, terms)) return true;
            return false;
        }

        return true;
    }

    private bool CheckboxFiltersAllow(MkNode node)
    {
        return ImpactFilterAllows(node) && ObjectTypeFilterAllows(node);
    }

    private bool ImpactFilterAllows(MkNode node)
    {
        if (node.Impact == ImpactKind.Neutral) return true;
        foreach (var check in impactFilterChecks)
        {
            if (check.Checked && check.Tag is ImpactKind && (ImpactKind)check.Tag == node.Impact)
                return true;
        }
        return false;
    }

    private bool ObjectTypeFilterAllows(MkNode node)
    {
        if (string.IsNullOrWhiteSpace(node.ObjectType)) return true;
        foreach (var check in objectTypeFilterChecks)
        {
            if (check.Checked && string.Equals(Convert.ToString(check.Tag), node.ObjectType, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static HashSet<string> GetExpandedNodePaths(TreeNodeCollection nodes)
    {
        var result = new HashSet<string>();
        CollectExpanded(nodes, result);
        return result;
    }

    private static void CollectExpanded(TreeNodeCollection nodes, HashSet<string> result)
    {
        foreach (TreeNode node in nodes)
        {
            if (node.IsExpanded) result.Add(node.Name);
            CollectExpanded(node.Nodes, result);
        }
    }

    private void RememberCurrentExpansion()
    {
        foreach (var path in GetExpandedNodePaths(tree.Nodes))
            rememberedExpandedPaths.Add(path);
    }

    private void RemoveRememberedExpansion(TreeNode node)
    {
        if (node == null) return;
        if (!string.IsNullOrEmpty(node.Name)) rememberedExpandedPaths.Remove(node.Name);
        foreach (TreeNode child in node.Nodes) RemoveRememberedExpansion(child);
    }

    private static bool SelectNodeByPath(TreeNodeCollection nodes, string path)
    {
        foreach (TreeNode node in nodes)
        {
            if (string.Equals(node.Name, path, StringComparison.Ordinal))
            {
                node.TreeView.SelectedNode = node;
                return true;
            }
            if (SelectNodeByPath(node.Nodes, path)) return true;
        }
        return false;
    }

    private void OpenJson()
    {
        if (File.Exists(JsonPath)) System.Diagnostics.Process.Start("notepad.exe", JsonPath);
    }

    private void UpdateDetailsPanel()
    {
        if (tree.SelectedNode == null)
        {
            txtDetails.Text = "";
            return;
        }

        var node = tree.SelectedNode.Tag as MkNode;
        if (node == null)
        {
            txtDetails.Text = tree.SelectedNode.Text;
            return;
        }

        var builder = new StringBuilder();
        builder.AppendLine("Object");
        builder.AppendLine("------");
        builder.AppendLine(node.Text);
        builder.AppendLine("Impact: " + ImpactLabel(node.Impact));
        if (!string.IsNullOrWhiteSpace(node.ObjectType)) builder.AppendLine("Type: " + node.ObjectType);
        if (!string.IsNullOrWhiteSpace(node.Tooltip))
        {
            builder.AppendLine();
            builder.AppendLine("Summary");
            builder.AppendLine("-------");
            builder.AppendLine(WrapText(node.Tooltip, 96));
        }

        if (node.SourceObject != null)
        {
            builder.AppendLine();
            if (chkFullDetails.Checked)
            {
                builder.AppendLine("Full Definition");
                builder.AppendLine("---------------");
                AppendValue(builder, node.SourceObject, 0);
            }
            else
            {
                builder.AppendLine("Lineage");
                builder.AppendLine("-------");
                AppendLineageDetails(builder, node.SourceObject);
            }
        }

        txtDetails.Text = builder.ToString();
        txtDetails.SelectionStart = 0;
        txtDetails.SelectionLength = 0;
        UpdateVisualPreview(node);
    }

    private void UpdateVisualPreview(MkNode node)
    {
        visualBoxes.Clear();
        if (node != null) CollectVisualBoxes(node.SourceObject, visualBoxes);

        var hasVisuals = visualBoxes.Count > 0;
        visualPreview.Visible = hasVisuals;
        visualPreviewRow.Height = hasVisuals ? 190 : 0;
        visualPreview.Invalidate();
    }

    private static void CollectVisualBoxes(object source, List<VisualBox> target)
    {
        var dict = source as Dictionary<string, object>;
        if (dict == null) return;

        var direct = TryCreateVisualBox(dict);
        if (direct != null)
        {
            target.Add(direct);
            return;
        }

        foreach (Dictionary<string, object> visual in Json.Array(dict, "visual_dependencies"))
        {
            var box = TryCreateVisualBox(visual);
            if (box != null) target.Add(box);
        }
    }

    private static VisualBox TryCreateVisualBox(Dictionary<string, object> dict)
    {
        var coordinates = Json.Str(dict, "coordinates");
        var hasVisualIdentity =
            !string.IsNullOrWhiteSpace(Json.Str(dict, "visual_id")) ||
            !string.IsNullOrWhiteSpace(Json.Str(dict, "visual_type")) ||
            !string.IsNullOrWhiteSpace(Json.Str(dict, "used_as"));
        if (!hasVisualIdentity) return null;

        double x;
        double y;
        if (!TryReadCoordinate(coordinates, "x", out x)) x = ReadDouble(dict, "x", 0);
        if (!TryReadCoordinate(coordinates, "y", out y)) y = ReadDouble(dict, "y", 0);

        var width = ReadDouble(dict, "width", 0);
        var height = ReadDouble(dict, "height", 0);
        if (width <= 0 || height <= 0) return null;

        var title = Json.Str(dict, "visual_title");
        if (string.IsNullOrWhiteSpace(title)) title = Json.Str(dict, "visual_type");
        if (string.IsNullOrWhiteSpace(title)) title = Json.Str(dict, "visual_id");

        return new VisualBox
        {
            X = x,
            Y = y,
            Width = width,
            Height = height,
            Title = title,
            Page = Json.Str(dict, "page"),
            UsedAs = Json.Str(dict, "used_as")
        };
    }

    private static bool TryReadCoordinate(string coordinates, string key, out double value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(coordinates)) return false;
        var parts = coordinates.Split(',');
        foreach (var part in parts)
        {
            var pair = part.Split('=');
            if (pair.Length != 2) continue;
            if (!pair[0].Trim().Equals(key, StringComparison.OrdinalIgnoreCase)) continue;
            return TryParseDouble(pair[1], out value);
        }
        return false;
    }

    private static double ReadDouble(Dictionary<string, object> dict, string key, double defaultValue)
    {
        if (!dict.ContainsKey(key) || dict[key] == null) return defaultValue;
        double result;
        return TryParseDouble(Convert.ToString(dict[key]), out result) ? result : defaultValue;
    }

    private static bool TryParseDouble(string value, out double result)
    {
        result = 0;
        if (string.IsNullOrWhiteSpace(value)) return false;
        var normalized = value.Trim().Replace(',', '.');
        return double.TryParse(normalized, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out result);
    }

    private void PaintVisualPreview(object sender, PaintEventArgs e)
    {
        e.Graphics.Clear(Color.White);
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using (var textBrush = new SolidBrush(Color.DimGray))
        using (var pagePen = new Pen(Color.Silver))
        using (var visualBrush = new SolidBrush(Color.FromArgb(80, Color.Plum)))
        using (var visualPen = new Pen(Color.Indigo, 2f))
        using (var font = new Font("Segoe UI", 8f))
        {
            e.Graphics.DrawString("Visual coordinates", font, textBrush, new PointF(8, 7));
            if (visualBoxes.Count == 0) return;

            var bounds = GetVisualBounds(visualBoxes);
            if (bounds.Width <= 0 || bounds.Height <= 0) return;

            var canvas = new RectangleF(8, 26, visualPreview.ClientSize.Width - 16, visualPreview.ClientSize.Height - 34);
            if (canvas.Width <= 10 || canvas.Height <= 10) return;

            var scale = Math.Min(canvas.Width / (float)bounds.Width, canvas.Height / (float)bounds.Height);
            var pageWidth = (float)bounds.Width * scale;
            var pageHeight = (float)bounds.Height * scale;
            var page = new RectangleF(
                canvas.Left + (canvas.Width - pageWidth) / 2f,
                canvas.Top + (canvas.Height - pageHeight) / 2f,
                pageWidth,
                pageHeight);
            e.Graphics.DrawRectangle(pagePen, page.X, page.Y, page.Width, page.Height);

            foreach (var box in visualBoxes)
            {
                var rect = new RectangleF(
                    page.Left + (float)(box.X - bounds.X) * scale,
                    page.Top + (float)(box.Y - bounds.Y) * scale,
                    Math.Max(2f, (float)box.Width * scale),
                    Math.Max(2f, (float)box.Height * scale));
                e.Graphics.FillRectangle(visualBrush, rect);
                e.Graphics.DrawRectangle(visualPen, rect.X, rect.Y, rect.Width, rect.Height);
                var label = string.IsNullOrWhiteSpace(box.UsedAs) ? box.Title : box.Title + " / " + box.UsedAs;
                if (!string.IsNullOrWhiteSpace(label))
                    e.Graphics.DrawString(label, font, Brushes.Black, new RectangleF(rect.Left + 3, rect.Top + 3, Math.Max(30, rect.Width - 6), Math.Max(14, rect.Height - 6)));
            }
        }
    }

    private static RectangleF GetVisualBounds(List<VisualBox> boxes)
    {
        var left = double.MaxValue;
        var top = double.MaxValue;
        var right = double.MinValue;
        var bottom = double.MinValue;

        foreach (var box in boxes)
        {
            left = Math.Min(left, box.X);
            top = Math.Min(top, box.Y);
            right = Math.Max(right, box.X + box.Width);
            bottom = Math.Max(bottom, box.Y + box.Height);
        }

        if (left == double.MaxValue) return RectangleF.Empty;
        if (left > 0) left = 0;
        if (top > 0) top = 0;
        return new RectangleF((float)left, (float)top, (float)(right - left), (float)(bottom - top));
    }

    private static void AppendLineageDetails(StringBuilder builder, object source)
    {
        var dict = source as Dictionary<string, object>;
        if (dict == null)
        {
            builder.AppendLine("No lineage fields are available for this node.");
            return;
        }

        var wroteAny = false;
        wroteAny |= AppendScalarIfPresent(builder, dict, "model_name", "Model");
        wroteAny |= AppendScalarIfPresent(builder, dict, "table_name", "Table");
        wroteAny |= AppendScalarIfPresent(builder, dict, "name", "Name");
        wroteAny |= AppendScalarIfPresent(builder, dict, "reference_type", "Reference type");
        wroteAny |= AppendScalarIfPresent(builder, dict, "artifact_name", "Artifact");
        wroteAny |= AppendScalarIfPresent(builder, dict, "report_name", "Report");
        wroteAny |= AppendScalarIfPresent(builder, dict, "page_name", "Page");
        wroteAny |= AppendScalarIfPresent(builder, dict, "visual_name", "Visual");
        wroteAny |= AppendScalarIfPresent(builder, dict, "expression", "Expression");

        var dependencyKeys = new[]
        {
            "relationship_dependencies",
            "artifact_dependencies",
            "visual_dependencies",
            "visual_level_filters_dependencies",
            "page_level_filters_dependencies",
            "report_level_filters_dependencies",
            "visual_calculations_dependencies",
            "mobile_layout_dependencies",
            "model_dependencies",
            "power_query_dependencies",
            "sorting_dependencies",
            "calc_table_dependencies",
            "hierarchy_dependencies",
            "field_parameter_dependencies",
            "calculation_item_dependencies",
            "row_level_security_dependencies",
            "key_dependencies"
        };

        foreach (var key in dependencyKeys)
            wroteAny |= AppendDependencyIfPresent(builder, dict, key);

        if (!wroteAny)
            builder.AppendLine("No lineage fields are available for this node. Enable Full details for the raw JSON object.");
    }

    private static bool AppendScalarIfPresent(StringBuilder builder, Dictionary<string, object> dict, string key, string label)
    {
        if (!dict.ContainsKey(key) || dict[key] == null) return false;
        var value = Convert.ToString(dict[key]);
        if (string.IsNullOrWhiteSpace(value)) return false;
        builder.Append(label);
        builder.Append(": ");
        builder.AppendLine(WrapText(value, 96));
        return true;
    }

    private static bool AppendDependencyIfPresent(StringBuilder builder, Dictionary<string, object> dict, string key)
    {
        var list = Json.Array(dict, key);
        if (list.Count == 0) return false;

        builder.AppendLine();
        builder.AppendLine(ToTitle(key) + " (" + list.Count + ")");
        builder.AppendLine(new string('-', Math.Min(72, key.Length + 4)));
        AppendValue(builder, list, 2);
        return true;
    }

    private static string ToTitle(string key)
    {
        var text = key.Replace("_dependencies", "").Replace("_", " ");
        if (text.Length == 0) return key;
        return char.ToUpperInvariant(text[0]) + text.Substring(1);
    }

    private static string WrapText(string value, int width)
    {
        if (string.IsNullOrEmpty(value) || width < 20) return value ?? "";
        var normalized = value.Replace("\r\n", "\n").Replace('\r', '\n');
        var lines = normalized.Split('\n');
        var result = new StringBuilder();
        foreach (var line in lines)
        {
            var remaining = line;
            while (remaining.Length > width)
            {
                var split = remaining.LastIndexOf(' ', width);
                if (split <= 0) split = width;
                result.AppendLine(remaining.Substring(0, split).TrimEnd());
                remaining = remaining.Substring(split).TrimStart();
            }
            result.AppendLine(remaining);
        }
        return result.ToString().TrimEnd();
    }

    private static void AppendValue(StringBuilder builder, object value, int indent)
    {
        var dict = value as Dictionary<string, object>;
        if (dict != null)
        {
            foreach (var pair in dict)
            {
                AppendIndent(builder, indent);
                builder.Append(pair.Key);
                builder.Append(": ");
                if (pair.Value is Dictionary<string, object> || pair.Value is ArrayList)
                {
                    builder.AppendLine();
                    AppendValue(builder, pair.Value, indent + 2);
                }
                else
                {
                    builder.AppendLine(pair.Value == null ? "null" : Convert.ToString(pair.Value));
                }
            }
            return;
        }

        var list = value as ArrayList;
        if (list != null)
        {
            var index = 1;
            foreach (var item in list)
            {
                AppendIndent(builder, indent);
                builder.AppendLine("- item " + index++);
                AppendValue(builder, item, indent + 2);
            }
            return;
        }

        AppendIndent(builder, indent);
        builder.AppendLine(value == null ? "null" : Convert.ToString(value));
    }

    private static void AppendIndent(StringBuilder builder, int indent)
    {
        for (var i = 0; i < indent; i++) builder.Append(' ');
    }

    private void RefreshSelectionFilterIfNeeded()
    {
        var key = GetSelectionKey();
        if (string.Equals(key, lastSelectionKey, StringComparison.Ordinal)) return;
        lastSelectionKey = key;
        UpdateSelectionLabel();
        if (chkSelectedObject.Checked)
        {
            expandAfterSelectionChange = true;
            ApplyFilters();
        }
    }

    private void UpdateSelectionLabel()
    {
        if (!chkSelectedObject.Checked)
        {
            lblSelection.Text = "Selected object filter is off.";
            return;
        }

        var objects = GetSelectedObjects().ToList();
        if (objects.Count == 0) lblSelection.Text = "No object selected in the main window.";
        else if (objects.Count == 1) lblSelection.Text = "Selected object: " + ObjectLabel(objects[0]);
        else lblSelection.Text = "Selected objects: " + objects.Count;
    }

    private string GetSelectionKey()
    {
        var labels = new List<string>();
        foreach (var selectedObject in GetSelectedObjects())
            labels.Add(ObjectLabel(selectedObject));
        return string.Join("|", labels.ToArray());
    }

    private IEnumerable<object> GetSelectedObjects()
    {
        try
        {
            var treeView = GetPropertyObject(context, "TreeView");
            var selectedNodes = GetPropertyObject(treeView, "SelectedNodes") as IEnumerable;
            if (selectedNodes == null) return Enumerable.Empty<object>();

            var result = new List<object>();
            foreach (var node in selectedNodes)
            {
                var item = GetPropertyObject(node, "Tag");
                if (item == null || GetProperty(item, "ObjectType") == "Model") continue;
                result.Add(item);
            }
            return result;
        }
        catch
        {
            return Enumerable.Empty<object>();
        }
    }

    private List<string[]> GetSelectedObjectTerms()
    {
        var result = new List<string[]>();
        if (!chkSelectedObject.Checked) return result;

        foreach (var obj in GetSelectedObjects())
        {
            var terms = new List<string>();
            AddSearchTerms(terms, GetProperty(obj, "Name"));
            AddSearchTerms(terms, GetProperty(obj, "ObjectType"));

            var table = GetPropertyObject(obj, "Table");
            AddSearchTerms(terms, GetProperty(table, "Name"));
            AddSearchTerms(terms, GetProperty(obj, "DaxObjectFullName"));
            AddSearchTerms(terms, GetProperty(table, "DaxObjectFullName"));

            var distinct = terms.Distinct().ToArray();
            if (distinct.Length > 0) result.Add(distinct);
        }
        return result;
    }

    private static object GetPropertyObject(object instance, string propertyName)
    {
        if (instance == null) return null;
        var property = instance.GetType().GetProperty(propertyName);
        return property == null ? null : property.GetValue(instance, null);
    }

    private static string GetProperty(object instance, string propertyName)
    {
        if (instance == null) return "";
        var property = instance.GetType().GetProperty(propertyName);
        if (property == null) return "";
        var value = property.GetValue(instance, null);
        return value == null ? "" : Convert.ToString(value);
    }

    private static string ObjectLabel(object obj)
    {
        if (obj == null) return "";
        return GetProperty(obj, "ObjectType") + " " + GetProperty(obj, "Name");
    }

    private static bool IsUnused(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        return value.Equals("false", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("unused", StringComparison.OrdinalIgnoreCase) ||
               value.IndexOf("unused", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool ContainsAllTerms(string search, IEnumerable<string> terms)
    {
        foreach (var term in terms)
            if (search.IndexOf(term, StringComparison.OrdinalIgnoreCase) < 0)
                return false;
        return true;
    }

    private static string[] Terms(string value)
    {
        return NormalizeForSearch(value).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
    }

    private static string TermsText(params string[] values)
    {
        return NormalizeForSearch(string.Join(" ", values.Where(v => !string.IsNullOrWhiteSpace(v)).ToArray()));
    }

    private static void AddSearchTerms(List<string> terms, string value)
    {
        foreach (var term in Terms(value))
            if (term.Length > 1) terms.Add(term);
    }

    private static string NormalizeForSearch(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value.ToLowerInvariant())
            builder.Append(char.IsLetterOrDigit(ch) ? ch : ' ');
        return string.Join(" ", builder.ToString().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
    }

    private static string FirstNonEmpty(params string[] values)
    {
        foreach (var value in values)
            if (!string.IsNullOrWhiteSpace(value)) return value;
        return "";
    }
}

internal enum EffectiveUsage
{
    Keep,
    CascadeCandidate,
    RelationshipOnly,
    RelationshipIsland,
    Unused,
    Review
}

internal enum LineageEdgeKind
{
    MeasureDependency,
    Visual,
    VisualFilter,
    PageFilter,
    ReportFilter,
    Relationship,
    SortBy,
    Hierarchy,
    FieldParameter,
    CalculationItem,
    RowLevelSecurity,
    Artifact,
    Unknown
}

internal class LineageObject
{
    public string Key;
    public string ObjectType;
    public string TableName;
    public string Name;
    public string DirectUsage;
    public EffectiveUsage EffectiveUsage;
    public string Reason;
    public string Recommendation;
    public string SearchText;
    public object SourceObject;
    public List<LineageEdge> UsedBy = new List<LineageEdge>();
    public Dictionary<string, string> RawProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

internal class LineageEdge
{
    public string FromKey;
    public string ToKey;
    public LineageEdgeKind Kind;
    public string Label;
}

internal enum ImpactKind
{
    Neutral,
    Used,
    Unused,
    Relationship,
    RelationshipOnly,
    RelationshipOnlyIsolated,
    MeasureOnlyDeadEnd,
    Model,
    Report
}

internal class MkNode
{
    public readonly string Text;
    public readonly ImpactKind Impact;
    public readonly string Tooltip;
    public readonly List<MkNode> Children = new List<MkNode>();
    public string SearchText;
    public string Path;
    public string ObjectType;
    public object SourceObject;
    public bool IsUnused { get { return Impact == ImpactKind.Unused; } }

    public MkNode(string text, bool isUnused, string tooltip)
        : this(text, isUnused ? ImpactKind.Unused : ImpactKind.Used, tooltip)
    {
    }

    public MkNode(string text, ImpactKind impact, string tooltip)
    {
        Text = text;
        Impact = impact;
        Tooltip = tooltip;
        SearchText = text + " " + tooltip;
        Path = text;
    }

    public MkNode Add(string text, bool isUnused, string tooltip)
    {
        var child = new MkNode(text, isUnused, tooltip);
        child.Path = Path + "/" + text;
        Children.Add(child);
        return child;
    }

    public MkNode Add(string text, ImpactKind impact, string tooltip)
    {
        var child = new MkNode(text, impact, tooltip);
        child.Path = Path + "/" + text;
        Children.Add(child);
        return child;
    }
}

internal class VisualBox
{
    public double X;
    public double Y;
    public double Width;
    public double Height;
    public string Title;
    public string Page;
    public string UsedAs;
}

internal static class Json
{
    public static Dictionary<string, object> ReadObject(string text)
    {
        return new Parser(text).ParseObject();
    }

    public static object Value(Dictionary<string, object> dict, string key)
    {
        object value;
        return dict != null && dict.TryGetValue(key, out value) ? value : null;
    }

    public static string Str(Dictionary<string, object> dict, string key)
    {
        var value = Value(dict, key);
        return value == null ? "" : Convert.ToString(value);
    }

    public static ArrayList Array(Dictionary<string, object> dict, string key)
    {
        return Value(dict, key) as ArrayList ?? new ArrayList();
    }

    private sealed class Parser
    {
        private readonly string text;
        private int index;

        public Parser(string text)
        {
            this.text = text ?? "";
        }

        public Dictionary<string, object> ParseObject()
        {
            SkipWhiteSpace();
            return ReadObject();
        }

        private object ReadValue()
        {
            SkipWhiteSpace();
            if (index >= text.Length) throw new FormatException("Unexpected end of JSON.");
            var ch = text[index];
            if (ch == '{') return ReadObject();
            if (ch == '[') return ReadArray();
            if (ch == '"') return ReadString();
            if (ch == 't') return ReadLiteral("true", true);
            if (ch == 'f') return ReadLiteral("false", false);
            if (ch == 'n') return ReadLiteral("null", null);
            return ReadNumber();
        }

        private Dictionary<string, object> ReadObject()
        {
            Expect('{');
            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            SkipWhiteSpace();
            if (TryRead('}')) return result;

            while (true)
            {
                SkipWhiteSpace();
                var key = ReadString();
                SkipWhiteSpace();
                Expect(':');
                result[key] = ReadValue();
                SkipWhiteSpace();
                if (TryRead('}')) return result;
                Expect(',');
            }
        }

        private ArrayList ReadArray()
        {
            Expect('[');
            var result = new ArrayList();
            SkipWhiteSpace();
            if (TryRead(']')) return result;

            while (true)
            {
                result.Add(ReadValue());
                SkipWhiteSpace();
                if (TryRead(']')) return result;
                Expect(',');
            }
        }

        private string ReadString()
        {
            Expect('"');
            var builder = new StringBuilder();
            while (index < text.Length)
            {
                var ch = text[index++];
                if (ch == '"') return builder.ToString();
                if (ch != '\\')
                {
                    builder.Append(ch);
                    continue;
                }

                if (index >= text.Length) throw new FormatException("Invalid JSON string escape.");
                var escaped = text[index++];
                switch (escaped)
                {
                    case '"': builder.Append('"'); break;
                    case '\\': builder.Append('\\'); break;
                    case '/': builder.Append('/'); break;
                    case 'b': builder.Append('\b'); break;
                    case 'f': builder.Append('\f'); break;
                    case 'n': builder.Append('\n'); break;
                    case 'r': builder.Append('\r'); break;
                    case 't': builder.Append('\t'); break;
                    case 'u':
                        if (index + 4 > text.Length) throw new FormatException("Invalid JSON unicode escape.");
                        builder.Append((char)Convert.ToInt32(text.Substring(index, 4), 16));
                        index += 4;
                        break;
                    default:
                        throw new FormatException("Invalid JSON string escape.");
                }
            }
            throw new FormatException("Unterminated JSON string.");
        }

        private object ReadNumber()
        {
            var start = index;
            if (text[index] == '-') index++;
            while (index < text.Length && char.IsDigit(text[index])) index++;
            var isDecimal = false;
            if (index < text.Length && text[index] == '.')
            {
                isDecimal = true;
                index++;
                while (index < text.Length && char.IsDigit(text[index])) index++;
            }
            if (index < text.Length && (text[index] == 'e' || text[index] == 'E'))
            {
                isDecimal = true;
                index++;
                if (index < text.Length && (text[index] == '+' || text[index] == '-')) index++;
                while (index < text.Length && char.IsDigit(text[index])) index++;
            }

            var value = text.Substring(start, index - start);
            if (isDecimal)
            {
                double d;
                if (double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out d)) return d;
            }
            else
            {
                long l;
                if (long.TryParse(value, out l)) return l;
            }
            throw new FormatException("Invalid JSON number.");
        }

        private object ReadLiteral(string literal, object value)
        {
            if (index + literal.Length > text.Length || text.Substring(index, literal.Length) != literal)
                throw new FormatException("Invalid JSON literal.");
            index += literal.Length;
            return value;
        }

        private void Expect(char ch)
        {
            SkipWhiteSpace();
            if (index >= text.Length || text[index] != ch)
                throw new FormatException("Expected '" + ch + "'.");
            index++;
        }

        private bool TryRead(char ch)
        {
            SkipWhiteSpace();
            if (index >= text.Length || text[index] != ch) return false;
            index++;
            return true;
        }

        private void SkipWhiteSpace()
        {
            while (index < text.Length && char.IsWhiteSpace(text[index])) index++;
        }
    }
}
