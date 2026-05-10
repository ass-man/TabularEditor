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
    private void UpdatePreviewFromUsedByTreeSelection()
    {
        var node = tree.SelectedNode;
        var obj = node == null ? null : node.Tag as LineageObject;
        if (obj == null) return;
        UpdateVisualPreview(obj);
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
}
