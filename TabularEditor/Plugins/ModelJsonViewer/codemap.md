# ModelJsonViewer Code Map

## Purpose
Runtime window plugin that shows the current model script (JSON/TMSL), supports refresh/copy, and includes in-window text search.

## File Structure

1. `ModelJsonViewerPlugin.csx`
- Plugin entrypoint.
- Declares `ModelJsonViewerPlugin` and returns `ModelJsonViewerForm`.
- Implements `IRuntimeWindowPlugin.CreateWindow`.

2. `ModelJsonViewerForm.csx`
- Main window composition and lifecycle.
- Creates toolbar, JSON textbox, and search panel controls.
- Handles timer-based content refresh from `PluginHostContext.ScriptCurrentModelCreateOrReplace()`.
- Handles copy-to-clipboard and timer disposal.

3. `ModelJsonViewerSearch.csx`
- Search and keyboard interaction logic for `ModelJsonViewerForm` (partial class).
- Supports:
  - `Ctrl+F` to show search
  - `F3` / `Shift+F3` for next/previous match
  - `Enter` / `Shift+Enter` in search box
  - `Esc` to close search panel
- Encapsulates match selection and status label updates.

## File Resolution Model

- Plugin loader compiles all `*.csx` files in this folder together (top directory, non-recursive).
- Types and members are shared through normal C# compilation rules.
- No `#load` is required between files.

## Runtime Flow

1. Host creates `ModelJsonViewerPlugin`.
2. `CreateWindow` returns `ModelJsonViewerForm`.
3. Form initializes controls and starts 1-second refresh timer.
4. Timer calls `RefreshContent` and updates text only when script content changed.
5. Search interactions operate on the currently displayed text.
