# Common Pitfalls

Starter file. Add only pitfalls that are verified or clearly relevant from repo inspection. Mark speculative items as `to verify`.

## Verified/Clearly Relevant

- Classic .NET Framework projects use `packages.config`; missing `packages/` will break package imports until restore runs.
- `TOMWrapper.csproj` links `AntlrGrammars/obj/Debug/DAXLexer.cs`; grammar/generated output order can matter.
- Installer/signing/release files are present and should be avoided unless explicitly required.
- WinForms designer/resource files are easy to churn. Avoid touching `*.Designer.cs` and generated resource designer files unless planned.
- Test projects use MSTest-style project metadata and may need Visual Studio test tooling or `vstest.console`.

## To Verify

- Whether `dotnet build` is sufficient for this solution on a machine without Visual Studio MSBuild in PATH.
- Exact supported workflow for regenerating ANTLR and T4 outputs.
- Which UI interactions have existing test seams versus needing manual validation.
