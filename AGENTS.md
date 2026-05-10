# AGENTS.md

This repository is Tabular Editor 2.x, the open-source .NET Framework WinForms C# app. Future agents must not assume the whole codebase is in context. Start narrow, inspect only relevant files, and keep notes durable.

## First Reading Path

1. `docs/agents/00-start-here.md`
2. `docs/agents/01-repo-map.md`
3. `docs/agents/03-build-and-test.md`
4. Relevant feature plan under `docs/agents/features/`

## Major Repo Areas

- `TabularEditor.sln`: main solution for app, TOM wrapper, ANTLR grammar project, installer project, and tests.
- `TabularEditor/`: WinForms application, CLI handler, scripting, UI controllers, dialogs, resources, and app services.
- `TOMWrapper/`: wrapper/model layer around Analysis Services Tabular objects, serialization, deployment utilities, undo framework, property grid support, and DAX/text helpers.
- `AntlrGrammars/`: ANTLR lexer grammars for C# and DAX.
- `TabularEditorTest/`: app, CLI, scripting, BPA, UI-tree, and regression tests.
- `TOMWrapperTest/`: TOM/model wrapper, serialization, undo, dependency/fixup, Power BI, and regression tests.
- `TabularEditorInstaller/`: Visual Studio installer project and installer scripts. Do not touch unless explicitly planned.
- `BPALib.sln` and `BPALib.csproj`: separate Best Practice Analyzer library solution/project.
- `Scripts/`: helper scripts for property import/export.
- `Documentation/`: user/product documentation.

## Working Rules

- No broad refactors unless explicitly planned.
- Make small, reviewable changes.
- UI features should first locate forms, controls, commands, and controllers in `TabularEditor/`, especially `FormMain*`, `UI/`, `UI/Actions/`, and `UI/Dialogs/`.
- Model behavior should inspect `TOMWrapper/` and relevant tests in `TOMWrapperTest/` first.
- Grammar/parser work should inspect `AntlrGrammars/` first.
- Keep behavior backwards-compatible unless the feature plan says otherwise.
- Update feature implementation logs after important discoveries.
- Prefer adding or updating targeted tests near existing related tests.

## Feature Workflow

1. Feature interviewer clarifies vague requests.
2. Architect writes a self-contained plan.
3. Developer implements only the approved plan.
4. Reviewer checks the diff against the request and plan.
5. Repo cartographer updates durable docs when verified knowledge was learned.

Every feature folder should be self-contained enough for a fresh Codex session to resume from this file plus that feature plan.

## Verification Expectations

- Before editing, identify the smallest relevant build/test scope.
- Run targeted tests when possible.
- If validation cannot run, record the exact limitation in the feature implementation log or review.
- Do not claim a command passed unless it was run in this environment.

## Do Not Touch Unless Explicitly Required

- Installer files under `TabularEditorInstaller/`.
- Signing/release files such as `signfiles.txt`, release scripts, package manifests, and publish/signing settings.
- Generated files such as `*.Designer.cs`, `*.resx` designer outputs, `*.generated.cs`, `obj/`, `bin/`, and ANTLR generated outputs unless the approved plan specifically requires regeneration.
- Broad formatting-only edits across production code.





CRITICAL: Use `distill` only when the desired answer is a compressed summary, classification, extraction, or short structured result.

Prefer distill for running builds and tests.
Do NOT pipe through `distill` when:
- exact raw command output is required
- the expected answer is larger than about 400 tokens
- the task asks to list every match/path/line from a large command
- the result must preserve every line, full paths, ordering, or formatting
- debugging truncation, pagination, terminal output, or command I/O behavior

For large `rg`, `find`, `git grep`, `ls`, `cat`:
- prefer narrowing the command first
- use `rg -l` when only filenames are needed
- use `rg -n -m <N>` or more specific patterns
- redirect large raw output to a temp file when needed
- inspect chunks with `sed -n`, `head`, `tail`, or `wc -l`
- only use `distill` after narrowing the output enough that the final answer is expected to fit under 400 tokens

Good:
  rg -n "foo" src > /tmp/rg.out
  wc -l /tmp/rg.out
  sed -n '1,120p' /tmp/rg.out

Good:
  rg -l "foo" src | distill "Return only the 10 most relevant file paths and why each matters."

Bad:
  rg -n "foo" src | distill "Return all matches exactly."

# RTK - Rust Token Killer (Codex)

**Usage**: Token-optimized CLI proxy for shell commands.

## Rule

Always prefix shell commands with `rtk`.

Examples:

```bash
rtk rg
rtk git status
rtk npm run build
rtk pytest -q
```

## Meta Commands

```bash
rtk gain            # Token savings analytics
rtk gain --history  # Recent command savings history
rtk proxy <cmd>     # Run raw command without filtering
```

## Verification

```bash
rtk --version
rtk gain
which rtk
```
