# Architecture Notes

Facts below come only from targeted inspection. Add detail only after verified future work.

## UI

- App is a .NET Framework WinForms executable in `TabularEditor/TabularEditor.csproj`.
- Main entry point is `TabularEditor/Program.cs`; WinForms launches `FormMain` when command-line handling does not short-circuit into CLI-only mode.
- UI work appears split across `FormMain*`, `UI/UIController*.cs`, `UI/Actions/`, `UI/Dialogs/`, `PropertyGrid/`, `TreeViewAdv/`, and `TreeViewAdvExtension/`.
- `TabularEditor/UI/Actions/` contains action abstractions such as `ModelAction`, `ModelActionManager`, undo/redo/delete actions, and text box actions.

## TOM/Model Wrapper

- `TOMWrapper/TOMWrapper.csproj` builds a `TOMWrapper` class library targeting .NET Framework 4.8.
- Model object wrappers live under `TOMWrapper/TOMWrapper/` with files for tables, columns, measures, relationships, partitions, roles, perspectives, cultures, calculation groups, and handler classes.
- `TabularModelHandler*.cs` files are likely central for loading, saving, events, database operations, and file handling.
- Undo behavior lives in `TOMWrapper/UndoFramework/`.
- Property grid support lives in `TOMWrapper/PropertyGridUI/`.

## Tests

- `TabularEditorTest/` covers app-level behavior including CLI, scripting, Best Practice Analyzer, UI tree, reported issues, and regressions.
- `TOMWrapperTest/` covers model wrapper behavior including serialization/deserialization, object creation/handling, undo, dependency/fixup, Power BI, and regressions.
- Tests are MSTest-style projects targeting .NET Framework 4.8.

## Serialization/Deployment

- `TOMWrapper/Serialization/` and `TOMWrapper/TOMWrapper/Serialization/` contain serialization-related code.
- `TOMWrapper/Utils/TabularDeployer.cs` and `TOMWrapper/Utils/ITabularDeployer.cs` are deployment-related.
- CLI deployment and build/save flows are handled in `TabularEditor/CommandLineHandler.cs`.
- CLI supports model build/save to `.bim`, folder, TMDL, deployment, XMLA/TMSL generation, schema check, BPA analyze, and script execution based on inspected usage text.

## Scripting/CLI

- CLI entry is `Program.RunWithArgs`, which delegates to `CommandLineHandler.HandleCommandLine` when more than one argument is provided.
- `CommandLineHandler.cs` loads models from file/server/local Power BI instance, executes scripts, saves/builds model outputs, analyzes BPA rules, and deploys.
- `ConsoleHandler.cs` attaches output to the parent console for command-line use.
- App scripting code lives in `TabularEditor/Scripting/`.

## Future Detail Needed

- Add verified UI command/action lifecycle notes after first UI feature.
- Add verified model load/save lifecycle notes after first model behavior feature.
- Add verified generated-code workflow for ANTLR and T4 outputs before grammar/generator changes.
