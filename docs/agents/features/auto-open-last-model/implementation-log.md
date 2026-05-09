# Auto Open Last Model Implementation Log

## 2026-05-09

- Added `RecentFiles.LastOpenedModel`.
- Records successful file/folder model opens in `UIController.File_Open`.
- Records successful database opens in `UIController.Database_Open`.
- Added `FormMain.AutoOpenLastModel()` and invoke it only when no CLI model source is supplied.
