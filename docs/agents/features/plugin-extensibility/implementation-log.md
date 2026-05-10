# Implementation Log

## Session Log

### 2026-05-03 Session

- Goal:
  Implement plugin extensibility feature from approved `plan.md` with `Plugins` top menu, runtime plugin loading, per-model tree-color persistence, and JSON/TMSL viewer plugin.
- Files inspected:
  `TabularEditor/FormMain.cs`, `TabularEditor/UI/UIController.cs`, `TabularEditor/UIServices/Preferences.cs`, `TabularEditor/TabularEditor.csproj`, `docs/agents/features/plugin-extensibility/plan.md`
- Files changed:
  `TabularEditor/FormMain.cs`, `TabularEditor/UI/UIController.cs`, `TabularEditor/UIServices/Preferences.cs`, `TabularEditor/TabularEditor.csproj`, plugin infrastructure and plugin files listed below.
- Discoveries:
  Existing plugin system (`ITabularEditorPlugin`, `Program.Plugins`) remains startup/eager and Tools-menu based; new runtime plugin system can coexist without breaking legacy path.
- Validation run:
  `MSBuild.exe TabularEditor\TabularEditor.csproj /t:Build /p:Configuration=Debug /m` succeeded with warnings only (0 errors).
- Validation not run:
  Manual WinForms runtime checks (menu placement, single-instance behavior, live JSON refresh, per-model color persistence) not run in this session because UI interaction was not executed through terminal tooling.
- Next step:
  Run manual UI scenarios from `plan.md` Validation section and adjust behavior if any runtime UX issues appear.

## Important Discoveries

- Keeping new plugin runtime loader separate from legacy `Program.Plugins` avoids broad refactor risk and preserves backward compatibility.
- Runtime menu insertion in `FormMain.cs` avoids editing generated designer files while still adding top-level `Plugins`.

## Changed Files

- `TabularEditor/FormMain.cs`: inserted runtime `Plugins` top-level menu and passed menu handle into `UIElements`.
- `TabularEditor/UI/UIController.cs`: wired `PluginRuntimeManager` lifecycle and model-load hook; extended `UIElements` with `PluginsMenu`.
- `TabularEditor/UIServices/Preferences.cs`: added persisted per-model tree color dictionary.
- `TabularEditor/Plugins/Infrastructure/PluginDescriptor.cs`: descriptor model + source type enum.
- `TabularEditor/Plugins/Infrastructure/IRuntimeWindowPlugin.cs`: plugin runtime interface.
- `TabularEditor/Plugins/Infrastructure/PluginHostContext.cs`: host context exposing model/UI/settings helpers.
- `TabularEditor/Plugins/Infrastructure/PluginCatalog.cs`: manifest loader for static menu descriptors.
- `TabularEditor/Plugins/Infrastructure/PluginLoader.cs`: assembly/script plugin loader with script compilation.
- `TabularEditor/Plugins/Infrastructure/PluginRuntimeManager.cs`: static menu population, lazy load, single-instance window management, per-model tree color apply/persist logic.
- `TabularEditor/Plugins/ModelTreeColor/ModelTreeColorPlugin.cs`: compiled plugin window to configure and persist per-model tree panel color.
- `TabularEditor/Plugins/ModelJsonViewer/ModelJsonViewerPlugin.csx`: script plugin window showing live JSON/TMSL with debounced model-change refresh.
- `TabularEditor/Plugins/plugins.json`: static plugin descriptors (compiled + script plugin entries).
- `TabularEditor/Plugins/README.md`: runtime plugin behavior notes, script compatibility constraints, and troubleshooting pointers.
- `TabularEditor/TabularEditor.csproj`: included new plugin infrastructure compile files and copied plugin manifest/script to output.
- `docs/agents/features/plugin-extensibility/troubleshooting.md`: full incident timeline, error signatures, root-cause analysis, and recovery commands.

## Validation History

| Date | Command/Check | Result | Notes |
| --- | --- | --- | --- |
| 2026-05-03 | `MSBuild.exe TabularEditor\TabularEditor.csproj /t:Build /p:Configuration=Debug /m` | PASS | 0 errors, warnings only (existing dependency/code-analysis warnings). |
| 2026-05-03 | Output artifact check: `TabularEditor\bin\Debug\Plugins\...` | PASS | `plugins.json` and `ModelJsonViewerPlugin.csx` copied under output `Plugins` folder. |
| 2026-05-03 | Runtime reflection smoke-check of `PluginLoader.Load(...)` for `ModelJsonViewerPlugin.csx` | PASS | Script plugin compiled and instantiated successfully in current Debug binaries. |
| 2026-05-03 | `msbuild` command availability check in this environment | FAIL | `msbuild` and `dotnet` are not installed on PATH, so source rebuild and test execution could not be run in this terminal session. |
| 2026-05-03 | `vswhere` lookup for MSBuild path | PASS | Located MSBuild at `C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe`. |
| 2026-05-03 | `TabularEditor.csproj` build using located MSBuild | PASS | Build succeeded; observed lock warnings (`MSB3026`) when `TabularEditor.exe` was running. |
| 2026-05-03 | `TabularEditorTest.csproj` build | PASS | Build succeeded with existing dependency warnings (no new compile errors). |
| 2026-05-03 | `vstest.console.exe` targeted run of `PluginLoaderTests` | PASS | `TabularEditor.PluginLoaderTests.Load_ModelJsonViewerScriptPlugin_CompilesAndInstantiates` passed. |
| 2026-05-03 | `vstest.console.exe` targeted run of manifest-driven plugin script tests | PASS | `Load_AllScriptPluginsFromManifest_CompileAndInstantiate` and `Load_ModelJsonViewerScriptPlugin_CompilesAndInstantiates` both passed. |

## Bugfix Addendum (2026-05-03)

- Reported issue:
  Runtime script plugin compile failure for `ModelJsonViewerPlugin.csx` (`TabularEditor.TOMWrapper` namespace/types not resolved).
- Changes made:
  - `TabularEditor/Plugins/Infrastructure/PluginLoader.cs`:
    - Added explicit script compile references for `System*` UI assemblies, executing assembly, `TabularModelHandler` assembly, and explicit fallback paths for `TOMWrapper.dll` and `TabularEditor.exe`.
    - Removed broad "all loaded assemblies" reference loop after it introduced duplicate-identity compile errors (`CS1703`).
    - Removed unconditional script prelude `using TabularEditor.TOMWrapper*` statements to avoid forcing TOMWrapper namespace resolution in every script.
    - Script plugin compilation now compiles all `*.csx` files in the configured plugin folder (same directory as descriptor `source`) in one compilation.
  - `TabularEditor/Plugins/ModelJsonViewer/ModelJsonViewerPlugin.csx`:
    - Added `Copy as Text` command in the toolstrip.
    - Reworked auto-refresh to timer-based polling and removed direct TOMWrapper event-arg type usage from script code.
    - Added `Ctrl+F`-style search bar with next/previous navigation (`Enter`, `Shift+Enter`, `F3`, `Shift+F3`) and hide-on-`Esc`.
    - Replaced C# string interpolation with compatibility-safe concatenation after runtime `CS1056 Unexpected character '$'` failure.
  - `TabularEditor/Plugins/Infrastructure/PluginHostContext.cs`:
    - Added `ScriptCurrentModelCreateOrReplace()` helper so script plugins can obtain JSON/TMSL without directly referencing TOMWrapper utility types.
  - `TabularEditor/Plugins/ModelTreeColor/ModelTreeColorPlugin.cs`:
    - Added `Copy as Text` button with model key + color payload.
  - `TabularEditorTest/Plugins/PluginLoaderTests.cs`:
    - Added automated regression test that loads and compiles the JSON/TMSL script plugin via `PluginLoader`.
    - Added manifest-driven automated regression test that loads and compiles all script plugins declared in plugin manifests.
    - Added multi-file script regression test to verify all `*.csx` files in one plugin folder are compiled together.
  - `TabularEditorTest/TabularEditorTest.csproj`:
    - Included the new plugin-loader test file in test compilation.

## Follow-up Troubleshooting Notes

- Full issue timeline, signatures, and recovery guidance are documented in:
  - `docs/agents/features/plugin-extensibility/troubleshooting.md`
- Important runtime nuance:
  - Plugin script source under `TabularEditor/Plugins/...` and runtime copy under `TabularEditor/bin/Debug/Plugins/...` must both be current for immediate local testing.
  - Reopen Tabular Editor after plugin script updates; stale running processes can keep old behavior active.
