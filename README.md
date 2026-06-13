# Canup Photo Organizer

Canup Photo Organizer is a WinForms desktop application for reviewing, organizing, comparing, renaming, converting, rating, and archiving image files from a session-based workspace.

The application supports:

- Session-based configuration stored in `C:\ProgramData\PhotoOrganizer\PhotoOrganizer.config`
- Per-session source folders, destination folders, visible actions, thumbnail size, sort order, recurse-subdirectories option, info-pane width, filename display, date-difference highlighting, and optional action display in the info pane
- Supported source types: `JPG`, `JPEG`, `PNG`, `GIF`, `TIFF`, `TIF`, `HEIC`, `HEIF`, and `PDF`
- `HEIC` / `HEIF` loading now uses a Windows Imaging Component fallback path instead of only `System.Drawing`, which improves compatibility on systems where the Microsoft HEIF/HEVC extensions are installed
- Recursive or top-level file loading, depending on session settings
- Square thumbnail grid with selection, keyboard navigation, thumbnail resizing, fullscreen view, and side-by-side compare view
- Actions for copy, move, date update, rename, convert, autocrop, rotate, delete, rating, compare, and external edit
- Incremental grid updates where practical instead of full reloads
- Session-scoped append-only change logging in `C:\ProgramData\PhotoOrganizer\PhotoOrganizer.changes.ndjson`
- Undo support for logged write actions, with rollback backups in `C:\ProgramData\PhotoOrganizer\UndoBackups` when needed

## Project Layout

- `src\MainForm.cs`: main window, session flow, loading pipeline, menus, actions, undo integration
- `src\ThumbnailGridControl.cs`: custom thumbnail grid, selection, keyboard handling, thumbnail resize behavior
- `src\PhotoMetadataHelper.cs`: thumbnail generation, metadata reads, date comparison helpers, preview loading
- `src\ImageFileOperations.cs`: filesystem actions such as rename, convert, move, copy, rotate, and delete
- `src\SessionModels.cs`: session, action, sort-order, rename-rule, and app config models
- `src\SessionConfigStore.cs`: session config persistence and normalization
- `src\ChangeLogModels.cs` / `src\ChangeLogStore.cs`: change-log model and NDJSON persistence
- `src\UndoLogForm.cs`: undo history window
- `src\CompareViewerForm.cs`: two-image fullscreen compare viewer
- `src\ShellRatingHelper.cs`: Windows Shell rating read/write integration for JPEG and PNG
- `src\PdfPhotoProcessor.cs`: PDF rendering and autocrop/image-processing helpers
- `build.ps1`: compile script and EXE version-resource stamping

## Build

Required runtime/build target: `.NET Framework 4.0` (`CLR v4.0.30319`)

Run:

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

Output:

- `bin\PhotoOrganizer.exe`

The build currently uses the .NET Framework C# compiler directly and stamps Windows file-version metadata after compilation.

## HEIC / HEIF Notes

- The app now uses a WIC-based decode fallback for `HEIC` / `HEIF` files instead of relying only on GDI+ / `System.Drawing`.
- This improves compatibility on machines where the Microsoft HEIF/HEVC extensions are installed but `Image.FromFile(...)` still fails.
- `HEIC` / `HEIF` still depend on the Windows codec being available on the target machine in this build.
- `HEIC` / `HEIF` viewing, thumbnails, compare, and conversion are supported.
- `HEIC` / `HEIF` rotate and autocrop are currently disabled to avoid writing back an invalid file format.

## Runtime Storage

- Session config: `C:\ProgramData\PhotoOrganizer\PhotoOrganizer.config`
- Change log: `C:\ProgramData\PhotoOrganizer\PhotoOrganizer.changes.ndjson`
- Undo backups: `C:\ProgramData\PhotoOrganizer\UndoBackups`

## Documentation

- AI coding agent guide: [docs/AI_AGENT_GUIDE.md](C:/Users/markc/Downloads/PictureOrganizer/docs/AI_AGENT_GUIDE.md)
- Human developer guide: [docs/DEVELOPER_GUIDE.md](C:/Users/markc/Downloads/PictureOrganizer/docs/DEVELOPER_GUIDE.md)
- End-user help file: [docs/PhotoOrganizerHelp.htm](C:/Users/markc/Downloads/PictureOrganizer/docs/PhotoOrganizerHelp.htm)
