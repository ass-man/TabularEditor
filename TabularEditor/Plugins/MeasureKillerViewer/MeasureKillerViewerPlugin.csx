using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

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
    private readonly CheckBox chkFullDetails;
    private readonly Timer selectionTimer;
    private readonly List<CheckBox> impactFilterChecks = new List<CheckBox>();
    private readonly List<CheckBox> objectTypeFilterChecks = new List<CheckBox>();
    private readonly List<MkNode> allRoots = new List<MkNode>();
    private string lastSelectionKey = "";
    private bool expandAfterSelectionChange;

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
            RowCount = 3
        };
        rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 142));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(rootLayout);

        var toolStrip = new ToolStrip { Dock = DockStyle.Fill };
        var btnRefresh = new ToolStripButton("Refresh");
        var btnOpenJson = new ToolStripButton("Open JSON");
        var btnExpandAll = new ToolStripButton("Expand All");
        var btnCollapseAll = new ToolStripButton("Collapse All");
        btnRefresh.Click += delegate { LoadJson(); };
        btnOpenJson.Click += delegate { OpenJson(); };
        btnExpandAll.Click += delegate { RenderTree(); tree.ExpandAll(); };
        btnCollapseAll.Click += delegate { tree.CollapseAll(); };
        chkFullDetails = new CheckBox { AutoSize = true, Text = "Full details", Checked = false };
        chkFullDetails.CheckedChanged += delegate { UpdateDetailsPanel(); };
        toolStrip.Items.Add(btnRefresh);
        toolStrip.Items.Add(btnOpenJson);
        toolStrip.Items.Add(new ToolStripSeparator());
        toolStrip.Items.Add(btnExpandAll);
        toolStrip.Items.Add(btnCollapseAll);
        toolStrip.Items.Add(new ToolStripSeparator());
        toolStrip.Items.Add(new ToolStripControlHost(chkFullDetails));
        rootLayout.Controls.Add(toolStrip, 0, 0);

        var header = new TableLayoutPanel();
        header.Dock = DockStyle.Fill;
        header.ColumnCount = 4;
        header.RowCount = 5;
        header.Padding = new Padding(8, 6, 8, 6);
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));

        header.Controls.Add(new Label { Text = "JSON:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 4, 6, 0) }, 0, 0);
        header.Controls.Add(new TextBox { ReadOnly = true, Dock = DockStyle.Fill, Text = JsonPath }, 1, 0);
        header.Controls.Add(new Label { Text = "Filter:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(12, 4, 6, 0) }, 2, 0);
        txtFilter = new TextBox { Dock = DockStyle.Fill };
        txtFilter.TextChanged += delegate { RenderTree(); };
        header.Controls.Add(txtFilter, 3, 0);

        lblStatus = new Label { AutoSize = true, Dock = DockStyle.Fill, Text = "Loading...", Margin = new Padding(0, 6, 0, 0) };
        header.Controls.Add(lblStatus, 1, 1);
        header.SetColumnSpan(lblStatus, 3);

        chkSelectedObject = new CheckBox { AutoSize = true, Checked = false, Text = "Filter to selected object", Margin = new Padding(0, 6, 6, 0) };
        chkSelectedObject.CheckedChanged += delegate { RenderTree(); UpdateSelectionLabel(); };
        header.Controls.Add(chkSelectedObject, 0, 2);
        lblSelection = new Label { AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 7, 0, 0) };
        header.Controls.Add(lblSelection, 1, 2);
        header.SetColumnSpan(lblSelection, 3);

        var impactFilters = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        impactFilters.Controls.Add(new Label { AutoSize = true, Text = "Impact:", Margin = new Padding(0, 5, 6, 0) });
        AddFilterCheck(impactFilters, impactFilterChecks, "Unused", ImpactKind.Unused);
        AddFilterCheck(impactFilters, impactFilterChecks, "Rel-key table", ImpactKind.RelationshipOnlyIsolated);
        AddFilterCheck(impactFilters, impactFilterChecks, "Rel-focused table", ImpactKind.RelationshipOnly);
        AddFilterCheck(impactFilters, impactFilterChecks, "Unused measure only", ImpactKind.MeasureOnlyDeadEnd);
        AddFilterCheck(impactFilters, impactFilterChecks, "Report", ImpactKind.Report);
        AddFilterCheck(impactFilters, impactFilterChecks, "Model", ImpactKind.Model);
        AddFilterCheck(impactFilters, impactFilterChecks, "Relationship", ImpactKind.Relationship);
        AddFilterCheck(impactFilters, impactFilterChecks, "Used", ImpactKind.Used);
        header.Controls.Add(impactFilters, 0, 3);
        header.SetColumnSpan(impactFilters, 4);

        var typeFilters = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        typeFilters.Controls.Add(new Label { AutoSize = true, Text = "Types:", Margin = new Padding(0, 5, 14, 0) });
        AddFilterCheck(typeFilters, objectTypeFilterChecks, "Tables", "Table");
        AddFilterCheck(typeFilters, objectTypeFilterChecks, "Columns", "Column");
        AddFilterCheck(typeFilters, objectTypeFilterChecks, "Measures", "Measure");
        AddFilterCheck(typeFilters, objectTypeFilterChecks, "Partitions", "Partition");
        AddFilterCheck(typeFilters, objectTypeFilterChecks, "Relationships", "Relationship");
        AddFilterCheck(typeFilters, objectTypeFilterChecks, "Best practices", "BestPractice");
        header.Controls.Add(typeFilters, 0, 4);
        header.SetColumnSpan(typeFilters, 4);
        rootLayout.Controls.Add(header, 0, 1);

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
        tree.AfterSelect += delegate { UpdateDetailsPanel(); };

        txtDetails = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            WordWrap = true,
            Font = new Font("Segoe UI", 9f)
        };

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 820
        };
        split.Panel1.Controls.Add(tree);
        split.Panel2.Controls.Add(txtDetails);
        rootLayout.Controls.Add(split, 0, 2);

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
        check.CheckedChanged += delegate { RenderTree(); };
        target.Add(check);
        panel.Controls.Add(check);
    }

    private void LoadJson()
    {
        allRoots.Clear();
        tree.Nodes.Clear();

        if (!File.Exists(JsonPath))
        {
            lblStatus.Text = "JSON file not found.";
            return;
        }

        try
        {
            var root = Json.ReadObject(File.ReadAllText(JsonPath));
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
            RenderTree();
            tree.ExpandAll();
            ScrollTreeToRoot();
        }
        catch (Exception ex)
        {
            lblStatus.Text = "Failed to load JSON: " + ex.Message;
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

    private void RenderTree()
    {
        var expanded = GetExpandedNodePaths(tree.Nodes);
        tree.BeginUpdate();
        tree.Nodes.Clear();
        var textTerms = Terms(txtFilter.Text);
        var selectedTerms = GetSelectedObjectTerms();
        foreach (var root in allRoots)
        {
            var node = BuildTreeNode(root, expanded, textTerms, selectedTerms, false);
            if (node != null) tree.Nodes.Add(node);
        }
        if (expandAfterSelectionChange)
        {
            tree.ExpandAll();
            expandAfterSelectionChange = false;
        }
        tree.EndUpdate();
        ScrollTreeToRoot();
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
            RenderTree();
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
