# Auto Open Last Model

## User Request

Add a feature to Tabular Editor itself so that when opening the app it automatically connects to the previously open model.

## Assumptions

- Applies only when Tabular Editor is launched without a model path/server command-line argument.
- The last successfully opened model can be a file/folder/TMDL/PBIT path or a database connection.
- If the last file path no longer exists, startup should continue without opening anything.
