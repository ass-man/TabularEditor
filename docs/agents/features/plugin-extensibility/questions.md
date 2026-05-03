# Clarification Questions

## Questions And Answers

| Question | Answer | Status |
| --- | --- | --- |
| Should plugin loading source be local folder only, or also NuGet/zip/other sources? | Same repo plugins under `TabularEditor/Plugins/` for v1. | Answered |
| Should plugins load only at app startup, or allow enable/disable/reload at runtime from UI? | Runtime only, load when plugin window opened. | Answered |
| What trust/safety model is required (unsigned local code allowed, signed-only, allowlist, warning prompts)? | Allow all; no security restrictions in v1. | Answered |
| For model edits by plugins, should plugin actions participate in Undo/Redo as normal user actions? | Yes. | Answered |
| Should plugins target one API style: compiled .NET assemblies, C# scripts, or both? | Both. | Answered |
| For app settings changes by plugins, should changes apply immediately, or only after explicit save/app restart? | Immediately when possible. | Answered |
| Should `Plugins` menu support both static entries and context-sensitive entries based on current object selection? | Static only in v1; keep extensible for future context-sensitive entries. | Answered |
| Should there be a built-in plugin manager UI in v1 (list, enable/disable, settings), or menu-only launch support first? | Menu launch only in v1. | Answered |
| Must plugin windows be single-instance per plugin command, or can multiple instances open? | Single-instance per plugin. | Answered |
| What minimum validation is required before acceptance (manual scenario list, targeted automated tests, both)? | Manual validation scenarios only, including two first plugins: model-tree panel color settings window (persist per server+database model identity) and raw model JSON/TMSL viewer (auto reload on model change). | Answered |
| "Raw TOML" display should map to what canonical format? | Use model JSON/TMSL view (not TOML). | Answered |
| Per-model color persistence key should use what identity? | Server + database name (example `localhost/TabularEditorAgentFinance`). | Answered |

## Assumptions

- Initial target is desktop WinForms UI only.
- Plugin commands primarily launch windows/dialogs and interact with existing app state.
- User expects feature to be named `Plugins` in main top menu.
- Plugins live inside same repository for v1 development.
- Plugin security hardening intentionally deferred.

## Unresolved Items

- None at intake stage.
