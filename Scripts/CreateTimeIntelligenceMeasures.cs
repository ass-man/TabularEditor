// Creates reusable time-intelligence measures for selected or parameter-listed base measures.
//
// Usage:
// - UI: select one or more base measures. Optionally also select one date column.
// - CLI: set TE_BASE_MEASURE_NAMES to semicolon-separated measure names.
// - CLI optional: set TE_DATE_COLUMN_REF to a DAX column reference, for example '<DateTable>'[<DateColumn>].
//
// If TE_DATE_COLUMN_REF is not supplied, the script tries to infer a single DateTime
// one-side column used by active relationships. The script is intentionally model-agnostic.
// Do not hardcode object names here.

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
Func<string, string> quoteMeasure = name => "[" + name.Replace("]", "]]") + "]";

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

Func<Column> resolveDateColumn = () =>
{
    var dateColumnRef = Environment.GetEnvironmentVariable("TE_DATE_COLUMN_REF");
    if(!string.IsNullOrWhiteSpace(dateColumnRef))
    {
        return findColumnByReference(dateColumnRef);
    }

    var selectedDateColumns = Selected.Columns
        .Where(c => c.DataType == DataType.DateTime)
        .Distinct()
        .ToList();

    if(selectedDateColumns.Count == 1)
    {
        return selectedDateColumns[0];
    }

    var relationshipDateColumns = Model.Relationships
        .Where(r => r.IsActive && r.ToColumn != null && r.ToColumn.DataType == DataType.DateTime)
        .Select(r => r.ToColumn)
        .Distinct()
        .ToList();

    return relationshipDateColumns.Count == 1 ? relationshipDateColumns[0] : null;
};

Func<List<Measure>> resolveBaseMeasures = () =>
{
    var measureNames = Environment.GetEnvironmentVariable("TE_BASE_MEASURE_NAMES");
    if(string.IsNullOrWhiteSpace(measureNames))
    {
        return Selected.Measures.Distinct().ToList();
    }

    var result = new List<Measure>();
    foreach(var measureName in measureNames.Split(new [] { ';' }, StringSplitOptions.RemoveEmptyEntries))
    {
        var name = measureName.Trim();
        var matches = Model.AllMeasures.Where(m => m.Name == name).ToList();
        if(matches.Count == 0)
        {
            Error("Measure was not found: " + name);
            return null;
        }

        if(matches.Count > 1)
        {
            Error("Measure name is ambiguous: " + name);
            return null;
        }

        result.Add(matches[0]);
    }

    return result.Distinct().ToList();
};

Action<Table, string, string, string, string> addMeasureIfMissing = (table, name, expression, formatString, displayFolder) =>
{
    if(table.Measures.Any(m => m.Name == name))
    {
        Warning("Skipped existing measure: " + table.Name + "." + name);
        return;
    }

    var measure = table.AddMeasure(name, expression, displayFolder);
    if(!string.IsNullOrWhiteSpace(formatString))
    {
        measure.FormatString = formatString;
    }
};

var dateColumn = resolveDateColumn();
if(dateColumn == null)
{
    Error("Could not resolve a unique date column. Select one DateTime column or set TE_DATE_COLUMN_REF.");
    return;
}

var baseMeasures = resolveBaseMeasures();
if(baseMeasures == null) return;

if(baseMeasures.Count == 0)
{
    Error("No base measures were selected or supplied. Select measures or set TE_BASE_MEASURE_NAMES.");
    return;
}

var dateRef = quoteTable(dateColumn.Table.Name) + quoteColumn(dateColumn.Name);
var createdFamilies = 0;

foreach(var baseMeasure in baseMeasures)
{
    var table = baseMeasure.Table;
    var baseRef = quoteMeasure(baseMeasure.Name);
    var folder = string.IsNullOrWhiteSpace(baseMeasure.DisplayFolder)
        ? "Time Intelligence"
        : baseMeasure.DisplayFolder + "\\Time Intelligence";

    var ytdName = baseMeasure.Name + " YTD";
    var qtdName = baseMeasure.Name + " QTD";
    var mtdName = baseMeasure.Name + " MTD";
    var pyName = baseMeasure.Name + " PY";
    var yoyName = baseMeasure.Name + " YoY";
    var yoyPctName = baseMeasure.Name + " YoY %";

    addMeasureIfMissing(table, ytdName, "TOTALYTD(" + baseRef + ", " + dateRef + ")", baseMeasure.FormatString, folder);
    addMeasureIfMissing(table, qtdName, "TOTALQTD(" + baseRef + ", " + dateRef + ")", baseMeasure.FormatString, folder);
    addMeasureIfMissing(table, mtdName, "TOTALMTD(" + baseRef + ", " + dateRef + ")", baseMeasure.FormatString, folder);
    addMeasureIfMissing(table, pyName, "CALCULATE(" + baseRef + ", DATEADD(" + dateRef + ", -1, YEAR))", baseMeasure.FormatString, folder);
    addMeasureIfMissing(table, yoyName, baseRef + " - " + quoteMeasure(pyName), baseMeasure.FormatString, folder);
    addMeasureIfMissing(table, yoyPctName, "DIVIDE(" + quoteMeasure(yoyName) + ", " + quoteMeasure(pyName) + ")", "0.00%;-0.00%;0.00%", folder);

    createdFamilies++;
}

Info("Processed time-intelligence measure families: " + createdFamilies + "; date column: " + dateColumn.Table.Name + "." + dateColumn.Name);
