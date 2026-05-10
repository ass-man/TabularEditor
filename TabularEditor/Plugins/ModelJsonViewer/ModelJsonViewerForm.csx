using System;
using System.Drawing;
using System.Windows.Forms;

internal partial class ModelJsonViewerForm : Form
{
    private readonly PluginHostContext context;
    private readonly TextBox txtJson;
    private readonly Timer refreshTimer;
    private readonly Panel searchPanel;
    private readonly TextBox txtSearch;
    private readonly Label lblSearchStatus;
    private string lastScriptText = string.Empty;

    public ModelJsonViewerForm(PluginHostContext context)
    {
        this.context = context;

        Text = "Model JSON/TMSL Viewer";
        Width = 1000;
        Height = 700;

        txtJson = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            Font = new Font("Consolas", 9f)
        };
        txtJson.KeyDown += HandleGlobalKeyDown;
        Controls.Add(txtJson);

        var toolStrip = new ToolStrip();
        var btnRefresh = new ToolStripButton("Refresh");
        var btnCopyText = new ToolStripButton("Copy as Text");
        btnRefresh.Click += (s, e) => RefreshContent();
        btnCopyText.Click += (s, e) => CopyAsText();
        toolStrip.Items.Add(btnRefresh);
        toolStrip.Items.Add(btnCopyText);
        toolStrip.Dock = DockStyle.Top;
        Controls.Add(toolStrip);

        searchPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 36,
            Visible = false
        };
        var searchFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(6, 6, 6, 4)
        };
        searchFlow.Controls.Add(new Label
        {
            AutoSize = true,
            Margin = new Padding(0, 5, 6, 0),
            Text = "Find:"
        });

        txtSearch = new TextBox { Width = 280 };
        txtSearch.KeyDown += SearchBox_KeyDown;
        txtSearch.TextChanged += (s, e) => lblSearchStatus.Text = string.Empty;
        searchFlow.Controls.Add(txtSearch);

        var btnFindNext = new Button { AutoSize = true, Text = "Next" };
        btnFindNext.Click += (s, e) => FindNext();
        searchFlow.Controls.Add(btnFindNext);

        var btnFindPrev = new Button { AutoSize = true, Text = "Previous" };
        btnFindPrev.Click += (s, e) => FindPrevious();
        searchFlow.Controls.Add(btnFindPrev);

        var btnCloseSearch = new Button { AutoSize = true, Text = "X" };
        btnCloseSearch.Click += (s, e) => HideSearchBar();
        searchFlow.Controls.Add(btnCloseSearch);

        lblSearchStatus = new Label
        {
            AutoSize = true,
            Margin = new Padding(8, 5, 0, 0)
        };
        searchFlow.Controls.Add(lblSearchStatus);

        searchPanel.Controls.Add(searchFlow);
        Controls.Add(searchPanel);

        KeyPreview = true;
        KeyDown += HandleGlobalKeyDown;

        refreshTimer = new Timer { Interval = 1000 };
        refreshTimer.Tick += (s, e) =>
        {
            RefreshContent();
        };
        refreshTimer.Start();

        FormClosed += (s, e) => DisposeSubscriptions();

        RefreshContent();
    }

    private void RefreshContent()
    {
        try
        {
            var currentText = context.ScriptCurrentModelCreateOrReplace();
            if (string.Equals(currentText, lastScriptText, StringComparison.Ordinal)) return;

            lastScriptText = currentText;
            txtJson.Text = currentText;
        }
        catch (Exception ex)
        {
            txtJson.Text = "Failed to script model.\r\n\r\n" + ex;
        }
    }

    private void DisposeSubscriptions()
    {
        refreshTimer.Stop();
        refreshTimer.Dispose();
    }

    private void CopyAsText()
    {
        Clipboard.SetText(txtJson.Text ?? string.Empty);
    }
}
