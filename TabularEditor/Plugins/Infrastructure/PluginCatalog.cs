using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TabularEditor.TOMWrapper;

namespace TabularEditor.Plugins.Infrastructure
{
    public static class PluginCatalog
    {
        public static List<PluginDescriptor> Load(string pluginsRoot)
        {
            var result = new List<PluginDescriptor>();
            if (string.IsNullOrWhiteSpace(pluginsRoot) || !Directory.Exists(pluginsRoot))
                return result;

            foreach (var manifestPath in Directory.EnumerateFiles(pluginsRoot, "*.json", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    var token = JToken.Parse(File.ReadAllText(manifestPath));
                    var items = token.Type == JTokenType.Array ? (JArray)token : token["plugins"] as JArray;
                    if (items == null) continue;

                    foreach (var item in items.OfType<JObject>())
                    {
                        if (!TryParse(item, manifestPath, out var descriptor)) continue;
                        result.Add(descriptor);
                    }
                }
                catch (Exception ex)
                {
                    TabularModelHandler.Log($"Failed loading plugin manifest '{manifestPath}'. {ex.Message}");
                }
            }

            return result;
        }

        private static bool TryParse(JObject item, string manifestPath, out PluginDescriptor descriptor)
        {
            descriptor = null;

            var id = item.Value<string>("id")?.Trim();
            if (string.IsNullOrEmpty(id)) return false;

            var menuPath = item.Value<string>("menuPath")?.Trim();
            if (string.IsNullOrEmpty(menuPath)) menuPath = item.Value<string>("name")?.Trim();
            if (string.IsNullOrEmpty(menuPath)) return false;

            var pluginType = (item.Value<string>("pluginType") ?? "assembly").Trim();
            var sourceType = pluginType.Equals("script", StringComparison.InvariantCultureIgnoreCase)
                ? PluginSourceType.Script
                : PluginSourceType.Assembly;

            descriptor = new PluginDescriptor
            {
                Id = id,
                Name = item.Value<string>("name")?.Trim() ?? id,
                MenuPath = menuPath,
                SourceType = sourceType,
                Source = item.Value<string>("source")?.Trim(),
                EntryType = item.Value<string>("entryType")?.Trim(),
                SingleInstance = item.Value<bool?>("singleInstance") ?? true,
                ManifestPath = manifestPath
            };
            return true;
        }
    }
}
