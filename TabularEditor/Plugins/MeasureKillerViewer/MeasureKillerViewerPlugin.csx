using System.Windows.Forms;

public class MeasureKillerViewerPlugin : IRuntimeWindowPlugin
{
    public Form CreateWindow(PluginHostContext context)
    {
        return new MeasureKillerViewerForm(context);
    }
}
