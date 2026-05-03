using Aga.Controls.Tree;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using TabularEditor.TOMWrapper;
using TabularEditor.UI;
using TabularEditor.UIServices;

namespace TabularEditor.Plugins.Infrastructure
{
    public class PluginRuntimeManager
    {
        private readonly UIController uiController;
        private readonly ToolStripDropDownItem pluginsMenu;
        private readonly string pluginsRoot;
        private readonly PluginLoader loader = new PluginLoader();
        private readonly Dictionary<string, IRuntimeWindowPlugin> loadedPlugins = new Dictionary<string, IRuntimeWindowPlugin>(StringComparer.InvariantCultureIgnoreCase);
        private readonly Dictionary<string, Form> openForms = new Dictionary<string, Form>(StringComparer.InvariantCultureIgnoreCase);
        private readonly List<PluginDescriptor> descriptors;

        public PluginRuntimeManager(UIController uiController, ToolStripDropDownItem pluginsMenu)
        {
            this.uiController = uiController;
            this.pluginsMenu = pluginsMenu;

            pluginsRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins");
            descriptors = PluginCatalog.Load(pluginsRoot);

            BuildMenu();
        }

        public void OnModelLoaded()
        {
            ApplyCurrentModelTreeColor();
        }

        private void BuildMenu()
        {
            if (pluginsMenu == null) return;

            pluginsMenu.DropDownItems.Clear();
            foreach (var descriptor in descriptors.OrderBy(d => d.MenuPath, StringComparer.InvariantCultureIgnoreCase))
            {
                var item = AddMenuPath(pluginsMenu.DropDownItems, descriptor.MenuPath);
                if (item == null) continue;
                item.Tag = descriptor;
                item.Click += PluginMenuItem_Click;
            }

            if (pluginsMenu.DropDownItems.Count == 0)
            {
                pluginsMenu.DropDownItems.Add(new ToolStripMenuItem("(No plugins)") { Enabled = false });
            }
        }

        private void PluginMenuItem_Click(object sender, EventArgs e)
        {
            var menuItem = sender as ToolStripMenuItem;
            var descriptor = menuItem?.Tag as PluginDescriptor;
            if (descriptor == null) return;

            if (uiController.Handler == null)
            {
                MessageBox.Show("Load or connect to a model before opening plugins.", "No model loaded", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                if (descriptor.SingleInstance &&
                    openForms.TryGetValue(descriptor.Id, out var existing) &&
                    existing != null &&
                    !existing.IsDisposed)
                {
                    if (existing.WindowState == FormWindowState.Minimized) existing.WindowState = FormWindowState.Normal;
                    existing.Show();
                    existing.Focus();
                    return;
                }

                if (!loadedPlugins.TryGetValue(descriptor.Id, out var plugin))
                {
                    plugin = loader.Load(descriptor, pluginsRoot);
                    loadedPlugins[descriptor.Id] = plugin;
                }

                var context = new PluginHostContext(uiController, this);
                var form = plugin.CreateWindow(context);
                if (form == null) return;

                if (descriptor.SingleInstance)
                {
                    openForms[descriptor.Id] = form;
                    form.FormClosed += (s, a) => openForms.Remove(descriptor.Id);
                }

                form.StartPosition = FormStartPosition.CenterParent;
                form.Show(uiController.Elements.FormMain);
                form.BringToFront();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Plug-in Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static ToolStripMenuItem AddMenuPath(ToolStripItemCollection items, string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;

            ToolStripItemCollection current = items;
            ToolStripMenuItem leaf = null;
            var parts = path.Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                var existing = current.OfType<ToolStripMenuItem>().FirstOrDefault(i => i.Text == part);
                if (existing == null)
                {
                    existing = new ToolStripMenuItem(part);
                    current.Add(existing);
                }

                leaf = existing;
                current = existing.DropDownItems;
            }

            return leaf;
        }

        internal string GetCurrentModelIdentityKey()
        {
            var handler = uiController.Handler;
            if (handler == null) return null;
            if (handler.SourceType != ModelSourceType.Database) return null;

            var server = handler.ConnectionInfo;
            var database = handler.Database?.Name;
            if (string.IsNullOrWhiteSpace(server) || string.IsNullOrWhiteSpace(database)) return null;
            return $"{server}/{database}";
        }

        internal bool TryGetModelTreeColor(string modelKey, out Color color)
        {
            color = SystemColors.Window;
            if (string.IsNullOrWhiteSpace(modelKey)) return false;

            if (!Preferences.Current.Plugin_ModelTreeBackColorByModel.TryGetValue(modelKey, out var rawColor)) return false;

            try
            {
                color = ColorTranslator.FromHtml(rawColor);
                return true;
            }
            catch
            {
                return false;
            }
        }

        internal void SetModelTreeColor(string modelKey, Color color)
        {
            if (string.IsNullOrWhiteSpace(modelKey)) return;

            Preferences.Current.Plugin_ModelTreeBackColorByModel[modelKey] = ColorTranslator.ToHtml(color);
            Preferences.Current.Save();
        }

        internal void ClearModelTreeColor(string modelKey)
        {
            if (string.IsNullOrWhiteSpace(modelKey)) return;

            Preferences.Current.Plugin_ModelTreeBackColorByModel.Remove(modelKey);
            Preferences.Current.Save();
        }

        internal void ApplyCurrentModelTreeColor()
        {
            var treeView = uiController.Elements.TreeView;
            if (treeView == null) return;

            var key = GetCurrentModelIdentityKey();
            if (TryGetModelTreeColor(key, out var color))
            {
                SetTreeBackground(treeView, color);
            }
            else
            {
                SetTreeBackground(treeView, SystemColors.Window);
            }
        }

        private void SetTreeBackground(TreeViewAdv treeView, Color color)
        {
            if (treeView.InvokeRequired)
            {
                treeView.BeginInvoke(new Action(() => treeView.BackColor = color));
            }
            else
            {
                treeView.BackColor = color;
            }
        }
    }
}
