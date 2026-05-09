# Measure Killer Output Viewer

## User Request

Add a new plugin for viewing Measure Killer Excel output files. The program should automatically load files based on the opened model.

Sample output folder inspected:

`C:\Users\gamer\AppData\Local\Programs\Measure Killer\WWI_test`

## Discovered Output Files

- `dax_expressions.xlsx`
- `m_expressions.xlsx`
- `relationships_level_results.xlsx`
- `reports.xlsx`
- `results.xlsx`
- `table_level_results.xlsx`

## Assumptions

- Measure Killer stores model-specific output folders below `%LOCALAPPDATA%\Programs\Measure Killer`.
- The best automatic match is the opened model/database name, with fallbacks based on the loaded file or parent folder names.
- The first implementation should be read-only and should not mutate the Tabular Editor model.
