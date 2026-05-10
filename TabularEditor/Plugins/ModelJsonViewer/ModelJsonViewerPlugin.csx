using System.Windows.Forms;

public class ModelJsonViewerPlugin : IRuntimeWindowPlugin
{
    public Form CreateWindow(PluginHostContext context)
    {
        return new ModelJsonViewerForm(context);
    }
}
