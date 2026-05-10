using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;

internal partial class MeasureKillerViewerForm
{
    private static readonly string[] VisualDependencyKeys =
    {
        "visual_dependencies",
        "visual_level_filters_dependencies",
        "page_level_filters_dependencies",
        "report_level_filters_dependencies",
        "visual_calculations_dependencies",
        "mobile_layout_dependencies"
    };

    private static readonly string[] ReportUsageFlags =
    {
        "is_used_visuals",
        "is_used_visual_level_filters",
        "is_used_page_level_filters",
        "is_used_report_level_filters",
        "is_used_visual_calculations",
        "is_used_mobile",
        "is_used_paginated",
        "is_used_excel"
    };

    private static readonly string[] ModelUsageFlags =
    {
        "is_used_model",
        "is_used_power_query",
        "is_used_sorting",
        "is_used_calc_tables",
        "is_used_hierarchy",
        "is_used_field_parameters",
        "is_used_calculation_items",
        "is_used_model_extension",
        "is_used_key",
        "is_used_default_label",
        "is_used_row_level_security",
        "is_used_dynamic_param",
        "is_used_change_detection",
        "is_used_udf",
        "is_used_calendars",
        "is_used_kpis"
    };

    private static readonly string[] ModelDependencyKeys =
    {
        "model_dependencies",
        "power_query_dependencies",
        "sorting_dependencies",
        "calc_table_dependencies",
        "hierarchy_dependencies",
        "field_parameter_dependencies",
        "calculation_item_dependencies",
        "model_extension_dependencies",
        "key_column_dependencies",
        "default_label_dependencies",
        "row_level_security_dependencies",
        "dynamic_param_dependencies",
        "change_detection_dependencies",
        "udf_dependencies",
        "calendar_dependencies",
        "kpi_dependencies"
    };

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

        ClassifyDuplicateVisuals();
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
            var role = FirstNonEmpty(Json.Str(dep, "used_as"), UsageLabel(kind));
            visual.DirectUsage = UsageLabel(kind);
            AddEdge(obj, visual, kind, role);
            AddReportVisualBox(dep);
        }
    }

    private void AddReportVisualBox(Dictionary<string, object> dep)
    {
        var box = TryCreateVisualBox(dep, false);
        if (box == null) return;
        var key = box.Page + "|" + box.VisualId + "|" + box.X + "|" + box.Y + "|" + box.Width + "|" + box.Height;
        foreach (var existing in allReportVisualBoxes)
        {
            var existingKey = existing.Page + "|" + existing.VisualId + "|" + existing.X + "|" + existing.Y + "|" + existing.Width + "|" + existing.Height;
            if (existingKey.Equals(key, StringComparison.OrdinalIgnoreCase)) return;
        }
        allReportVisualBoxes.Add(box);
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

    private void ClassifyDuplicateVisuals()
    {
        var groups = new Dictionary<string, List<LineageObject>>(StringComparer.OrdinalIgnoreCase);
        foreach (var visual in lineageObjects)
        {
            if (!visual.ObjectType.Equals("Visual", StringComparison.OrdinalIgnoreCase)) continue;
            var signature = VisualDuplicateSignature(visual);
            if (string.IsNullOrWhiteSpace(signature)) continue;

            List<LineageObject> matches;
            if (!groups.TryGetValue(signature, out matches))
            {
                matches = new List<LineageObject>();
                groups[signature] = matches;
            }
            matches.Add(visual);
        }

        foreach (var group in groups.Values)
        {
            if (group.Count < 2) continue;
            var locations = string.Join(", ", group.Select(v => string.IsNullOrWhiteSpace(v.TableName) ? v.Name : v.TableName + " / " + v.Name).ToArray());
            foreach (var visual in group)
            {
                visual.EffectiveUsage = EffectiveUsage.DuplicateVisual;
                visual.Reason = "This visual has the same visual type and field wells as another visual: " + locations + ".";
                visual.Recommendation = "Review whether these visuals are intentionally repeated. If not, remove or consolidate the duplicate visual.";
            }
        }
    }

    private string VisualDuplicateSignature(LineageObject visual)
    {
        var raw = visual.SourceObject as Dictionary<string, object>;
        var visualType = raw == null ? "" : Json.Str(raw, "visual_type");
        if (string.IsNullOrWhiteSpace(visualType)) visualType = visual.Name;

        var inputs = new List<string>();
        foreach (var producer in lineageObjects)
        {
            foreach (var edge in producer.UsedBy)
            {
                if (!edge.ToKey.Equals(visual.Key, StringComparison.OrdinalIgnoreCase)) continue;
                var role = string.IsNullOrWhiteSpace(edge.Label) ? UsageLabel(edge.Kind) : edge.Label;
                inputs.Add(role + ":" + producer.ObjectType + ":" + producer.TableName + ":" + producer.Name);
            }
        }

        if (inputs.Count == 0) return "";
        inputs.Sort(StringComparer.OrdinalIgnoreCase);
        return visualType + "|" + string.Join("|", inputs.ToArray());
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
            case EffectiveUsage.DuplicateVisual: return "DUP";
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
            case EffectiveUsage.DuplicateVisual: return "Duplicate visual";
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
            case EffectiveUsage.DuplicateVisual: return Color.Lavender;
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
        return AnyTrueFlag(obj, ReportUsageFlags) || AnyDependency(obj, VisualDependencyKeys);
    }

    private static bool HasModelImpact(Dictionary<string, object> obj)
    {
        return AnyTrueFlag(obj, ModelUsageFlags) || AnyDependency(obj, ModelDependencyKeys);
    }

    private static bool AnyTrueFlag(Dictionary<string, object> obj, IEnumerable<string> keys)
    {
        foreach (var key in keys)
            if (Bool(obj, key)) return true;
        return false;
    }

    private static bool AnyDependency(Dictionary<string, object> obj, IEnumerable<string> keys)
    {
        foreach (var key in keys)
            if (Json.Array(obj, key).Count > 0) return true;
        return false;
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

    private static bool IsUnused(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        return value.Equals("false", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("unused", StringComparison.OrdinalIgnoreCase) ||
               value.IndexOf("unused", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string FirstNonEmpty(params string[] values)
    {
        foreach (var value in values)
            if (!string.IsNullOrWhiteSpace(value)) return value;
        return "";
    }
}

