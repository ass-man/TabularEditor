using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using TabularEditor.Plugins.Infrastructure;

namespace TabularEditor
{
    [TestClass]
    public class PluginLoaderTests
    {
        [TestMethod]
        public void Load_ModelJsonViewerScriptPlugin_CompilesAndInstantiates()
        {
            var repoRoot = FindRepoRoot();
            var pluginsRoot = Path.Combine(repoRoot, "TabularEditor", "Plugins");
            var scriptPath = Path.Combine(pluginsRoot, "ModelJsonViewer", "ModelJsonViewerPlugin.csx");

            Assert.IsTrue(Directory.Exists(pluginsRoot), $"Plugins root not found: {pluginsRoot}");
            Assert.IsTrue(File.Exists(scriptPath), $"Script plugin file not found: {scriptPath}");

            var descriptor = new PluginDescriptor
            {
                Id = "model-json-viewer",
                Name = "Model JSON/TMSL Viewer",
                MenuPath = "Model JSON/TMSL Viewer...",
                SourceType = PluginSourceType.Script,
                Source = @"ModelJsonViewer\ModelJsonViewerPlugin.csx",
                EntryType = "ModelJsonViewerPlugin",
                SingleInstance = true
            };

            var loader = new PluginLoader();
            var plugin = loader.Load(descriptor, pluginsRoot);

            Assert.IsNotNull(plugin);
            Assert.AreEqual("ModelJsonViewerPlugin", plugin.GetType().Name);
        }

        private static string FindRepoRoot()
        {
            var current = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (current != null)
            {
                if (File.Exists(Path.Combine(current.FullName, "TabularEditor.sln")))
                    return current.FullName;

                current = current.Parent;
            }

            Assert.Fail($"Could not locate repository root from '{AppDomain.CurrentDomain.BaseDirectory}'.");
            return null;
        }
    }
}
