# Lattice

> Structural precision for your desktop workspace.

Project: https://github.com/Castald0/Lattice

A small Windows desktop app for saving and restoring named window layouts. Built independently with original source code; no Mosaico installation or registration is needed.

## Start

Download or build, then double-click `Lattice.exe`. No installer is needed. Keep the executable wherever you want; the source and build script are optional.

1. Open your apps and arrange their windows.
2. In Lattice, click **Refresh** and check the windows to include.
3. Enter a layout name and click **Save layout**.
4. Move your windows around, select the saved layout, and click **Restore**.

Saving with the same name asks before replacing a layout. Delete also asks for confirmation.

**Hide to tray** keeps the app running. Right-click its tray icon to restore a layout or quit. The window's close button also hides it to the tray.

## Logi Tune

Select `Lattice.exe` as the application to launch. No command-line arguments are required. Choose your saved layout in the app and click Restore, or use its tray menu.

## What this version does

- Saves selected windows' positions, sizes, monitor work areas, and maximized state.
- Restores matching open windows, with positions adjusted when monitor work areas change.
- Moves windows from a missing monitor onto the primary monitor and keeps target rectangles within its work area.
- Matches application name and window title first. A changed title is accepted only when one remaining saved window and one remaining open window belong to that application.
- Reports missing or ambiguous windows instead of guessing among them.

## Limits

- Closed apps are not launched. Documents and browser tabs are not reopened.
- This version does not automatically arrange newly opened apps, provide drag-to-zone snapping, or register global hotkeys.
- Minimized windows are restored visibly. Virtual desktop membership and window stacking order are not saved.
- Windows with app-enforced minimum sizes or custom placement behavior may adjust the requested geometry.
- If an app runs as administrator, Lattice may also need to run as administrator to move it.
- Multiple monitors and changed monitor arrangements are supported in the positioning logic. Physical multi-monitor and mixed-DPI configurations have not been tested here.
- This is a locally built, unsigned executable.

## Your saved layouts

Layouts are stored in `%LOCALAPPDATA%\Lattice\layouts.xml`. A previous copy is kept as `layouts.xml.bak` when saving. To transfer your layouts, quit Lattice on both computers and copy that file into the same folder on the other computer. App names and window titles still need to match; monitor names may differ.

Layout files contain application names and window titles, which can include document names. No network requests are made by the app.

## Source and build

`src/Lattice.cs` contains the source. Run `build.ps1` in PowerShell to compile it with the .NET Framework compiler included on this PC. The app uses Windows Forms and Windows APIs.

```powershell
.\build.ps1                       # produces bin\Lattice.exe
.\test.ps1                        # core tests
.\test.ps1 -IncludeWindowTests    # also opens temporary test windows
```

GitHub Actions builds the executable and runs core tests on Windows. Successful runs provide a `Lattice-Windows` download artifact.

## Validation

Passed automated checks for saved-layout XML round trips, exact-title matching, single-window dynamic titles, ambiguous-window skipping, negative monitor coordinates, and disconnected-monitor scaling.

Passed controlled Windows checks for external application window discovery, actual position and size restoration, maximized-state capture and restoration, and main-window construction. The rendered interface was visually checked. Your everyday applications and actual monitor configuration still need a tryout.

## Project direction

Saved layouts are the first working component of Lattice. The following ideas from the original project outline remain planned, not implemented: geometric tiling, customizable keyboard shortcuts, monitor cycling, configurable gaps, and animations.

Lattice belongs to the productivity suite described in the original project outline:

- **CataList** — workflow launcher and quick indexer
- **ReAgent** — automation and reactive desktop triggers
- **Lattice** — window tiling and structural layout manager

This initial build does not yet integrate with CataList or ReAgent.
