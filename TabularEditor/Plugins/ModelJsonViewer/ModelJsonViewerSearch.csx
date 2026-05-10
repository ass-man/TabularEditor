using System;
using System.Windows.Forms;

internal partial class ModelJsonViewerForm
{
    private void HandleGlobalKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Control && e.KeyCode == Keys.F)
        {
            e.SuppressKeyPress = true;
            ShowSearchBar();
            return;
        }

        if (e.KeyCode == Keys.F3)
        {
            e.SuppressKeyPress = true;
            if (e.Shift) FindPrevious();
            else FindNext();
            return;
        }
    }

    private void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            e.SuppressKeyPress = true;
            if (e.Shift) FindPrevious();
            else FindNext();
            return;
        }

        if (e.KeyCode == Keys.Escape)
        {
            e.SuppressKeyPress = true;
            HideSearchBar();
        }
    }

    private void ShowSearchBar()
    {
        searchPanel.Visible = true;
        txtSearch.Focus();
        txtSearch.SelectAll();
    }

    private void HideSearchBar()
    {
        searchPanel.Visible = false;
        lblSearchStatus.Text = string.Empty;
        txtJson.Focus();
    }

    private void FindNext()
    {
        var query = txtSearch.Text;
        if (string.IsNullOrEmpty(query))
        {
            lblSearchStatus.Text = "Type text to search.";
            txtSearch.Focus();
            return;
        }

        var content = txtJson.Text ?? string.Empty;
        if (content.Length == 0)
        {
            lblSearchStatus.Text = "No content.";
            return;
        }

        var startIndex = txtJson.SelectionStart + txtJson.SelectionLength;
        if (startIndex > content.Length) startIndex = content.Length;

        var index = content.IndexOf(query, startIndex, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
            index = content.IndexOf(query, 0, StringComparison.OrdinalIgnoreCase);

        SelectMatch(index, query.Length);
    }

    private void FindPrevious()
    {
        var query = txtSearch.Text;
        if (string.IsNullOrEmpty(query))
        {
            lblSearchStatus.Text = "Type text to search.";
            txtSearch.Focus();
            return;
        }

        var content = txtJson.Text ?? string.Empty;
        if (content.Length == 0)
        {
            lblSearchStatus.Text = "No content.";
            return;
        }

        var startIndex = txtJson.SelectionStart - 1;
        if (startIndex < 0) startIndex = content.Length - 1;
        if (startIndex < 0)
        {
            lblSearchStatus.Text = "No content.";
            return;
        }

        var index = content.LastIndexOf(query, startIndex, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
            index = content.LastIndexOf(query, content.Length - 1, StringComparison.OrdinalIgnoreCase);

        SelectMatch(index, query.Length);
    }

    private void SelectMatch(int index, int length)
    {
        if (index < 0 || length <= 0)
        {
            lblSearchStatus.Text = "No matches.";
            return;
        }

        txtJson.Focus();
        txtJson.SelectionStart = index;
        txtJson.SelectionLength = length;
        txtJson.ScrollToCaret();
        lblSearchStatus.Text = "Match at " + (index + 1) + ".";
    }
}
