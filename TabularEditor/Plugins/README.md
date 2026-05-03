# Runtime Plugins (Feature Notes)

## What this folder contains

- `plugins.json`: static plugin manifest consumed at runtime.
- Plugin source folders (assembly-backed and script-backed plugins).

## Runtime loading behavior

- Plugins are added under the top-level `Plugins` menu.
- Runtime plugin loading is lazy (on menu click).
- Single-instance plugin windows are enforced per plugin descriptor.

## Script plugin compatibility notes

- Script plugins are compiled via CodeDom at runtime.
- Prefer conservative C# syntax for compatibility.
- Avoid string interpolation (`$"..."`) in script plugins.
- Avoid unnecessary direct type coupling in script code when host-context helpers can be used.

## Testing guidance

- Source path for script plugin:
  - `TabularEditor/Plugins/...`
- Runtime copy used by Debug executable:
  - `TabularEditor/bin/Debug/Plugins/...`
- If runtime behavior does not match source changes:
  - ensure output copy is refreshed
  - restart Tabular Editor

## Regression test coverage

- `TabularEditorTest/Plugins/PluginLoaderTests.cs` contains compile/load validation for the JSON/TMSL script plugin.
