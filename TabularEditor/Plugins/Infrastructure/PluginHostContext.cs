using Aga.Controls.Tree;
using System;
using System.Drawing;
using TabularEditor.TOMWrapper;
using TabularEditor.TOMWrapper.Utils;
using TabularEditor.UI;
using TabularEditor.UIServices;

namespace TabularEditor.Plugins.Infrastructure
{
    public class PluginHostContext
    {
        private readonly PluginRuntimeManager manager;

        internal PluginHostContext(UIController uiController, PluginRuntimeManager manager)
        {
            UIController = uiController;
            this.manager = manager;
        }

        public UIController UIController { get; }
        public TabularModelHandler Handler => UIController.Handler;
        public Preferences Preferences => Preferences.Current;
        public FormMain FormMain => UIController.Elements.FormMain;
        public TreeViewAdv TreeView => UIController.Elements.TreeView;

        public string GetCurrentModelIdentityKey()
        {
            return manager.GetCurrentModelIdentityKey();
        }

        public bool TryGetCurrentModelTreeColor(out Color color)
        {
            var key = GetCurrentModelIdentityKey();
            return manager.TryGetModelTreeColor(key, out color);
        }

        public void SetCurrentModelTreeColor(Color color)
        {
            var key = GetCurrentModelIdentityKey();
            manager.SetModelTreeColor(key, color);
        }

        public void ClearCurrentModelTreeColor()
        {
            var key = GetCurrentModelIdentityKey();
            manager.ClearModelTreeColor(key);
        }

        public void ApplyCurrentModelTreeColor()
        {
            manager.ApplyCurrentModelTreeColor();
        }

        public void SavePreferences()
        {
            Preferences.Current.Save();
        }

        public string ScriptCurrentModelCreateOrReplace()
        {
            if (Handler == null) return "(No model loaded)";
            return Scripter.ScriptCreateOrReplace();
        }

        public void RunModelUpdate(string undoName, Action action)
        {
            if (Handler == null) return;
            if (action == null) return;

            Handler.BeginUpdate(undoName);
            try
            {
                action();
                Handler.EndUpdate();
            }
            catch
            {
                Handler.EndUpdate(true, true);
                throw;
            }
        }
    }
}
