using System;
using System.Drawing;
using System.Windows.Forms;
using TabularEditor.Plugins.Infrastructure;

namespace TabularEditor.Plugins.ModelTreeColor
{
    public class ModelTreeColorPlugin : IRuntimeWindowPlugin
    {
        public Form CreateWindow(PluginHostContext context)
        {
            return new ModelTreeColorForm(context);
        }
    }

    internal class ModelTreeColorForm : Form
    {
        private readonly PluginHostContext context;
        private readonly Label lblModelKey = new Label { AutoSize = true };
        private readonly Panel pnlPreview = new Panel
        {
            Height = 36,
            Width = 120,
            BorderStyle = BorderStyle.FixedSingle
        };
        private readonly Button btnChoose = new Button { Text = "Choose Color", AutoSize = true };
        private readonly Button btnReset = new Button { Text = "Reset", AutoSize = true };
        private readonly Button btnCopyText = new Button { Text = "Copy as Text", AutoSize = true };

        public ModelTreeColorForm(PluginHostContext context)
        {
            this.context = context;

            Text = "Model Tree Color";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            Width = 420;
            Height = 190;

            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                Padding = new Padding(12),
                WrapContents = false
            };

            lblModelKey.Margin = new Padding(0, 0, 0, 10);
            flow.Controls.Add(lblModelKey);
            flow.Controls.Add(pnlPreview);

            var buttonPanel = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                Margin = new Padding(0, 12, 0, 0)
            };
            buttonPanel.Controls.Add(btnChoose);
            buttonPanel.Controls.Add(btnReset);
            buttonPanel.Controls.Add(btnCopyText);
            flow.Controls.Add(buttonPanel);

            Controls.Add(flow);

            btnChoose.Click += BtnChoose_Click;
            btnReset.Click += BtnReset_Click;
            btnCopyText.Click += BtnCopyText_Click;
            Load += (s, e) => RefreshState();
        }

        private void BtnChoose_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(context.GetCurrentModelIdentityKey()))
            {
                MessageBox.Show("Connect to a model before configuring per-model tree color.", "No connected model", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var initialColor = pnlPreview.BackColor;
            using (var dialog = new ColorDialog { Color = initialColor, FullOpen = true })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                context.SetCurrentModelTreeColor(dialog.Color);
                context.ApplyCurrentModelTreeColor();
                RefreshState();
            }
        }

        private void BtnReset_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(context.GetCurrentModelIdentityKey()))
            {
                MessageBox.Show("Connect to a model before configuring per-model tree color.", "No connected model", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            context.ClearCurrentModelTreeColor();
            context.ApplyCurrentModelTreeColor();
            RefreshState();
        }

        private void RefreshState()
        {
            var key = context.GetCurrentModelIdentityKey();
            lblModelKey.Text = string.IsNullOrWhiteSpace(key)
                ? "Model key: (not connected)"
                : $"Model key: {key}";

            if (context.TryGetCurrentModelTreeColor(out var color))
                pnlPreview.BackColor = color;
            else
                pnlPreview.BackColor = SystemColors.Window;
        }

        private void BtnCopyText_Click(object sender, EventArgs e)
        {
            var key = context.GetCurrentModelIdentityKey();
            var hasColor = context.TryGetCurrentModelTreeColor(out var color);
            var colorText = hasColor ? ColorTranslator.ToHtml(color) : "(default)";
            var text = string.IsNullOrWhiteSpace(key)
                ? $"Model key: (not connected){Environment.NewLine}Tree color: {colorText}"
                : $"Model key: {key}{Environment.NewLine}Tree color: {colorText}";
            Clipboard.SetText(text);
        }
    }
}
