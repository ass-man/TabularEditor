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
    private void RefreshSelectionFilterIfNeeded()
    {
        var key = GetSelectionKey();
        if (string.Equals(key, lastSelectionKey, StringComparison.Ordinal)) return;
        lastSelectionKey = key;
        InvalidateSelectedAffectedCache();
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

    private HashSet<string> GetSelectedAffectedKeys()
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var selected in GetSelectedObjects())
        {
            foreach (var key in ResolveSelectedLineageKeys(selected))
                AddAffectedKeys(key, result);
        }
        return result;
    }

    private HashSet<string> GetCachedSelectedAffectedKeys()
    {
        var key = GetSelectionKey();
        if (string.Equals(key, cachedSelectedAffectKey, StringComparison.Ordinal))
            return cachedSelectedAffectedKeys;

        cachedSelectedAffectKey = key;
        cachedSelectedAffectedKeys = GetSelectedAffectedKeys();
        return cachedSelectedAffectedKeys;
    }

    private void InvalidateSelectedAffectedCache()
    {
        cachedSelectedAffectKey = "";
        cachedSelectedAffectedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    private IEnumerable<string> ResolveSelectedLineageKeys(object selected)
    {
        var objectType = GetProperty(selected, "ObjectType");
        var name = GetProperty(selected, "Name");
        var table = GetPropertyObject(selected, "Table");
        var tableName = GetProperty(table, "Name");

        var keys = new List<string>();
        AddResolvedKey(keys, objectType, tableName, name);
        AddResolvedKey(keys, objectType, "", name);

        if (objectType.Equals("Table", StringComparison.OrdinalIgnoreCase))
        {
            AddResolvedKey(keys, "Table", "", name);
            AddResolvedKey(keys, "Table", name, name);
        }

        if (objectType.Equals("Measure", StringComparison.OrdinalIgnoreCase)) AddResolvedKey(keys, "Measure", tableName, name);
        if (objectType.Equals("Column", StringComparison.OrdinalIgnoreCase)) AddResolvedKey(keys, "Column", tableName, name);
        if (objectType.Equals("Relationship", StringComparison.OrdinalIgnoreCase)) AddResolvedKey(keys, "Relationship", "", name);

        var fullName = GetProperty(selected, "DaxObjectFullName");
        if (!string.IsNullOrWhiteSpace(fullName))
        {
            foreach (var obj in lineageObjects)
                if (fullName.IndexOf(obj.Name, StringComparison.OrdinalIgnoreCase) >= 0 &&
                    (string.IsNullOrWhiteSpace(obj.TableName) || fullName.IndexOf(obj.TableName, StringComparison.OrdinalIgnoreCase) >= 0))
                    keys.Add(obj.Key);
        }

        return keys.Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private void AddResolvedKey(List<string> keys, string objectType, string tableName, string name)
    {
        if (string.IsNullOrWhiteSpace(objectType) || string.IsNullOrWhiteSpace(name)) return;
        var key = ObjectKey(objectType, tableName, name);
        if (lineageByKey.ContainsKey(key)) keys.Add(key);
    }

    private void AddAffectedKeys(string key, HashSet<string> result)
    {
        if (string.IsNullOrWhiteSpace(key) || !result.Add(key)) return;
        LineageObject obj;
        if (!lineageByKey.TryGetValue(key, out obj)) return;
        foreach (var edge in obj.UsedBy)
            AddAffectedKeys(edge.ToKey, result);
    }
}
