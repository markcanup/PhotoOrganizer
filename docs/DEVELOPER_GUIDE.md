# Developer Guide

## Overview

Canup Photo Organizer is a .NET Framework 4.0 WinForms application that helps users review and organize photo collections with session-based settings and undoable write actions.

## Architecture

### Main layers

- UI shell: `src/MainForm.cs`
- Session dialogs: `src\SessionEditorForm.cs`, `src\SessionListForm.cs`
- Viewers: `src\FullscreenViewerForm.cs`, `src\CompareViewerForm.cs`
- Custom controls: `src\ThumbnailGridControl.cs`
- Metadata helpers: `src\PhotoMetadataHelper.cs`, `src\ShellRatingHelper.cs`
- File operations: `src\ImageFileOperations.cs`, `src\RecycleBinHelper.cs`
- Safe replacement, journaling, and undo: `src\FileSafety.cs`, `src\FileOperationJournal.cs`, `src\FileUndoOperations.cs`
- Autocrop preparation and preview: `src\PreparedImageEdit.cs`, `src\AutoCropPreviewForm.cs`
- Persistence: `src\SessionConfigStore.cs`, `src\ChangeLogStore.cs`
- Models: `src\SessionModels.cs`, `src\ChangeLogModels.cs`, `src\PhotoItem.cs`

### Data flow

1. `MainForm` loads session config.
2. `RefreshPhotos()` discovers files and inserts placeholder `PhotoItem` rows.
3. Background hydration replaces placeholders with full thumbnails and metadata.
4. UI actions mutate files and update the grid incrementally where possible.
5. Mutating actions append session-scoped change-log entries for undo.

## Build and Run

Build:

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

Output:

- `bin\PhotoOrganizer.exe`

The script compiles directly with `csc.exe` and then stamps EXE version-resource metadata.

## Storage Locations

- Config: `C:\ProgramData\PhotoOrganizer\PhotoOrganizer.config`
- Change log: `C:\ProgramData\PhotoOrganizer\PhotoOrganizer.changes.ndjson`
- Undo backups: `C:\ProgramData\PhotoOrganizer\UndoBackups`

## Session Model

Current session settings include:

- session name and session ID
- source folders
- destination folders
- visible actions
- thumbnail size
- filename display toggle
- show-actions-in-info-panel toggle
- recurse-subdirectories toggle
- highlight-date-differences toggle
- sort order
- info-pane width percentage

If you add a new setting, update:

- `OrganizerSession`
- `SessionConfigStore.NormalizeSessions`
- `SessionEditorForm`
- any dependent runtime logic in `MainForm`

## Change Log and Undo

The change log uses newline-delimited JSON so it can scale to large histories without rewriting on every append.

Write actions that are logged:

- copy
- move
- date update
- rename
- convert
- rotate
- delete
- rating
- session settings

Undo behavior lives in `MainForm.cs`. If you add a new undoable action:

File actions now write a per-file recovery entry before mutation. Persisted payloads include SHA-256 fingerprints and original-file or overwritten-destination backups. Failed commits recover immediately when possible; failed recovery retains the entry and backups. New copy/move/rename/conversion results explicitly identify their source and destination instead of relying on selection order. Image outputs are validated in a temporary file and committed with `File.Replace`; do not add a copy-over fallback. Undo requires existing backups and verifies current content before destructive changes. Legacy payloads remain readable through `ChangePayloadReader`, with explicit confirmation for records without fingerprints. Autocrop has its own log kind and previews each prepared output before committing.

Run `powershell -ExecutionPolicy Bypass -File .\tests\run-tests.ps1` for isolated regression tests. The suite uses generated fixtures and an injected journal, so it does not write to the user's configuration, history, or backup directory.

1. create a payload model in `ChangeLogModels.cs`
2. append a log entry from the action path
3. add rollback handling in `MainForm.RollbackEntry`
4. update `ChangeLogFormatter.cs` for readable undo text

## UI Notes

### Main window

- launches maximized
- contains a menu, a thumbnail grid, and a resizable info pane

### Thumbnail grid

- custom-drawn
- supports keyboard navigation and drag resize
- can highlight date mismatches in yellow when enabled

### Undo window

- multi-select list
- shows formatted change entries, not raw object names

## Metadata Rules

- JPEG and PNG ratings are read/written through Windows Shell properties
- GIF ratings are unsupported
- PDF uses first-page preview and page count instead of EXIF date
- HEIC / HEIF use a WIC/WPF decoder fallback path when `System.Drawing` cannot decode them
- HEIC / HEIF still depend on the Microsoft HEIF/HEVC codec support being present on the machine in this build
- filename date comparison accepts:
  - `YYYYMMDD-...`
  - `YYYY-MM-DD...`

### HEIC / HEIF write limitations

- HEIC / HEIF loading, preview, and conversion are supported.
- Rotate and autocrop are intentionally disabled for HEIC / HEIF because the current save-back pipeline does not write native HEIC/HEIF output.

## Compare View

The compare viewer displays:

- left image at 50% width
- right image at 50% width
- filename
- file size
- image dimensions

## Version Metadata

The compiled EXE should expose these Windows file properties:

- File description: `Photo Organizer Application`
- File version: `1.0.3.0`
- Product name: `Canup Photo Organizer`
- Product version: `1.0.3`
- Copyright: `Copyright (C) 2026 Mark Canup`

These are stamped in the build pipeline, not only in managed assembly attributes.

Increment the version metadata on each new build-oriented release so the EXE version always moves forward.

## Recommended Maintenance Practices

- Prefer targeted grid updates instead of full reloads.
- Preserve backward compatibility for config and change-log parsing.
- Treat `MainForm.cs` as the integration point and keep helper logic in helper files when possible.
- Rebuild after any UI or persistence change.
