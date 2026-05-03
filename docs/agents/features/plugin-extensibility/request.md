# Feature Request

## User-Visible Goal

Add plugin/addon/extension support so users can launch extra WinForms tools from Tabular Editor, including a new top-level `Plugins` menu, while allowing those tools to interact with main UI, app settings, and active tabular model.

## User Workflow

1. User opens Tabular Editor with one or more plugins available in-repo.
2. User opens top menu and sees `Plugins` next to existing entries (`File`, `Edit`, `View`, `Model`, `Plugins`).
3. User opens a plugin command (example: raw TOML viewer, plugin settings window).
4. Plugin loads on demand when command is opened, then window opens.
5. Plugin window can read/write model state, read/write app settings, and coordinate with main UI.
6. Only one instance of each plugin window can be open at a time.
7. User closes plugin window and continues normal editing workflow.

## Acceptance Criteria

- App shows top-level `Plugins` menu in main window.
- `Plugins` menu uses static entries in v1.
- User can run plugin-provided commands that open WinForms windows.
- Plugin implementations support both compiled assemblies and script-based plugins.
- Plugins are loaded at runtime when their window/command is opened.
- Only one instance per plugin window/command can exist at once.
- Plugin windows can access and modify active tabular model through supported API surface.
- Plugin windows can access and modify app settings through supported API surface.
- Plugin model actions participate in normal Undo/Redo behavior.
- Plugin settings changes apply immediately when possible.
- Plugin actions do not corrupt app/model state.
- Initial validation plugins:
- Settings window plugin to change left model-tree panel background color per connected tabular model, persisted and remembered.
- Raw model JSON/TMSL viewer plugin that reloads displayed content when model changes.

## Constraints

- Backwards-compatible for users who do not use plugins.
- Allow-all trust model for v1 (no plugin security restrictions).
- Plugin projects/scripts live under `TabularEditor/Plugins/`.
- Per-model settings identity uses server + database name (example: `localhost/TabularEditorAgentFinance`).
- Validation for this feature is manual scenarios only.
- No implementation plan yet; this file captures intake only.
- Keep scope focused on runtime plugin extensibility and menu integration.

## Out Of Scope

- Packaging/distribution strategy decisions not yet confirmed.
- Network plugin marketplace design.
- CLI plugin behavior (unless explicitly requested later).
- Context-sensitive plugin menu entries (future extension).
- Plugin manager UI in v1.
