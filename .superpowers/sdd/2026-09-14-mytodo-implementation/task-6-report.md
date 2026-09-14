# Task 6 Report

Implemented the deterministic packaging pipeline and replaced the README with Windows usage, controls, storage, soft-delete, build, and launch documentation.

## Verification

- `dotnet test MyToDo.sln --filter PackagingContractTests`: 2 passed.
- `.\build.ps1`: restore, full test suite (33 passed), and self-contained single-file `win-x64` publish succeeded.
- `dotnet test MyToDo.sln -c Release --no-restore`: 33 passed.

## Round 2 Review Fixes

- Restricted `-OutputPath` to a strict descendant of the repository `artifacts` directory; the artifacts root itself and external paths are rejected.
- Added ancestor-by-ancestor reparse-point validation from the requested output path through `artifacts`, preventing junction/symlink redirection during recursive cleanup.
- Added focused contract coverage for external output rejection and reparse-point ancestor rejection.

## Round 2 Verification

- Focused `PackagingContractTests`: 4 passed.
- Default `artifacts/publish` remains a valid single-file executable from the prior bounded publish verification.
- `artifacts/publish/MyToDo.exe`: 153,784,001 bytes; PE header validated (`MZ`, `PE\0\0`).
- `git diff --check`: no whitespace errors.

## Notes

- `build.ps1` resolves paths from `$PSScriptRoot`, validates configuration/runtime values, prefers `.tools\dotnet8\dotnet.exe`, runs restore/test/publish separately, safely cleans only `artifacts\publish`, and reports executable size.
- Publish sets `AssemblyName=MyToDo` so the required artifact is `artifacts/publish/MyToDo.exe`.
- `PackagingContractTests` backs up and restores any existing publish directory so its dotnet shim cannot overwrite a real package during the full test suite.

## Round 1 Review Fixes

- Added `-OutputPath` to `build.ps1`; the packaging contract now uses a temporary output directory and never mutates `artifacts/publish`.
- Added cleanup validation for filesystem roots, repository paths outside `artifacts`, files, and reparse-point directories.
- Added `IncludeNativeLibrariesForSelfExtract=true`, `DebugSymbols=false`, and `DebugType=None`; the default publish now leaves only `MyToDo.exe`.

## Round 1 Verification

- `dotnet test MyToDo.sln --filter PackagingContractTests`: 2 passed.
- `.\build.ps1`: full suite 33 passed; default publish produced one file, `artifacts/publish/MyToDo.exe` (163,692,495 bytes).
- Artifact header validation: `MZ` and `PE\0\0`.
- `dotnet test MyToDo.sln -c Release --no-restore`: 33 passed.
