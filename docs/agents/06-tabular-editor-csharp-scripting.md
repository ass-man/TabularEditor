# Tabular Editor C# Scripting Notes

LLM-facing summary of the official Tabular Editor C# scripting docs. Use this as a local quick reference before creating or changing scripts under `Scripts/` or running scripts through `TabularEditor.exe -S`.

Sources:

- https://docs.tabulareditor.com/en/features/csharp-scripts.html
- https://docs.tabulareditor.com/en/features/CSharpScripts/csharp-script-library-beginner.html
- https://docs.tabulareditor.com/en/features/CSharpScripts/csharp-script-library-advanced.html

## When To Use Scripts

- Use C# scripts for repeatable bulk metadata edits that are awkward or error-prone in the UI.
- Use reusable scripts with the Tabular Editor CLI for build/deploy workflows.
- Save reusable scripts under `Scripts/`.
- Saved reusable scripts must not contain hardcoded model object names such as specific table, column, measure, relationship, or database names.
- Do not save one-time local repair scripts. Keep those as conversation notes or implementation-log details only when the exact action matters.

```powershell
$p = Start-Process -FilePath .\TabularEditor\bin\Debug\TabularEditor.exe `
  -Wait -NoNewWindow -PassThru `
  -ArgumentList "localhost WWI_Tabular -S Scripts\SomeReusableScript.cs -D -E -W"
exit $p.ExitCode
```

## Script Execution Model

- Scripts operate on the currently loaded model.
- `Model` exposes the loaded model and its tables, columns, measures, relationships, roles, perspectives, cultures, data sources, and other metadata.
- `Selected` exposes the current UI selection with typed singular and plural accessors such as `Selected.Measure`, `Selected.Measures`, `Selected.Columns`, `Selected.Tables`, and `Selected.SingleColumnRelationships`.
- Singular `Selected.*` accessors require exactly one selected object of that type.
- Plural `Selected.*` accessors return an empty collection if nothing of that type is selected.
- Prefer Tabular Editor wrapper objects over raw TOM objects because UI execution can preview and undo metadata edits.

## Common Coding Patterns

Use direct object paths for known objects:

```csharp
Model.Tables["FactInternetSales"].Measures["Sales Amount"].FormatString = "0.0%";
```

Use LINQ and collection helpers for bulk changes:

```csharp
Model.AllMeasures
    .Where(m => m.Name.Contains("Amount"))
    .ForEach(m => m.FormatString = "$#,0.00");
```

Use `Selected` for scripts meant to run from the UI against the current selection:

```csharp
Selected.Measures.DisplayFolder = "Review";
```

Use `Info`, `Warning`, and `Error` for CLI-visible script messages. Use `Environment.GetEnvironmentVariable(...)` for parameters in CLI or CI/CD runs; Tabular Editor scripts do not receive normal command-line arguments.

## Default References And Imports

Tabular Editor scripts can use common .NET and Tabular Editor APIs without manually adding most references. The official docs list these default namespaces:

- `System`
- `System.Linq`
- `System.Collections.Generic`
- `Newtonsoft.Json`
- `TabularEditor.TOMWrapper`
- `TabularEditor.TOMWrapper.Utils`
- `TabularEditor.UI`

Useful default assemblies include `System`, `System.Core`, `System.Data`, `System.Windows.Forms`, `Microsoft.CSharp`, `Newtonsoft.Json`, `TOMWrapper`, `TabularEditor`, and `Microsoft.AnalysisServices.Tabular`.

For extra assemblies, put `#r "<assembly name or DLL path>"` at the top of the script, before `using` statements and executable code.

## Helper Methods To Remember

- `Output(object)`: inspect an object interactively or write output in command-line execution.
- `Info(string)`, `Warning(string)`, `Error(string)`: write CLI-visible messages at the corresponding level.
- `ReadFile(string)`, `SaveFile(string, string)`: read/write text.
- `ExportProperties(...)`, `ImportProperties(...)`: export/import object properties as TSV.
- `CustomAction(...)`: invoke a named macro.
- `FormatDax(...)`, `CallDaxFormatter(...)`: queue and apply DAX formatting.
- `ConvertDax(...)`: convert DAX separators between locale conventions.
- `CollectVertiPaqAnalyzerStats()` and `GetCardinality(...)`: inspect VertiPaq stats when connected to Analysis Services.

## UI Safety

- In the desktop UI, use Run with Preview for unfamiliar or broad scripts so metadata changes can be reviewed before accepting.
- Script metadata changes are undoable as one transaction when executed in the UI.
- External side effects, such as file/database writes or web requests, are not undone by preview or rollback.

## Compatibility Notes

- Tabular Editor 2 and 3 scripting APIs are mostly compatible, but not identical.
- If a reusable script must support both, keep syntax conservative and avoid relying on newer TE3-only helpers unless guarded.
- TE3 supports preprocessor directives such as `#if TE3` for version-specific script logic.
- For scripts that run in this repo's TE2 CLI, prefer simple C# syntax and verify by running the CLI script, not just by reading it.

## Official Script Library Themes

Beginner examples are intended as easier starting points. Common themes include row counts, SUM measure creation, measure tables, table groups, M parameters, hidden partition editing, numeric measure formatting, data-source dependency inspection, field parameters, and unique value display.

Advanced examples assume stronger C#/TOM familiarity. Common themes include object counting, object detail grids, date table creation, M parameter replacement across partitions, Power Query formatting, incremental refresh configuration, measure error cleanup, DAX find/replace, Databricks-oriented setup and relationship creation, Direct Lake / Import conversion, and user-defined aggregations.

## Local Repo Practice

- Put reusable Tabular Editor scripts in `Scripts/`.
- Name scripts by action, for example `HideRelationshipColumns.cs`.
- Keep saved scripts model-agnostic. Prefer iterating metadata (`Model.Tables`, `Model.Relationships`, `Selected.Columns`, etc.) and filtering by object properties or annotations instead of hardcoding object names.
- If a script truly must target specific object names, treat it as a workflow-specific script: place it near that workflow, document the scope clearly at the top of the file, and do not present it as a general reusable script.
- Scripts intended for CLI automation should emit a short `Info(...)` summary and avoid modal UI prompts.
- Validate reusable scripts with the local CLI before considering them done.
- If the target model was loaded from a server and should be saved back to the same source, use `-D` without server/database parameters.
- For deploying a model artifact to a target database, use `-D <server> <database>` plus deployment flags such as `-F`, `-E`, and `-W`.
