# Measure Killer Output Viewer Implementation Log

## 2026-05-09

- Inspected existing runtime plugin infrastructure:
  - `TabularEditor/Plugins/plugins.json`
  - `TabularEditor/Plugins/Infrastructure/PluginLoader.cs`
  - `TabularEditor/Plugins/ModelJsonViewer/ModelJsonViewerPlugin.csx`
  - `TabularEditorTest/Plugins/PluginLoaderTests.cs`
- Inspected Measure Killer sample output folder and found six workbook outputs.
- Workbook similarities:
  - `results.xlsx`, `table_level_results.xlsx`, and `relationships_level_results.xlsx` are status/usage result tables with repeated `Used_in_*` fields.
  - `dax_expressions.xlsx` and `m_expressions.xlsx` are expression inventory tables with `Name`, `Type`, `Source`, and `Expression`.
  - `reports.xlsx` is report lineage with repeated `Obj*` columns.
- Chosen implementation changed from workbook parsing to a JSON-only read-only viewer after runtime compile issues in the Excel/OpenXML version.
- The plugin now reads only `C:\Users\gamer\AppData\Local\Programs\Measure Killer\WWI_test_20260509_200325.json`.
- Added selected-object filtering: the viewer watches the main model tree selection and filters JSON-derived rows using selected object/table/type terms, with a checkbox to disable it.
- Verification:
  - `MSBuild TabularEditorTest.csproj` passed when built to `bin\CodexDebug` / `obj\CodexDebug`.
  - `Load_AllScriptPluginsFromManifest_CompileAndInstantiate` passed against `TabularEditorTest\bin\CodexDebug\TabularEditorTest.dll`.
  - The normal `bin\Debug` build was blocked because `TabularEditor\bin\Debug\TabularEditor.exe` was locked by a running process.
- Runtime fix:
  - Removed Excel/OpenXML parsing entirely.
  - Copied the fixed JSON-only script into `TabularEditor\bin\Debug\Plugins\MeasureKillerViewer\MeasureKillerViewerPlugin.csx` for the currently running Debug app.
- Runtime fix 2:
  - Removed the runtime dependency on `System.Web.Extensions`; the JSON reader is now embedded in the plugin script.
  - Added `MeasureKillerViewer_JsonParser_ParsesMeasureKillerShape` because the earlier script plugin test only compiled/instantiated the plugin and did not execute JSON loading code.
- UI update:
  - Removed the grid/tab layout.
  - Replaced it with a single tree view.
  - Nodes whose Measure Killer status is unused are rendered red; other nodes are rendered green.
- Layout fix:
  - Moved the toolbar, header, and tree into a three-row `TableLayoutPanel` so the tree can no longer render behind the header controls.
  - Verified with `build.ps1`; exit code was 0 and no error lines were found in the captured raw log.
