# Repo Map

First pass from targeted inspection of solution/project files, top-level folders, key CLI files, and test project metadata. Not exhaustive.

## Solutions and Projects

- `TabularEditor.sln`: main Visual Studio solution. Contains `TabularEditor`, `TOMWrapper`, `AntlrGrammars`, `TabularEditorInstaller`, `TabularEditorTest`, and `TOMWrapperTest`.
- `BPALib.sln`: separate solution for `BPALib.csproj`.
- `TabularEditor/TabularEditor.csproj`: WinForms executable, `OutputType=WinExe`, `TargetFrameworkVersion=v4.8`, `LangVersion=10.0`, references `TOMWrapper`.
- `TOMWrapper/TOMWrapper.csproj`: class library, `TargetFrameworkVersion=v4.8`, `LangVersion=10.0`, depends on generated ANTLR DAX lexer output from `AntlrGrammars/obj/Debug/DAXLexer.cs`.
- `AntlrGrammars/AntlrGrammars.csproj`: class library that compiles `CSharpLexer.g4` and `DAXLexer.g4` using `Antlr4.CodeGenerator`.
- `TabularEditorTest/TabularEditorTest.csproj`: MSTest-style unit test project for app/CLI/scripting/BPA/UI tree areas, `TargetFrameworkVersion=v4.8`, `LangVersion=12`.
- `TOMWrapperTest/TOMWrapperTest.csproj`: MSTest-style unit test project for model wrapper behavior, serialization, undo, fixup, Power BI, and regressions, `TargetFrameworkVersion=v4.8`, `LangVersion=12`.
- `TabularEditorInstaller/TabularEditorInstaller.vdproj`: Visual Studio installer project. Treat as release/installer area.

## Major Directories

- `TabularEditor/`: main app. Contains `Program.cs`, `CommandLineHandler.cs`, `ConsoleHandler.cs`, `FormMain*`, `Scripting/`, `TextServices/`, `UI/`, `UI/Actions/`, `UI/Dialogs/`, `UIServices/`, resources, and settings.
- `TabularEditor/Plugins/`: runtime plugin manifest and plugin sources (compiled and script-backed plugins).
- `TabularEditor/bin/Debug/Plugins/`: runtime output copy consumed by the Debug executable for plugin script/manifest loading.
- `TOMWrapper/`: model wrapper library. Contains `TOMWrapper/` objects, `Serialization/`, `Utils/`, `UndoFramework/`, `PropertyGridUI/`, `TextServices/`, and generated/template files.
- `AntlrGrammars/`: ANTLR grammar sources for C# and DAX lexing.
- `TabularEditorTest/`: app-level tests, including `CLITests.cs`, `ScriptEngineTests.cs`, BPA tests, UI tree tests, regression tests, and test data.
- `TOMWrapperTest/`: wrapper-level tests, regression tests, test data, and T4-generated test helpers.
- `Scripts/`: property export/import helper scripts.
- `Documentation/`: product/user documentation.
- `.github/ISSUE_TEMPLATE/`: issue templates. No workflow files found in targeted listing.

## Where To Start

- UI changes: `TabularEditor/FormMain.cs`, `TabularEditor/FormMain.Designer.cs`, `TabularEditor/UI/UIController*.cs`, `TabularEditor/UI/Actions/`, `TabularEditor/UI/Dialogs/`. Avoid designer/resource edits unless needed and planned.
- Plugin runtime/UI changes: start in `TabularEditor/Plugins/Infrastructure/` plus plugin source under `TabularEditor/Plugins/`; when validating locally, ensure runtime copy under `TabularEditor/bin/Debug/Plugins/` is current.
- TOM/model behavior: `TOMWrapper/TOMWrapper/*.cs`, `TOMWrapper/TOMWrapper/TabularModelHandler*.cs`, `TOMWrapper/Utils/`, `TOMWrapper/Serialization/`, `TOMWrapper/UndoFramework/`, then matching tests in `TOMWrapperTest/`.
- CLI behavior: `TabularEditor/Program.cs`, `TabularEditor/CommandLineHandler.cs`, `TabularEditor/ConsoleHandler.cs`, then `TabularEditorTest/CLITests.cs`.
- Tests: `TabularEditorTest/` for app/CLI/UI/scripting/BPA behavior; `TOMWrapperTest/` for model wrapper/serialization/undo/regression behavior.
- Parser/grammar work: `AntlrGrammars/CSharpLexer.g4`, `AntlrGrammars/DAXLexer.g4`, generated outputs, `TabularEditor/TextServices/`, and `TOMWrapper/TextServices/`.
- Installer/release work: `TabularEditorInstaller/`, `signfiles.txt`, project publish/signing settings. Do not touch unless explicitly required.

## Plugin Architecture Entry Points

- Runtime manager and menu wiring: `TabularEditor/Plugins/Infrastructure/PluginRuntimeManager.cs`.
- Manifest parsing and descriptor model: `TabularEditor/Plugins/Infrastructure/PluginCatalog.cs`, `TabularEditor/Plugins/Infrastructure/PluginDescriptor.cs`, `TabularEditor/Plugins/plugins.json`.
- Plugin load/compile behavior (assembly and script): `TabularEditor/Plugins/Infrastructure/PluginLoader.cs`.
- Host capabilities exposed to plugin windows/scripts: `TabularEditor/Plugins/Infrastructure/PluginHostContext.cs`.

## Runtime vs Source Paths

- Plugin source of truth: `TabularEditor/Plugins/...`.
- Debug runtime copy consumed by executable: `TabularEditor/bin/Debug/Plugins/...`.
- If runtime behavior does not match source edits, verify the output copy and restart Tabular Editor.

## Test Coverage Map

- Plugin script compile/load regression coverage: `TabularEditorTest/Plugins/PluginLoaderTests.cs`.
- Manifest-driven test validates all script plugins declared in `TabularEditor/Plugins/plugins.json`.

## Do Not Touch (Unless Required)

- Installer/release artifacts: `TabularEditorInstaller/`, `signfiles.txt`, publish/signing settings.
- Generated or generated-adjacent outputs: ANTLR generated files under `AntlrGrammars/obj/...`, designer-generated WinForms code, and other generated outputs unless regeneration is explicitly part of the approved change.

## Uncertain

- Generated ANTLR/T4 workflow needs future verification before changing grammar or generated files.

## Verified Tooling In This Environment

- MSBuild verified:
  - `C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe`
- MSTest runner verified:
  - `C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe`
