# Build and Test

First pass from solution/project/package inspection and local PATH check. No build/test command was run to avoid creating build artifacts and because required packages/tools were absent from PATH.

## Verified In This Environment

- `TabularEditor.sln` exists and contains `TabularEditor`, `TOMWrapper`, `AntlrGrammars`, `TabularEditorInstaller`, `TabularEditorTest`, and `TOMWrapperTest`.
- `BPALib.sln` exists and contains `BPALib`.
- Main projects target `.NET Framework v4.8`.
- `TabularEditor/TabularEditor.csproj` is a classic non-SDK WinForms `WinExe` project.
- `TOMWrapper/TOMWrapper.csproj` is a classic non-SDK class library.
- `AntlrGrammars/AntlrGrammars.csproj` uses `Antlr4.CodeGenerator` for `CSharpLexer.g4` and `DAXLexer.g4`.
- `TabularEditorTest/TabularEditorTest.csproj` and `TOMWrapperTest/TOMWrapperTest.csproj` are MSTest-style .NET Framework test projects.
- `dotnet.exe` is present in PATH.
- `msbuild`, `vstest.console`, and `nuget` were not found in PATH by `Get-Command`.
- `packages/` directory was not present at repo root during inspection.
- `AntlrGrammars/obj/Debug/DAXLexer.cs` was not present during inspection, although `TOMWrapper.csproj` links to it.
- SQL Server default instance is reachable at `localhost`.
- SQL Server Analysis Services default instance is reachable at `localhost`.
- SSAS service `MSSQLServerOLAPService` was running as `NT Service\MSSQLServerOLAPService`.
- SQL OLE DB provider `MSOLEDBSQL` was installed.
- ADOMD.NET assembly was available at `C:\Program Files\Microsoft.NET\ADOMD.NET\170\Microsoft.AnalysisServices.AdomdClient.dll`.

## Local SQL Server / SSAS Test Environment

This machine has local test data and a deployed SSAS tabular model for Tabular Editor feature testing.

### SQL Server

- Server: `localhost`
- Database: `TabularEditorAgentTest`
- Recommended .NET `SqlConnection` string:

```text
Data Source=localhost;Initial Catalog=TabularEditorAgentTest;Integrated Security=True;Persist Security Info=False;Pooling=False;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=True;Application Name="Codex"
```

- User-provided SSMS-style connection string also included `Command Timeout=0`. `System.Data.SqlClient.SqlConnection` does not accept `Command Timeout` as a connection-string keyword; set `SqlCommand.CommandTimeout = 0` instead.
- Tables:
  - `dbo.D_calendar`
  - `dbo.D_product`
  - `dbo.D_customer`
  - `dbo.D_SalesOrder`
  - `dbo.D_SalesInvoice`
- Fact tables link to dimensions by `D_calendar_SID`, `D_product_SID`, and `D_customer_SID`.
- The SSAS service account was granted `db_datareader` on `TabularEditorAgentTest` so import processing can read source data.

### Analysis Services

- Server: `localhost`
- Database/model: `TabularEditorAgentFinance`
- Recommended SSAS connection string:

```text
Data Source=localhost;Catalog=TabularEditorAgentFinance
```

- Model tables:
  - Facts: `D_SalesOrder`, `D_SalesInvoice`
  - Dimensions: `D_calendar`, `D_product`, `D_customer`
- Relationships:
  - `D_SalesOrder` to all 3 dimensions by `{dimension}_SID`
  - `D_SalesInvoice` to all 3 dimensions by `{dimension}_SID`
- Measures include:
  - `Total Order Sales`
  - `Gross Order Sales`
  - `Order Discount Amount`
  - `Order Discount %`
  - `Order Count`
  - `Average Order Value`
  - `Total Invoice Amount`
  - `Tax Amount`
  - `Net Invoice Revenue`
  - `Invoice Count`
  - `Average Invoice Value`
  - `Revenue Realization %`
  - `Outstanding Order Value`
  - `Assumed Gross Margin`
  - `Assumed Gross Margin %`
- Smoke-test DAX query returned:
  - `Total Order Sales = 4609.5`
  - `Net Invoice Revenue = 4609.5`
  - `Tax Amount = 1058.28`
  - `Revenue Realization % = 1`
  - `Assumed Gross Margin = 1290.66`

### Tools Used For Local Data/SSAS Setup

- PowerShell with `System.Data.SqlClient` created/refreshed SQL tables and granted SQL read access to the SSAS service account.
- `SQLCMD.EXE` was present at `C:\Program Files\Microsoft SQL Server\Client SDK\ODBC\180\Tools\Binn\SQLCMD.EXE`, but was not needed for setup.
- `SQLPS` PowerShell module was present at `C:\Program Files\Microsoft SQL Server\170\Tools\PowerShell\Modules\SQLPS\SQLPS.psd1`; it exposed SQL Server cmdlets, but `Invoke-ASCmd` was not available.
- ADOMD.NET from `C:\Program Files\Microsoft.NET\ADOMD.NET\170\Microsoft.AnalysisServices.AdomdClient.dll` executed TMSL create/refresh commands and DAX smoke tests against SSAS.
- Direct TOM/AMO loading from mixed SQL Server/SSMS assemblies was not used for deployment because local dependency loading was unreliable in PowerShell.

## Inferred Commands

These are likely commands for a Windows/Visual Studio environment after NuGet package restore, but they were not run here:

```powershell
nuget restore TabularEditor.sln
msbuild TabularEditor.sln /p:Configuration=Debug /p:Platform="Any CPU"
vstest.console TabularEditorTest\bin\Debug\TabularEditorTest.dll TOMWrapperTest\bin\Debug\TOMWrapperTest.dll
```

Possible targeted project builds after restore:

```powershell
msbuild AntlrGrammars\AntlrGrammars.csproj /p:Configuration=Debug /p:Platform="Any CPU"
msbuild TOMWrapper\TOMWrapper.csproj /p:Configuration=Debug /p:Platform="Any CPU"
msbuild TabularEditor\TabularEditor.csproj /p:Configuration=Debug /p:Platform="Any CPU"
```

Possible `dotnet` commands may fail or be incomplete because these are classic .NET Framework projects with `packages.config` and Visual Studio/MSBuild dependencies:

```powershell
dotnet build TabularEditor.sln --configuration Debug --no-restore
```

## Required Build Tools (Windows)

- Install Visual Studio Build Tools 2022 (or Visual Studio Community 2022) with the `.NET desktop build tools` / `.NET desktop development` workload so `MSBuild.exe` is available.
- Install `.NET Framework 4.8 Developer Pack` (projects target `v4.8`).

## Verified Working Build Command

This command was executed successfully in this environment:

```powershell
"C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" TabularEditor.sln /t:Build /p:Configuration=Debug /m
```

## Verified Working Test Command

This targeted command was executed successfully in this environment:

```powershell
"C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe" TabularEditorTest\bin\Debug\TabularEditorTest.dll /Tests:TabularEditor.PluginLoaderTests.Load_AllScriptPluginsFromManifest_CompileAndInstantiate,TabularEditor.PluginLoaderTests.Load_ModelJsonViewerScriptPlugin_CompilesAndInstantiates
```

## Not Verified Here

- Full solution build.
- NuGet package restore.
- Full test suite execution across `TabularEditorTest` and `TOMWrapperTest`.
- Installer project build.
- ANTLR generated output regeneration.
- T4 template regeneration.

## Validation Guidance

- For TOM/model behavior, prefer targeted `TOMWrapperTest` tests when available, then broader wrapper test run.
- For CLI changes, prefer `TabularEditorTest/CLITests.cs` and any related serialization/scripting tests.
- For UI changes, add targeted tests only where existing seams allow; otherwise document manual WinForms validation steps in the feature log.
- For grammar changes, verify ANTLR generation before compiling consumers.
- Record exact command, result, and limitation in the feature `implementation-log.md`.

## Runtime Script Plugin Compiler Compatibility

- Script-backed plugins (`*.csx` loaded by `PluginLoader`) compile through CodeDom at runtime and behave like an older C# compiler than the main project language version.
- Use compatibility-safe syntax in runtime plugin scripts:
  - avoid string interpolation (`$"..."`)
  - prefer classic string concatenation/formatting
- If a script plugin fails with parser errors like `CS1056 Unexpected character '$'`, treat it as a runtime compiler compatibility issue first.
- After any edit to `TabularEditor/Plugins/**/*.csx`, run plugin script compile/load tests before considering the change done.
- Required regression coverage includes:
  - `TabularEditor.PluginLoaderTests.Load_AllScriptPluginsFromManifest_CompileAndInstantiate`
  - `TabularEditor.PluginLoaderTests.Load_ModelJsonViewerScriptPlugin_CompilesAndInstantiates`
- Example targeted test command:

```powershell
vstest.console TabularEditorTest\bin\Debug\TabularEditorTest.dll /Tests:TabularEditor.PluginLoaderTests.Load_AllScriptPluginsFromManifest_CompileAndInstantiate
```
