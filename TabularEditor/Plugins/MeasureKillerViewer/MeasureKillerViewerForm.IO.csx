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
    private void LoadJson()
    {
        allRoots.Clear();
        tree.Nodes.Clear();
        lineageObjects.Clear();
        lineageByKey.Clear();
        allReportVisualBoxes.Clear();
        InvalidateSelectedAffectedCache();
        currentJsonPath = FindLatestJsonPath();

        if (!File.Exists(currentJsonPath))
        {
            lblStatus.Text = "No Measure Killer JSON export found in " + JsonFolder + ".";
            return;
        }

        try
        {
            var root = Json.ReadObject(File.ReadAllText(currentJsonPath));
            BuildLineageModel(root);
            PopulateTableFilter();
            ApplyFilters();
            lblStatus.Text = "Loaded " + Path.GetFileName(currentJsonPath) + ". Objects: " + lineageObjects.Count + ", relationships: " + Json.Array(root, "relationships").Count + ".";
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

    private string FindLatestJsonPath()
    {
        if (!Directory.Exists(JsonFolder)) return "";
        var modelName = GetCurrentModelNameForExport();
        var modelMatch = FindLatestJsonPath(modelName);
        if (!string.IsNullOrWhiteSpace(modelMatch)) return modelMatch;
        return FindLatestJsonPath("");
    }

    private static string FindLatestJsonPath(string modelName)
    {
        string best = "";
        string bestStamp = "";
        DateTime bestWrite = DateTime.MinValue;
        var requireModelName = !string.IsNullOrWhiteSpace(modelName);

        foreach (var file in Directory.GetFiles(JsonFolder, JsonSearchPattern))
        {
            if (requireModelName && !FileNameMatchesModel(file, modelName)) continue;
            var stamp = TimestampFromFileName(file);
            var write = File.GetLastWriteTime(file);
            if (string.IsNullOrEmpty(best) ||
                string.Compare(stamp, bestStamp, StringComparison.OrdinalIgnoreCase) > 0 ||
                (string.Equals(stamp, bestStamp, StringComparison.OrdinalIgnoreCase) && write > bestWrite))
            {
                best = file;
                bestStamp = stamp;
                bestWrite = write;
            }
        }

        return best;
    }

    private string GetCurrentModelNameForExport()
    {
        try
        {
            var handler = GetPropertyObject(context, "Handler");
            var model = GetPropertyObject(handler, "Model");
            var modelName = GetProperty(model, "Name");
            if (!string.IsNullOrWhiteSpace(modelName)) return modelName;
        }
        catch
        {
        }
        return "";
    }

    private static bool FileNameMatchesModel(string file, string modelName)
    {
        var name = Path.GetFileNameWithoutExtension(file);
        return !string.IsNullOrWhiteSpace(name) &&
               name.StartsWith(modelName + "_", StringComparison.OrdinalIgnoreCase);
    }

    private static string TimestampFromFileName(string file)
    {
        var name = Path.GetFileNameWithoutExtension(file);
        if (string.IsNullOrWhiteSpace(name)) return "";
        var index = name.LastIndexOf('_');
        if (index <= 0 || index >= name.Length - 1) return name;
        var datePart = name.Substring(0, index);
        var secondIndex = datePart.LastIndexOf('_');
        if (secondIndex < 0) return name.Substring(index + 1);
        return datePart.Substring(secondIndex + 1) + "_" + name.Substring(index + 1);
    }

    private void OpenJson()
    {
        if (!File.Exists(currentJsonPath)) currentJsonPath = FindLatestJsonPath();
        if (File.Exists(currentJsonPath)) System.Diagnostics.Process.Start("notepad.exe", currentJsonPath);
    }
}
