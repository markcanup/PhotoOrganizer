# AI Agent Guide

## Purpose

This project is a Windows Forms desktop application for organizing image files by session. An AI coding agent working in this repo should preserve existing behavior while making focused, incremental changes.

## Core Concepts

- The main application shell is in `src/MainForm.cs`.
- Session state lives in `OrganizerSession` in `src/SessionModels.cs`.
- Session persistence is handled by `src/SessionConfigStore.cs`.
- The thumbnail grid is custom-drawn in `src/ThumbnailGridControl.cs`.
- File metadata and thumbnail creation live in `src/PhotoMetadataHelper.cs`.
- Mutating file actions live in `src/ImageFileOperations.cs`.
- Undoable change logging is modeled in `src/ChangeLogModels.cs` and persisted by `src/ChangeLogStore.cs`.
- Undo UI is in `src/UndoLogForm.cs`.
- The embedded end-user help window is in `src/HelpViewerForm.cs`.

## Persistence Model

- Config file: `C:\ProgramData\PhotoOrganizer\PhotoOrganizer.config`
- Change log: `C:\ProgramData\PhotoOrganizer\PhotoOrganizer.changes.ndjson`
- Undo backups: `C:\ProgramData\PhotoOrganizer\UndoBackups`

When changing session shape, update:

- `OrganizerSession` in `src/SessionModels.cs`
- normalization rules in `src/SessionConfigStore.cs`
- the edit UI in `src/SessionEditorForm.cs`
- display/application logic in `src/MainForm.cs`

## UI Rules

- The grid and info pane must not overlap.
- Thumbnail cells are square and resize together.
- Multi-select behavior should stay conventional: `Ctrl` toggles, `Shift` selects ranges.
- Single-file-only actions should stay disabled when multiple files are selected.
- PDF restrictions must remain enforced where appropriate.

## Loading Pipeline

- File discovery and metadata hydration are intentionally separate.
- `RefreshPhotos()` first creates placeholder items, then hydrates thumbnails and metadata asynchronously.
- Avoid reloading the entire grid unless the source set actually changed.
- Prefer `UpdateGridItem`, `AddGridItems`, `ReplaceGridItem`, and `RemoveItems` for targeted refreshes.

## Undo / Change Logging Rules

- Do not log read-only actions such as fullscreen, compare viewing, or photo loading.
- Log write actions in a way that supports rollback.
- Prefer one log entry per affected file when an action touches multiple files.
- If a feature changes file bytes or destroys a file, ensure undo has enough data to restore it.
- Do not clear log entries automatically unless rollback succeeded or the user explicitly cleared them.

## Ratings

- JPEG and PNG ratings use the Windows Shell property system via `src/ShellRatingHelper.cs`.
- GIF rating is unsupported.
- Avoid reintroducing config-based ratings.

## Build

Use:

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

The build script:

- compiles with the .NET Framework compiler
- targets `.NET Framework 4.0` (`CLR v4.0.30319`)
- references `Microsoft.VisualBasic.dll` for recycle-bin support
- stamps the EXE with Windows version metadata
- writes the final app to `bin\PhotoOrganizer.exe`

Versioning rule:

- Increment the EXE version on every new build-oriented change.
- Keep `src/AssemblyInfo.cs` and the stamped values in `build.ps1` in sync.

## Safe Change Strategy

When editing:

1. Identify whether the change is session, grid, file-operation, metadata, or undo related.
2. Update the smallest number of files possible.
3. Preserve existing incremental update behavior.
4. Rebuild with `build.ps1`.
5. If a change affects persistence, keep backward compatibility for existing saved sessions and change-log entries.

## Common Extension Points

- Add a new session option:
  - `SessionModels.cs`
  - `SessionConfigStore.cs`
  - `SessionEditorForm.cs`
  - `MainForm.cs`
- Add a new mutating action:
  - `SessionActionType`
  - `SessionActionCatalog`
  - context-menu wiring in `MainForm.cs`
  - file-operation logic in `ImageFileOperations.cs` or helper class
  - change-log entry and rollback support
- Add new metadata display:
  - `PhotoItem.cs`
  - `PhotoMetadataHelper.cs`
  - `MainForm.cs`

## Known Design Constraints

- This is a direct WinForms codebase, not MVVM.
- Much of the UI is manually positioned or manually laid out.
- The application relies on Windows-specific APIs for ratings and recycle-bin integration.
- Explorer-visible EXE metadata is stamped post-build, not only via assembly attributes.
