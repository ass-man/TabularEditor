# Measure Killer Output Viewer Plan

## Scope

Add a read-only runtime plugin under the existing `Plugins` menu.

## Design

- Register a script plugin named `Measure Killer Output Viewer`.
- Load only the JSON export file requested by the user:
  - `C:\Users\gamer\AppData\Local\Programs\Measure Killer\WWI_test_20260509_200325.json`
- Present a tree view:
  - Model
  - Tables
  - Columns
  - Measures
  - Partitions
  - Relationships
  - Best Practices
- Color unused nodes red and other nodes green.
- Provide refresh, text filtering, selected-object filtering, open JSON, and copy-selection actions.

## Verification

- Run plugin script compile/load tests.
- Run the targeted plugin loader test assembly when available.
