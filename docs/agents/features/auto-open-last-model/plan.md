# Auto Open Last Model Plan

## Scope

- Store the last successfully opened model in existing recent-file persistence.
- Update successful file/folder opens and database opens to save that last model record.
- During startup, if no command-line model source was supplied, open the last saved model.

## Validation

- Build using `build.ps1`.
- Run relevant targeted tests if available.
