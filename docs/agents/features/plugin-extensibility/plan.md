# Feature Plan

This plan follows `PLANS.md`. Keep it self-contained enough that a fresh Codex session can continue from `AGENTS.md` plus this file.

## Purpose

Add first-class plugin extensibility to Tabular Editor with a dedicated top-level `Plugins` menu and runtime, on-demand plugin loading.  
Plugins must be able to:

- Open WinForms windows.
- Interact with main UI.
- Read/write app settings immediately when possible.
- Read/write model state with normal Undo/Redo behavior.

V1 scope is static menu entries and single-instance plugin windows, with both compiled and script-based plugins supported.  
Initial validation plugins are:

- Model-tree background color settings window (persisted per `server/database` identity).
- Raw model JSON/TMSL viewer window (auto-refreshes when model changes).

## Current Understanding

Verified from targeted inspection:

- Existing plugin contract exists: `ITabularEditorPlugin` in `TOMWrapper/IPlugin.cs` with `Init(TabularModelHandler)` and `RegisterActions(Action<string, Action>)`.
- Existing plugin loading exists in `TabularEditor/Program.cs`:
  - `LoadPlugins()` scans executable directory `*.dll`.
  - Finds types implementing `ITabularEditorPlugin`.
  - Instantiates plugins eagerly at startup.
- Existing plugin action registration exists in `TabularEditor/UI/UIController.cs`:
  - `RegisterPluginCallback` currently adds plugin actions under `Tools` menu.
  - `InitPlugins()` calls `plugin.Init(Handler)` when model is loaded.
- Main top menu currently has `File`, `Edit`, `View`, `Model`, hidden `Dynamic`, and `Tools` (from `FormMain.Designer.cs` wiring; runtime accessed in `FormMain.cs` / `UIElements`).
- `UIController` already exposes UI handles through `UIController.Current.Elements` (tree, property grid, menus, form), which can be used for plugin UI interaction.
- Model mutation and undo batching primitives exist via `TabularModelHandler.BeginUpdate(...)` / `EndUpdate(...)` and `UndoManager`.
- `TabularModelHandler` exposes change events (`ObjectChanged`, `ObjectDeleted`, etc.) and `Handler.Tree.StructureChanged`, useful for live refresh behavior.
- TMSL/JSON scripting helper exists: `TOMWrapper/Utils/Scripter.cs` (`Scripter.ScriptCreateOrReplace()`).
- Preferences persistence exists in `TabularEditor/UIServices/Preferences.cs` (`Preferences.Current`, JSON persisted under LocalAppData).

Constraints already decided:

- Plugins live in-repo under `TabularEditor/Plugins/`.
- Runtime load should occur when plugin window/command is opened.
- Trust model is allow-all for v1.
- Static `Plugins` menu entries only in v1, but keep future context-sensitive extension feasible.
- Single-instance per plugin command/window.
- Validation is manual scenarios only.

Uncertain (implementation detail, not product requirement):

- Best way to compile/load script plugins with existing compile helpers (likely requires shared compilation utility extraction from `ScriptEngine`, to avoid duplicate compiler setup).
- Exact output/copy strategy so in-repo plugin assets are available at runtime in `bin` without manual file copying.

## Open Questions

No blocking product questions remain from intake.

Implementation-time technical choices to finalize:

- Keep legacy eager DLL plugin scan as compatibility path vs. fully migrate all plugin discovery to new in-repo catalog.
- Script plugin compilation reuse strategy (`ScriptEngine` extraction vs. dedicated compiler service).

## Proposed Design

### 1. Add Dedicated `Plugins` Top Menu Without Designer Regeneration

- Create `pluginsToolStripMenuItem` at runtime in `FormMain.cs` (after `InitializeComponent`) and insert it in top menu after `Model` (before hidden `Dynamic`).
- Extend `UIElements` in `TabularEditor/UI/UIController.cs` with `ToolStripDropDownItem PluginsMenu`.
- Pass new menu reference from `FormMain.SetupUIController()`.
- Keep existing `Tools` menu behavior intact.

Rationale: avoids editing generated designer files while satisfying menu placement requirement.

### 2. Introduce Local Plugin Catalog + Lazy Activation

Add plugin infrastructure under `TabularEditor/Plugins/Infrastructure/` (new files):

- `PluginDescriptor`:
  - Plugin id, display name, menu path, plugin type (`assembly` / `script`), source path, entry type name, single-instance flag.
  - Optional future fields for context filters (ignored in v1).
- `PluginCatalog`:
  - Scans `Plugins` runtime folder for manifest files.
  - Returns static command descriptors for menu population.
- `PluginManager`:
  - Builds static menu items under `Plugins`.
  - Handles click -> lazy load plugin implementation.
  - Tracks open plugin windows by command id and enforces single-instance.
  - On existing window, activates/focuses instead of opening another.

Manifest-first discovery keeps menu static and allows lazy load before plugin execution.

### 3. Add Plugin Host Context for Controlled Access

Add host context class (new file, likely `PluginHostContext.cs`) exposing:

- `TabularModelHandler Handler` (active model).
- `UIController UI`.
- `Preferences Preferences`.
- Helpers for safe model updates:
  - `RunModelUpdate(string undoName, Action action)` wraps `BeginUpdate/EndUpdate` and rollback on failure.
- Helpers for preference persistence:
  - `SavePreferences()`.

This gives plugins required access to model/UI/settings while nudging consistent undo-safe mutations.

### 4. Support Both Compiled and Script Plugin Types

Compiled plugin path:

- Load assembly on first command execution (`Assembly.LoadFrom` against descriptor path).
- Create plugin instance from entry type.

Script plugin path:

- Compile plugin source at first execution into in-memory assembly.
- Instantiate entry type from compiled result.

To unify both paths, introduce a runtime plugin interface (new file, likely in `TOMWrapper` or shared UI layer), e.g.:

- `ITabularEditorWindowPlugin` with methods/properties:
  - `string CommandId`
  - `Form CreateOrGetWindow(PluginHostContext context)`
  - Optional lifecycle methods for model-context updates.

Compatibility strategy:

- Keep existing `ITabularEditorPlugin` flow operational for legacy external DLLs.
- New in-repo plugin catalog uses new lazy contract and `Plugins` menu.

### 5. Plugin Menu Wiring and Future Extensibility

- V1 menu entries are static from descriptors.
- Descriptor model keeps optional context metadata for future context-sensitive commands (not used now).
- Plugin manager owns menu creation so future dynamic filtering can be added without redesigning host contracts.

### 6. Persist Per-Model Tree Background Color

Extend `Preferences` with plugin-specific persisted map:

- Example field: `Dictionary<string, string> Plugin_ModelTreeBackColorByModel`.
- Key format: `server/database` (e.g. `localhost/TabularEditorAgentFinance`).
- Value format: HTML color string (`#RRGGBB` / ARGB format chosen at implementation time).

Apply path:

- On model load (`UIController.LoadTabularModelToUI` or plugin manager hook), apply stored color to `UI.Elements.TreeView.BackColor`.
- Color plugin window edits this map and saves immediately (`Preferences.Current.Save()`).

### 7. Initial Validation Plugin: Tree Color Settings Window

New plugin under `TabularEditor/Plugins/ModelTreeColor/`:

- Presents color selector UI.
- Reads current model identity (`server/database`) from active handler.
- Saves selected color for that identity immediately.
- Applies color to left tree panel immediately.
- Enforces single-instance behavior via host manager.

### 8. Initial Validation Plugin: Raw JSON/TMSL Viewer Window

New plugin under `TabularEditor/Plugins/ModelJsonViewer/`:

- Read-only text window showing `Scripter.ScriptCreateOrReplace()` output.
- Subscribes to model-change signals (`ObjectChanged`, `ObjectDeleted`, `Tree.StructureChanged`, and undo/redo state change as needed).
- Uses small debounce timer before regeneration to avoid expensive re-serialization spam during bulk edits.
- Unsubscribes events on window close/dispose.

### 9. Build/Project Integration

Likely project-level updates:

- `TabularEditor/TabularEditor.csproj`:
  - Include plugin manifests/scripts as content copied to output.
  - Include new infrastructure source files.
- If compiled sample plugins are separate class library projects:
  - Add projects under `TabularEditor/Plugins/.../*.csproj`.
  - Add solution entries to `TabularEditor.sln`.
  - Ensure DLLs are copied to runtime plugin folder under output.

## Implementation Steps

1. Add `Plugins` top-menu host wiring:
   `FormMain.cs` runtime menu insertion, `UIElements` extension, and `UIController` menu plumbing.
2. Add plugin infrastructure classes:
   descriptor, catalog, manager, host context, and single-instance window tracking.
3. Add lazy execution pipelines:
   compiled plugin loader and script plugin compiler/loader abstraction.
4. Wire static menu population from catalog at startup and click handling through plugin manager.
5. Add preference model key/color persistence support and apply-on-model-load behavior.
6. Implement first validation plugin (`ModelTreeColor`) and its settings UI flow.
7. Implement second validation plugin (`ModelJsonViewer`) with live refresh + debounce.
8. Validate backward compatibility with existing app behavior when no plugins are present.
9. Run manual validation scenarios and document outcomes.

## Validation

Manual validation only (per request):

1. Menu presence/order:
   confirm top menu shows `File`, `Edit`, `View`, `Model`, `Plugins` in main window.
2. Static entries:
   confirm `Plugins` menu shows expected fixed commands for available plugins.
3. Lazy load:
   start app and verify plugin assemblies/scripts are not loaded/executed until command click (via debug logging or instrumentation).
4. Single instance:
   click same plugin command twice; second click must focus existing window, not open another.
5. Tree color plugin:
   set color for model `localhost/TabularEditorAgentFinance`; verify immediate UI change; restart/reopen model and confirm color auto-applies from saved preferences.
6. Model identity behavior:
   open a different `server/database` model and verify color is independent.
7. JSON/TMSL viewer plugin:
   open viewer, mutate model (e.g., rename object/create measure), verify viewer refreshes to updated `CreateOrReplace` JSON.
8. Undo/Redo compatibility:
   perform plugin-driven model change wrapped in update batch; verify undo/redo entries and model state rollback/restore correctly.
9. No-plugin safety:
   run app with empty plugin catalog and verify no regressions/crashes.

Build/test command execution is not planned in this architecture phase; implementation phase should run minimal relevant validation commands where available and record limitations.

## Progress

- 2026-05-03: Architecture planning started from completed intake (`request.md`, `questions.md`).
- 2026-05-03: Relevant existing extension points identified (`Program.LoadPlugins`, `UIController.RegisterPluginCallback`, `ITabularEditorPlugin`, preferences, tree/model events).
- 2026-05-03: Plan drafted; implementation not started.

## Surprises and Discoveries

- Plugin support already exists in codebase but is startup/eager and wired through `Tools` menu, not a dedicated `Plugins` menu.
- Existing architecture already exposes enough host objects (`UIController.Current`, `UIElements`, `TabularModelHandler` events, `Scripter`) to support requested plugin interactions with minimal core surface expansion.
- Runtime menu insertion in `FormMain.cs` can satisfy new top menu requirement without touching generated designer files.

## Decision Log

- 2026-05-03: Use dedicated `Plugins` top menu (not `Tools`) for plugin commands. Reason: explicit user requirement.
- 2026-05-03: V1 menu entries remain static, while descriptor model keeps room for future context-sensitive extension. Reason: explicit scope + forward compatibility.
- 2026-05-03: Enforce single-instance per plugin window at host manager level. Reason: explicit user requirement and simpler plugin authoring.
- 2026-05-03: Persist model-tree panel color per `server/database` identity in preferences. Reason: explicit user requirement for remembered per connected model behavior.
- 2026-05-03: Manual validation only in this feature's acceptance path. Reason: explicit user requirement.

## Handoff Notes

Current state:

- Intake is complete and resolved.
- Architecture plan is complete.
- No production code changes have been made in this step.

Files added/updated in feature folder:

- `docs/agents/features/plugin-extensibility/request.md`
- `docs/agents/features/plugin-extensibility/questions.md`
- `docs/agents/features/plugin-extensibility/plan.md`

Exact next step:

- Start implementation phase using this plan, beginning with menu host plumbing and plugin infrastructure skeleton before sample plugin implementations.
