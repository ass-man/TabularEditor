using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
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

        [TestMethod]
        public void Load_AllScriptPluginsFromManifest_CompileAndInstantiate()
        {
            var repoRoot = FindRepoRoot();
            var pluginsRoot = Path.Combine(repoRoot, "TabularEditor", "Plugins");
            Assert.IsTrue(Directory.Exists(pluginsRoot), $"Plugins root not found: {pluginsRoot}");

            var descriptors = PluginCatalog.Load(pluginsRoot);
            Assert.IsNotNull(descriptors, "Plugin catalog load returned null.");

            var scriptPlugins = descriptors
                .Where(d => d.SourceType == PluginSourceType.Script)
                .ToList();

            Assert.IsTrue(scriptPlugins.Count > 0, "No script plugins found in plugin manifests.");

            var loader = new PluginLoader();
            foreach (var descriptor in scriptPlugins)
            {
                var plugin = loader.Load(descriptor, pluginsRoot);
                Assert.IsNotNull(plugin, $"Script plugin did not instantiate: {descriptor.Id}");
            }
        }

        [TestMethod]
        public void MeasureKillerViewer_JsonParser_ParsesMeasureKillerShape()
        {
            var repoRoot = FindRepoRoot();
            var pluginsRoot = Path.Combine(repoRoot, "TabularEditor", "Plugins");
            var descriptor = new PluginDescriptor
            {
                Id = "measure-killer-viewer",
                Name = "Measure Killer Output Viewer",
                MenuPath = "Measure Killer Output Viewer...",
                SourceType = PluginSourceType.Script,
                Source = @"MeasureKillerViewer\MeasureKillerViewerPlugin.csx",
                EntryType = "MeasureKillerViewerPlugin",
                SingleInstance = true
            };

            var plugin = new PluginLoader().Load(descriptor, pluginsRoot);
            var jsonType = plugin.GetType().Assembly.GetType("Json", true);
            var readObject = jsonType.GetMethod("ReadObject");
            var result = readObject.Invoke(null, new object[]
            {
                @"{""model_name"":""WWI_test"",""tables"":[{""name"":""D_StockItem"",""columns"":[{""name"":""Stock Item"",""is_used"":""Used""}],""measures"":[]}],""relationships"":[]}"
            });

            Assert.IsNotNull(result, "JSON parser returned null.");
            var str = jsonType.GetMethod("Str");
            Assert.AreEqual("WWI_test", str.Invoke(null, new[] { result, "model_name" }));
            var array = jsonType.GetMethod("Array");
            var tables = array.Invoke(null, new[] { result, "tables" }) as System.Collections.ArrayList;
            Assert.IsNotNull(tables, "Tables array did not parse.");
            Assert.AreEqual(1, tables.Count);
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
