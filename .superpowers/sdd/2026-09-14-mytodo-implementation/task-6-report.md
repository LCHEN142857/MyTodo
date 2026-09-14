# Task 6 Report

Implemented the deterministic packaging pipeline and replaced the README with Windows usage, controls, storage, soft-delete, build, and launch documentation.

## Verification

- `dotnet test MyToDo.sln --filter PackagingContractTests`: 2 passed.
- `.\build.ps1`: restore, full test suite (33 passed), and self-contained single-file `win-x64` publish succeeded.
- `dotnet test MyToDo.sln -c Release --no-restore`: 33 passed.
- `artifacts/publish/MyToDo.exe`: 153,784,001 bytes; PE header validated (`MZ`, `PE\0\0`).
- `git diff --check`: no whitespace errors.

## Notes

- `build.ps1` resolves paths from `$PSScriptRoot`, validates configuration/runtime values, prefers `.tools\dotnet8\dotnet.exe`, runs restore/test/publish separately, safely cleans only `artifacts\publish`, and reports executable size.
- Publish sets `AssemblyName=MyToDo` so the required artifact is `artifacts/publish/MyToDo.exe`.
- `PackagingContractTests` backs up and restores any existing publish directory so its dotnet shim cannot overwrite a real package during the full test suite.

