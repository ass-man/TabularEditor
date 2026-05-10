using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

internal partial class MeasureKillerViewerForm
{
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
        if (obj.ObjectType.Equals("Visual", StringComparison.OrdinalIgnoreCase))
        {
            builder.AppendLine();
            builder.AppendLine("Visual inputs");
            builder.AppendLine("-------------");
            builder.AppendLine(BuildVisualInputsText(obj));
        }
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

    private string RawDetails(LineageObject obj)
    {
        var builder = new StringBuilder();
        if (obj.SourceObject != null) AppendValue(builder, obj.SourceObject, 0);
        return builder.ToString();
    }

    private string BuildVisualInputsText(LineageObject visual)
    {
        var groups = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var producer in lineageObjects)
        {
            foreach (var edge in producer.UsedBy)
            {
                if (!edge.ToKey.Equals(visual.Key, StringComparison.OrdinalIgnoreCase)) continue;
                var role = string.IsNullOrWhiteSpace(edge.Label) ? UsageLabel(edge.Kind) : edge.Label;
                List<string> fields;
                if (!groups.TryGetValue(role, out fields))
                {
                    fields = new List<string>();
                    groups[role] = fields;
                }
                var field = producer.ObjectType + " / " + (string.IsNullOrWhiteSpace(producer.TableName) ? "" : producer.TableName + " / ") + producer.Name;
                if (!fields.Contains(field)) fields.Add(field);
            }
        }

        if (groups.Count == 0) return "No visual field inputs were found in the Measure Killer export.";

        var order = new[] { "Rows", "Columns", "Values", "X-Axis", "Y-Axis", "Legend", "Category", "Tooltips", "Filters" };
        var builder = new StringBuilder();
        foreach (var role in order)
            AppendVisualInputRole(builder, groups, role);
        foreach (var role in groups.Keys.OrderBy(x => x))
            if (!order.Contains(role, StringComparer.OrdinalIgnoreCase)) AppendVisualInputRole(builder, groups, role);
        return builder.ToString().TrimEnd();
    }

    private static void AppendVisualInputRole(StringBuilder builder, Dictionary<string, List<string>> groups, string role)
    {
        List<string> fields;
        if (!groups.TryGetValue(role, out fields)) return;
        builder.AppendLine(role + ":");
        foreach (var field in fields.OrderBy(x => x))
            builder.AppendLine("  - " + field);
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
}
