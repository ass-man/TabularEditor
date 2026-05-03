using Newtonsoft.Json;

namespace TabularEditor.Plugins.Infrastructure
{
    public enum PluginSourceType
    {
        Assembly,
        Script
    }

    public class PluginDescriptor
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string MenuPath { get; set; }
        public PluginSourceType SourceType { get; set; }
        public string Source { get; set; }
        public string EntryType { get; set; }
        public bool SingleInstance { get; set; } = true;

        [JsonIgnore]
        public string ManifestPath { get; set; }
    }
}
