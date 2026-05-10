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
        var previousKey = SelectedLineageObject() == null ? "" : SelectedLineageObject().Key;
        var previousAutoSize = gridObjects.AutoSizeColumnsMode;
        gridObjects.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
        var searchTerms = Terms(txtFilter.Text);
        var status = Convert.ToString(cboStatus.SelectedItem ?? "All");
        var type = Convert.ToString(cboObjectType.SelectedItem ?? "All");
        var table = Convert.ToString(cboTable.SelectedItem ?? "All tables");
        var selectedKeys = chkSelectedObject.Checked ? GetCachedSelectedAffectedKeys() : null;

        var rows = new List<DataGridViewRow>();
        foreach (var obj in lineageObjects)
        {
            if (!StatusMatches(obj, status)) continue;
            if (type != "All" && !obj.ObjectType.Equals(type, StringComparison.OrdinalIgnoreCase)) continue;
            if (table != "All tables" && !obj.TableName.Equals(table, StringComparison.OrdinalIgnoreCase) && !(obj.ObjectType == "Table" && obj.Name.Equals(table, StringComparison.OrdinalIgnoreCase))) continue;
            if (searchTerms.Length > 0 && !ContainsAllTerms(obj.SearchText, searchTerms)) continue;
            if (selectedKeys != null && selectedKeys.Count > 0 && !selectedKeys.Contains(obj.Key)) continue;

            var row = new DataGridViewRow();
            row.CreateCells(
                gridObjects,
                UsageLabel(obj.EffectiveUsage),
                obj.ObjectType,
                obj.TableName,
                obj.Name,
                obj.DirectUsage,
                obj.UsedBy.Count,
                obj.Reason);
            row.Tag = obj;
            row.DefaultCellStyle.BackColor = BackColorForUsage(obj.EffectiveUsage);
            row.DefaultCellStyle.ForeColor = Color.Black;
            rows.Add(row);
        }

        suppressGridSelectionChanged = true;
        gridObjects.SuspendLayout();
        try
        {
            gridObjects.Rows.Clear();
            if (rows.Count > 0) gridObjects.Rows.AddRange(rows.ToArray());
            SelectGridRow(previousKey);
            if (gridObjects.Rows.Count > 0 && gridObjects.CurrentCell == null)
                gridObjects.CurrentCell = gridObjects.Rows[0].Cells[0];
        }
        finally
        {
            gridObjects.AutoSizeColumnsMode = previousAutoSize;
            gridObjects.ResumeLayout();
            suppressGridSelectionChanged = false;
        }

        UpdateStatusCounts(rows);
        UpdateSelectedObjectDetails();
    }

    private void ScheduleFilter()
    {
        filterTimer.Stop();
        filterTimer.Start();
    }

    private static bool StatusMatches(LineageObject obj, string status)
    {
        return status == "All" || UsageLabel(obj.EffectiveUsage).Equals(status, StringComparison.OrdinalIgnoreCase);
    }

    private void UpdateStatusCounts(List<DataGridViewRow> visibleRows)
    {
        var counts = new Dictionary<EffectiveUsage, int>();
        foreach (DataGridViewRow row in visibleRows)
        {
            var obj = row.Tag as LineageObject;
            if (obj == null) continue;
            int count;
            counts.TryGetValue(obj.EffectiveUsage, out count);
            counts[obj.EffectiveUsage] = count + 1;
        }
        foreach (var pair in statusCountLabels)
            pair.Value.Text = Convert.ToString(pair.Value.Tag) + ": " + (counts.ContainsKey(pair.Key) ? counts[pair.Key] : 0);
    }

    private void SelectGridRow(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return;
        foreach (DataGridViewRow row in gridObjects.Rows)
        {
            var obj = row.Tag as LineageObject;
            if (obj == null || !obj.Key.Equals(key, StringComparison.OrdinalIgnoreCase)) continue;
            row.Selected = true;
            gridObjects.CurrentCell = row.Cells[0];
            return;
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
}
