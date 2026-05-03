using System.Windows.Forms;

namespace TabularEditor.Plugins.Infrastructure
{
    public interface IRuntimeWindowPlugin
    {
        Form CreateWindow(PluginHostContext context);
    }
}
