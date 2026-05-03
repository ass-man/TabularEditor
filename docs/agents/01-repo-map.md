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
- `TOMWrapper/`: model wrapper library. Contains `TOMWrapper/` objects, `Serialization/`, `Utils/`, `UndoFramework/`, `PropertyGridUI/`, `TextServices/`, and generated/template files.
- `AntlrGrammars/`: ANTLR grammar sources for C# and DAX lexing.
- `TabularEditorTest/`: app-level tests, including `CLITests.cs`, `ScriptEngineTests.cs`, BPA tests, UI tree tests, regression tests, and test data.
- `TOMWrapperTest/`: wrapper-level tests, regression tests, test data, and T4-generated test helpers.
- `Scripts/`: property export/import helper scripts.
- `Documentation/`: product/user documentation.
- `.github/ISSUE_TEMPLATE/`: issue templates. No workflow files found in targeted listing.

## Where To Start

- UI changes: `TabularEditor/FormMain.cs`, `TabularEditor/FormMain.Designer.cs`, `TabularEditor/UI/UIController*.cs`, `TabularEditor/UI/Actions/`, `TabularEditor/UI/Dialogs/`. Avoid designer/resource edits unless needed and planned.
- TOM/model behavior: `TOMWrapper/TOMWrapper/*.cs`, `TOMWrapper/TOMWrapper/TabularModelHandler*.cs`, `TOMWrapper/Utils/`, `TOMWrapper/Serialization/`, `TOMWrapper/UndoFramework/`, then matching tests in `TOMWrapperTest/`.
- CLI behavior: `TabularEditor/Program.cs`, `TabularEditor/CommandLineHandler.cs`, `TabularEditor/ConsoleHandler.cs`, then `TabularEditorTest/CLITests.cs`.
- Tests: `TabularEditorTest/` for app/CLI/UI/scripting/BPA behavior; `TOMWrapperTest/` for model wrapper/serialization/undo/regression behavior.
- Parser/grammar work: `AntlrGrammars/CSharpLexer.g4`, `AntlrGrammars/DAXLexer.g4`, generated outputs, `TabularEditor/TextServices/`, and `TOMWrapper/TextServices/`.
- Installer/release work: `TabularEditorInstaller/`, `signfiles.txt`, project publish/signing settings. Do not touch unless explicitly required.

## Uncertain

- Exact Visual Studio/MSBuild version required for successful local build was not verified.
- Exact test runner command was not verified in this environment.
- Generated ANTLR/T4 workflow needs future verification before changing grammar or generated files.
