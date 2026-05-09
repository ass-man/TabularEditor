# Start Here

This repo is large enough that new agents should not reread everything. Start from the feature folder, then inspect only the code paths the feature touches.

## Minimum Reading Path

1. `AGENTS.md`
2. `docs/agents/01-repo-map.md`
3. `docs/agents/03-build-and-test.md`
4. `docs/agents/06-tabular-editor-csharp-scripting.md` when creating or running Tabular Editor scripts
5. `docs/agents/features/<feature>/request.md`
6. `docs/agents/features/<feature>/plan.md`
7. `docs/agents/features/<feature>/implementation-log.md` if work already started

## Selecting Relevant Areas

- UI behavior: start in `TabularEditor/FormMain*`, `TabularEditor/UI/`, `TabularEditor/UI/Actions/`, `TabularEditor/UI/Dialogs/`, then tests in `TabularEditorTest/`.
- TOM/model behavior: start in `TOMWrapper/TOMWrapper/`, `TOMWrapper/Utils/`, `TOMWrapper/Serialization/`, `TOMWrapper/UndoFramework/`, then tests in `TOMWrapperTest/`.
- CLI behavior: start in `TabularEditor/Program.cs`, `TabularEditor/CommandLineHandler.cs`, `TabularEditor/ConsoleHandler.cs`, then `TabularEditorTest/CLITests.cs`.
- Parser/grammar behavior: start in `AntlrGrammars/`, then references from `TabularEditor/TextServices/` and `TOMWrapper/TextServices/`.
- Installer/release behavior: start in `TabularEditorInstaller/` only if explicitly requested.

## Feature Folder First

Feature work should start from its folder under `docs/agents/features/`. If no folder exists, create one from `_template/` before planning implementation. Keep `plan.md` and `implementation-log.md` current enough for a fresh Codex session to resume.
