# Plugin Extensibility Troubleshooting

## Scope

This document captures runtime issues found while implementing and validating the new `Plugins` menu and initial plugins (`Model Tree Color`, `Model JSON/TMSL Viewer`).

## Incident Timeline (2026-05-03)

1. Initial plugin feature implementation completed and built.
2. Runtime error reported when opening `Model JSON/TMSL Viewer` script plugin:
   - `CS0234`: `TabularEditor.TOMWrapper` namespace not found.
   - `CS0246`: `ITabularNamedObject`, `ObjectDeletingEventArgs`, `ObjectDeletedEventArgs`, `ObjectChangedEventArgs` not found.
3. Added more explicit script compile references in `PluginLoader`.
4. Removed broad "reference all loaded assemblies" approach after duplicate-reference issues (`CS1703`).
5. Removed forced `using TabularEditor.TOMWrapper*` from script prelude.
6. Reworked script plugin code to avoid direct TOMWrapper event-arg type dependencies.
7. Added host helper `ScriptCurrentModelCreateOrReplace()` for script plugins.
8. Added `Copy as Text` to both plugin windows.
9. Added `Ctrl+F` search bar to JSON/TMSL viewer.
10. Runtime error reported again: `CS1056 Unexpected character '$'` in script plugin.
11. Fixed by removing string interpolation from script (`$"..."`) and using string concatenation.
12. Synced updated script to runtime plugin output path under `bin\Debug\Plugins\...`.

## Error Signatures and Root Causes

### 1) `CS0234` / `CS0246` on TOMWrapper symbols in script plugin

- Symptom:
  Script plugin compile popup when opening viewer plugin.
- Root causes:
  - Script compiler context did not reliably resolve TOMWrapper types.
  - Loader injected TOMWrapper `using` lines globally, making all scripts sensitive to those references.
  - Script plugin itself used TOMWrapper-specific event argument types in method signatures.
- Fixes:
  - `TabularEditor/Plugins/Infrastructure/PluginLoader.cs`
    - explicit assembly references for script compile
    - removed forced TOMWrapper `using` prelude
  - `TabularEditor/Plugins/ModelJsonViewer/ModelJsonViewerPlugin.csx`
    - removed direct TOMWrapper event-type usage
    - switched to timer-driven refresh polling
  - `TabularEditor/Plugins/Infrastructure/PluginHostContext.cs`
    - added `ScriptCurrentModelCreateOrReplace()`

### 2) `CS1056 Unexpected character '$'` in script plugin

- Symptom:
  Script compiler error popup pointing to a line with `$`.
- Root cause:
  Plugin script compiler behaves like an older C# language version; interpolation syntax is unsupported.
- Fix:
  Replaced interpolated string with concatenation in `ModelJsonViewerPlugin.csx`.

### 3) Source updated but runtime still shows old behavior/errors

- Symptom:
  New source changes do not appear at runtime.
- Root causes:
  - Running executable loaded previous script copy under output folder.
  - App process may lock `bin\Debug\TabularEditor.exe` during rebuild attempts.
- Fixes:
  - Ensure script is present at `TabularEditor/bin/Debug/Plugins/ModelJsonViewer/ModelJsonViewerPlugin.csx`.
  - Fully close and reopen Tabular Editor after rebuild/sync.
  - If build warns about copy lock (`MSB3026`), close running Tabular Editor and rebuild.

## Verification Commands (Used)

- Build app project:
  - `C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe TabularEditor\TabularEditor.csproj /t:Build /p:Configuration=Debug /m`
- Build test project:
  - `C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe TabularEditorTest\TabularEditorTest.csproj /t:Build /p:Configuration=Debug /m`
- Run targeted regression test:
  - `C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe TabularEditorTest\bin\Debug\TabularEditorTest.dll /Tests:TabularEditor.PluginLoaderTests.Load_ModelJsonViewerScriptPlugin_CompilesAndInstantiates`

## Automated Test Coverage Added

- Added:
  `TabularEditorTest/Plugins/PluginLoaderTests.cs`
- Test:
  `Load_ModelJsonViewerScriptPlugin_CompilesAndInstantiates`
- Purpose:
  Catch script plugin compile/load regressions (including missing references) before manual runtime testing.

## Script Plugin Authoring Constraints (Current)

- Avoid C# features unsupported by CodeDom script compile context (notably string interpolation).
- Prefer compatibility-safe syntax for runtime script plugins.
- If model state access is needed, prefer `PluginHostContext` helper methods over direct low-level TOMWrapper coupling from script code.
