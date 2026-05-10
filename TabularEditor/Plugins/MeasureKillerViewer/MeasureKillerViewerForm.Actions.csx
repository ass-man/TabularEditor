using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

internal partial class MeasureKillerViewerForm
{
    private void CopySelectedObjectName()
    {
        var obj = SelectedLineageObject();
        if (obj != null) Clipboard.SetText(obj.Name);
    }

    private void CopySelectedLineage()
    {
        var obj = SelectedLineageObject();
        if (obj != null) Clipboard.SetText(BuildSummaryText(obj));
    }

    private void CopySelectedRecommendation()
    {
        var obj = SelectedLineageObject();
        if (obj != null) Clipboard.SetText(obj.ObjectType + " / " + obj.TableName + " / " + obj.Name + "\r\n" + UsageLabel(obj.EffectiveUsage) + "\r\n" + obj.Recommendation + "\r\n" + obj.Reason);
    }

    private void FilterToSelectedTable()
    {
        var obj = SelectedLineageObject();
        if (obj == null || string.IsNullOrWhiteSpace(obj.TableName)) return;
        cboTable.SelectedItem = obj.TableName;
    }

    private void FilterToSelectedStatus()
    {
        var obj = SelectedLineageObject();
        if (obj == null) return;
        cboStatus.SelectedItem = UsageLabel(obj.EffectiveUsage);
    }
}
