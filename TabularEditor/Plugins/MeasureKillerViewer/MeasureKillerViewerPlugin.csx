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
    private readonly Timer selectionTimer;
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
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 86));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(rootLayout);

        var toolStrip = new ToolStrip { Dock = DockStyle.Fill };
        var btnRefresh = new ToolStripButton("Refresh");
        var btnOpenJson = new ToolStripButton("Open JSON");
        var btnExpandAll = new ToolStripButton("Expand All");
        var btnCollapseAll = new ToolStripButton("Collapse All");
        btnRefresh.Click += delegate { LoadJson(); };
        btnOpenJson.Click += delegate { OpenJson(); };
        btnExpandAll.Click += delegate { tree.ExpandAll(); };
        btnCollapseAll.Click += delegate { tree.CollapseAll(); };
        toolStrip.Items.Add(btnRefresh);
        toolStrip.Items.Add(btnOpenJson);
        toolStrip.Items.Add(new ToolStripSeparator());
        toolStrip.Items.Add(btnExpandAll);
        toolStrip.Items.Add(btnCollapseAll);
        rootLayout.Controls.Add(toolStrip, 0, 0);

        var header = new TableLayoutPanel();
        header.Dock = DockStyle.Fill;
        header.ColumnCount = 4;
        header.RowCount = 3;
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
        rootLayout.Controls.Add(tree, 0, 2);

        selectionTimer = new Timer { Interval = 500 };
        selectionTimer.Tick += delegate { RefreshSelectionFilterIfNeeded(); };
        selectionTimer.Start();
        FormClosed += delegate { selectionTimer.Stop(); selectionTimer.Dispose(); };
        Load += delegate { LoadJson(); UpdateSelectionLabel(); };
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

            var tablesFolder = model.Add("Tables", false, "");
            var objectCount = 0;
            var unusedCount = 0;

            foreach (Dictionary<string, object> table in Json.Array(root, "tables"))
            {
                var tableName = Json.Str(table, "name");
                var tableNode = tablesFolder.Add(
                    tableName + "  [Table]",
                    IsUnused(Json.Str(table, "is_referenced")),
                    "Storage: " + Json.Str(table, "storage_mode"));
                tableNode.SearchText = TermsText(tableName, "table", Json.Str(table, "storage_mode"));
                objectCount++;
                if (tableNode.IsUnused) unusedCount++;

                var columnsFolder = tableNode.Add("Columns (" + Json.Array(table, "columns").Count + ")", false, "");
                foreach (Dictionary<string, object> column in Json.Array(table, "columns"))
                {
                    var used = Json.Str(column, "is_used");
                    var node = columnsFolder.Add(
                        Json.Str(column, "name") + "  [Column]  " + used,
                        IsUnused(used),
                        Json.Str(column, "description"));
                    node.SearchText = TermsText(Json.Str(column, "name"), tableName, "column", used, Json.Str(column, "description"));
                    objectCount++;
                    if (node.IsUnused) unusedCount++;
                }

                var measuresFolder = tableNode.Add("Measures (" + Json.Array(table, "measures").Count + ")", false, "");
                foreach (Dictionary<string, object> measure in Json.Array(table, "measures"))
                {
                    var used = Json.Str(measure, "is_used");
                    var node = measuresFolder.Add(
                        Json.Str(measure, "name") + "  [Measure]  " + used,
                        IsUnused(used),
                        Json.Str(measure, "expression"));
                    node.SearchText = TermsText(Json.Str(measure, "name"), tableName, "measure", used, Json.Str(measure, "expression"));
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
                    node.SearchText = TermsText(Json.Str(partition, "name"), tableName, "partition", state, Json.Str(partition, "type_str"));
                    objectCount++;
                }
            }

            var relationshipsFolder = model.Add("Relationships (" + Json.Array(root, "relationships").Count + ")", false, "");
            foreach (Dictionary<string, object> relationship in Json.Array(root, "relationships"))
            {
                var used = FirstNonEmpty(Json.Str(relationship, "is_used"), Json.Str(relationship, "status"));
                var name = FirstNonEmpty(Json.Str(relationship, "name"), Json.Str(relationship, "from_column") + " -> " + Json.Str(relationship, "to_column"));
                var node = relationshipsFolder.Add(name + "  [Relationship]  " + used, IsUnused(used), "");
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

        if (!includeWholeSubtree && !sourceMatches && childNodes.Count == 0) return null;

        var node = new TreeNode(source.Text);
        node.Name = source.Path;
        node.ToolTipText = source.Tooltip ?? "";
        node.BackColor = source.IsUnused ? Color.MistyRose : Color.Honeydew;
        node.ForeColor = source.IsUnused ? Color.DarkRed : Color.DarkGreen;
        foreach (var childNode in childNodes) node.Nodes.Add(childNode);
        if (includeWholeSubtree || includeChildrenUnfiltered || expanded.Contains(source.Path)) node.Expand();
        return node;
    }

    private bool MatchesFilter(MkNode node, string[] textTerms, List<string[]> selected)
    {
        var search = NormalizeForSearch(node.SearchText + " " + node.Text);

        if (textTerms.Length > 0 && !ContainsAllTerms(search, textTerms)) return false;

        if (selected.Count > 0)
        {
            foreach (var terms in selected)
                if (ContainsAllTerms(search, terms)) return true;
            return false;
        }

        return true;
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

internal class MkNode
{
    public readonly string Text;
    public readonly bool IsUnused;
    public readonly string Tooltip;
    public readonly List<MkNode> Children = new List<MkNode>();
    public string SearchText;
    public string Path;

    public MkNode(string text, bool isUnused, string tooltip)
    {
        Text = text;
        IsUnused = isUnused;
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
