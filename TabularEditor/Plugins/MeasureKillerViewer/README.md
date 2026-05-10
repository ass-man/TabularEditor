# Measure Killer JSON Viewer

Runtime plug-in for reviewing Measure Killer JSON exports inside Tabular Editor.

The viewer is focused on lineage and safe-removal analysis. Its main question is:

> Can I safely remove this object, and why?

## Input File

The plug-in scans this folder on load and refresh:

```text
C:\Users\gamer\AppData\Local\Programs\Measure Killer
```

It reads `*.json` exports from that folder. If a model is open in Tabular Editor, the plug-in prefers the newest JSON file whose filename starts with the open model name, for example:

```text
WWI_test_20260510_204746.json
```

If no model-specific file is found, it falls back to the newest JSON export in the folder.

Use **Refresh** to re-scan the folder and load a newer export.

## Main UI

The left side is the primary decision list. It is a grid with:

- Status
- Object Type
- Table
- Object
- Direct Usage
- Used By
- Reason

The top filter bar supports:

- Search
- Status
- Object Type
- Table
- Selected object only
- Show full details

`Selected object only` shows the selected model object plus every downstream object that references it through the lineage graph.

## Status Model

The plug-in computes an effective usage status for each object:

- **Keep**: dependency chain reaches report/model consumers that should be preserved.
- **Cascade candidate**: only used by measures or artifacts that do not reach real report consumers.
- **Relationship only**: only consumed by relationships. A relationship by itself is not treated as real report usage.
- **Relationship island**: table-level cleanup candidate where remaining usage is relationship-only or cascade-removable.
- **Duplicate visual**: visual has the same visual type and visual field wells as another visual.
- **Unused**: no effective consumers found.
- **Review**: ambiguous or model-only dependency; inspect before changing.

Row background colors are intentionally light and status-based. The right panel carries the detailed explanation.

## Right Panel

The right side contains:

- **Summary**: structured recommendation, reason, direct usage, and lineage summary.
- **Used By Chain**: recursive consumers for the selected object, using short prefixes such as `[KEEP]`, `[CASCADE]`, `[REL]`, `[ISLAND]`, `[UNUSED]`, and `[REVIEW]`.
- **Raw Details**: raw JSON fields. This is gated by **Show full details**.

For visual nodes, the Summary tab also shows **Visual inputs**, grouped by roles reported by Measure Killer such as Rows, Columns, Values, X-Axis, Y-Axis, Category, and similar visual wells.

When visual coordinate metadata exists, a visual preview appears below the right-side details and draws the visual box positions.

## Implementation Notes

The plug-in is pure WinForms script code and does not depend on external JSON, Excel, web, or chart libraries.

Key implementation types:

- `EffectiveUsage`
- `LineageObject`
- `LineageEdge`
- `LineageEdgeKind`

The embedded JSON parser is intentionally local to the script so the plug-in can compile in Tabular Editor's runtime plug-in host without extra assembly dependencies.

## Validation

Targeted validation should include:

```powershell
vstest.console.exe TabularEditorTest\bin\CodexDebug\TabularEditorTest.dll /Tests:TabularEditor.PluginLoaderTests.Load_AllScriptPluginsFromManifest_CompileAndInstantiate,TabularEditor.PluginLoaderTests.MeasureKillerViewer_JsonParser_ParsesMeasureKillerShape
```

Full repository tests may require `TE_TestServer` for Analysis Services integration tests.
