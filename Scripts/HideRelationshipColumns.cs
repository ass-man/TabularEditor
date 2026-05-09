// Hides every column used by a single-column relationship in the loaded model.
// Intended for Tabular Editor CLI/script workflows after model generation or schema updates.

var hiddenColumns = new HashSet<Column>();

foreach(var relationship in Model.Relationships)
{
    var fromColumn = relationship.FromColumn;
    var toColumn = relationship.ToColumn;

    if(fromColumn != null)
    {
        fromColumn.IsHidden = true;
        hiddenColumns.Add(fromColumn);
    }

    if(toColumn != null)
    {
        toColumn.IsHidden = true;
        hiddenColumns.Add(toColumn);
    }
}

Info("Hidden relationship columns: " + hiddenColumns.Count);
