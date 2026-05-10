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
    private void UpdateVisualPreview(LineageObject obj)
    {
        visualBoxes.Clear();
        allPreviewBoxes.Clear();
        visualPreviewPages.Clear();
        visualPreviewPageIndex = 0;
        visualPreviewTitle = "Visual coordinates";
        if (obj != null) CollectPreviewVisualBoxes(obj, allPreviewBoxes);
        RebuildPreviewPage();
    }

    private void CollectPreviewVisualBoxes(LineageObject selected, List<VisualBox> target)
    {
        if (selected == null) return;

        if (selected.ObjectType.Equals("Visual", StringComparison.OrdinalIgnoreCase))
        {
            var selectedBox = TryCreateVisualBox(selected.SourceObject as Dictionary<string, object>, false);
            var selectedPage = selectedBox == null ? selected.TableName : selectedBox.Page;
            foreach (var box in allReportVisualBoxes)
            {
                if (!box.Page.Equals(selectedPage, StringComparison.OrdinalIgnoreCase)) continue;
                target.Add(new VisualBox
                {
                    X = box.X,
                    Y = box.Y,
                    Width = box.Width,
                    Height = box.Height,
                    Title = box.Title,
                    Page = box.Page,
                    UsedAs = box.UsedAs,
                    VisualId = box.VisualId,
                    Highlight = selectedBox != null && box.VisualId.Equals(selectedBox.VisualId, StringComparison.OrdinalIgnoreCase)
                });
            }
            return;
        }

        CollectVisualBoxes(selected.SourceObject, target, false);
    }

    private void RebuildPreviewPage()
    {
        visualBoxes.Clear();
        visualPreviewPages.Clear();
        visualPreviewPages.AddRange(allPreviewBoxes
            .Select(b => string.IsNullOrWhiteSpace(b.Page) ? "(unknown page)" : b.Page)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(p => p));

        if (visualPreviewPages.Count == 0)
        {
            visualPreviewContainer.Visible = false;
            visualPreview.Visible = false;
            visualPreviewRow.Height = 0;
            lblPreviewPage.Text = "";
            visualPreview.Invalidate();
            return;
        }

        if (visualPreviewPageIndex < 0) visualPreviewPageIndex = visualPreviewPages.Count - 1;
        if (visualPreviewPageIndex >= visualPreviewPages.Count) visualPreviewPageIndex = 0;

        var selectedPage = visualPreviewPages[visualPreviewPageIndex];
        foreach (var box in allPreviewBoxes)
        {
            var page = string.IsNullOrWhiteSpace(box.Page) ? "(unknown page)" : box.Page;
            if (page.Equals(selectedPage, StringComparison.OrdinalIgnoreCase)) visualBoxes.Add(box);
        }

        visualPreviewTitle = "Visual coordinates - " + selectedPage + " (" + (visualPreviewPageIndex + 1) + " of " + visualPreviewPages.Count + " pages)";
        lblPreviewPage.Text = visualPreviewTitle;
        btnPreviewPrevious.Enabled = visualPreviewPages.Count > 1;
        btnPreviewNext.Enabled = visualPreviewPages.Count > 1;
        visualPreviewContainer.Visible = true;
        visualPreview.Visible = true;
        visualPreviewRow.Height = 286;
        visualPreview.Invalidate();
    }

    private void ChangePreviewPage(int delta)
    {
        if (visualPreviewPages.Count <= 1) return;
        visualPreviewPageIndex += delta;
        RebuildPreviewPage();
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
        CollectVisualBoxes(source, target, false);
    }

    private static void CollectVisualBoxes(object source, List<VisualBox> target, bool highlight)
    {
        var dict = source as Dictionary<string, object>;
        if (dict == null) return;

        var direct = TryCreateVisualBox(dict, highlight);
        if (direct != null)
        {
            target.Add(direct);
            return;
        }

        foreach (Dictionary<string, object> visual in Json.Array(dict, "visual_dependencies"))
        {
            var box = TryCreateVisualBox(visual, highlight);
            if (box != null) target.Add(box);
        }
    }

    private static VisualBox TryCreateVisualBox(Dictionary<string, object> dict, bool highlight)
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
            UsedAs = Json.Str(dict, "used_as"),
            VisualId = Json.Str(dict, "visual_id"),
            Highlight = highlight
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
        using (var visualBrush = new SolidBrush(Color.FromArgb(70, Color.LightSteelBlue)))
        using (var selectedBrush = new SolidBrush(Color.FromArgb(130, Color.Plum)))
        using (var visualPen = new Pen(Color.SteelBlue, 1f))
        using (var selectedPen = new Pen(Color.Indigo, 3f))
        using (var font = new Font("Segoe UI", 8f))
        {
            var pageName = visualBoxes.Count == 0 ? "" : visualBoxes[0].Page;
            var title = visualPreviewTitle;
            if (title == "Visual coordinates" && !string.IsNullOrWhiteSpace(pageName)) title += " - " + pageName;
            e.Graphics.DrawString(title, font, textBrush, new PointF(8, 7));
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
                e.Graphics.FillRectangle(box.Highlight ? selectedBrush : visualBrush, rect);
                e.Graphics.DrawRectangle(box.Highlight ? selectedPen : visualPen, rect.X, rect.Y, rect.Width, rect.Height);
                var label = string.IsNullOrWhiteSpace(box.UsedAs) ? box.Title : box.Title + " / " + box.UsedAs;
                if (!string.IsNullOrWhiteSpace(label))
                    e.Graphics.DrawString(label, font, Brushes.Black, new RectangleF(rect.Left + 3, rect.Top + 3, Math.Max(30, rect.Width - 6), Math.Max(14, rect.Height - 6)));
            }
        }
    }

    private static RectangleF GetVisualBounds(List<VisualBox> boxes)
    {
        var left = 0.0;
        var top = 0.0;
        var right = 1280.0;
        var bottom = 720.0;

        foreach (var box in boxes)
        {
            left = Math.Min(left, box.X);
            top = Math.Min(top, box.Y);
            right = Math.Max(right, box.X + box.Width);
            bottom = Math.Max(bottom, box.Y + box.Height);
        }

        return new RectangleF((float)left, (float)top, (float)(right - left), (float)(bottom - top));
    }
}
