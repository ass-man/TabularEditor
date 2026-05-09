// Creates SUM measures for numeric columns.
//
// Usage:
// - UI: select one or more numeric columns, then run the script.
// - CLI: set TE_COLUMN_REFS to semicolon-separated DAX column references, for example:
//   '<Table>'[<Column>];'<OtherTable>'[<OtherColumn>]
//
// The script is intentionally model-agnostic. Do not hardcode object names here.

Func<string, string> unquoteName = value =>
{
    value = value.Trim();
    if(value.StartsWith("'") && value.EndsWith("'"))
    {
        value = value.Substring(1, value.Length - 2).Replace("''", "'");
    }

    return value.Replace("]]", "]");
};

Func<string, string> quoteTable = name => "'" + name.Replace("'", "''") + "'";
Func<string, string> quoteColumn = name => "[" + name.Replace("]", "]]") + "]";

Func<Column, bool> isNumericColumn = column =>
    column.DataType == DataType.Int64 ||
    column.DataType == DataType.Double ||
    column.DataType == DataType.Decimal;

Func<string, Column> findColumnByReference = reference =>
{
    var trimmed = reference.Trim();
    var separator = trimmed.LastIndexOf("[");
    if(separator < 0 || !trimmed.EndsWith("]")) return null;

    var tableName = unquoteName(trimmed.Substring(0, separator).Trim());
    var columnName = trimmed.Substring(separator + 1, trimmed.Length - separator - 2).Replace("]]", "]");
    var table = Model.Tables.FirstOrDefault(t => t.Name == tableName);
    if(table == null) return null;

    return table.Columns.FirstOrDefault(c => c.Name == columnName);
};

var columns = new List<Column>();
var columnRefs = Environment.GetEnvironmentVariable("TE_COLUMN_REFS");

if(!string.IsNullOrWhiteSpace(columnRefs))
{
    foreach(var reference in columnRefs.Split(new [] { ';' }, StringSplitOptions.RemoveEmptyEntries))
    {
        var column = findColumnByReference(reference);
        if(column == null)
        {
            Error("Column reference was not found: " + reference);
            return;
        }

        columns.Add(column);
    }
}
else
{
    columns.AddRange(Selected.Columns);
}

columns = columns.Distinct().ToList();
var skipped = 0;
var created = 0;

foreach(var column in columns)
{
    if(!isNumericColumn(column))
    {
        skipped++;
        Warning("Skipped non-numeric column: " + column.Table.Name + "." + column.Name);
        continue;
    }

    var table = column.Table;
    var measureName = "Total " + column.Name;

    if(table.Measures.Any(m => m.Name == measureName))
    {
        skipped++;
        Warning("Skipped existing measure: " + table.Name + "." + measureName);
        continue;
    }

    var expression = "SUM(" + quoteTable(table.Name) + quoteColumn(column.Name) + ")";
    var measure = table.AddMeasure(measureName, expression);
    measure.DisplayFolder = "Auto Measures";
    measure.FormatString = column.DataType == DataType.Decimal || column.DataType == DataType.Double ? "#,0.00" : "#,0";
    created++;
}

Info("Created SUM measures: " + created + "; skipped: " + skipped);
