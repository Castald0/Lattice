# Lattice

> Structural precision for your desktop workspace.

Project: https://github.com/Castald0/Lattice

A dark Windows desktop app for positioning windows, saving their existing arrangements, and snapping dragged windows into preset zones. Built independently with original source code; no Mosaico installation or registration is needed.

## Start

Download or build, then double-click `Lattice.exe`. No installer is needed. Keep the executable wherever you want; the source and build script are optional.

1. Open your apps, then click **Refresh** in Lattice.
2. Select a window's row and use **Left half**, **Right third**, a corner, **Center**, or **Maximize**. Choose a display to place it on another monitor, or enter X, Y, Width, and Height and click **Apply position**.
3. Check the windows you want to include, enter a layout name, and click **Save layout**. This captures their current positions; manually arranged windows work too.
4. Select a saved layout and click **Restore** to return its matching open windows to those positions.

Selecting a row chooses the window to position. Checkboxes choose what a saved layout includes. **Undo last move** reverses the last move made with the positioning buttons.

## Drag into presets

For standard zones, enable **Snap while dragging** and choose **Halves**, **Thirds**, or **Quarters**. Drag an app by its title bar. A translucent highlight shows the target zone under the pointer; release to snap into it.

For your own presets:

1. Arrange windows at the exact positions and sizes you want as zones.
2. Save that arrangement as a named layout.
3. Select it and click **Use selected layout**. This enables drag snapping and uses the saved rectangles as custom zones.
4. Drag any compatible window into a zone, even if it wasn't in the saved layout.

Press **Escape** or **right-click** during a drag to bypass snapping for that entire drag. The highlight disappears and won't return until the next drag. Windows may also cancel its native move operation when you press Escape. The input is passed through normally.

Drag snapping starts disabled until you enable it. The selected preset and enabled setting are saved. It continues to work while Lattice is hidden in the tray. Use the checkbox or quit Lattice to turn it off. Window-border resizing does not activate snapping. Custom zones on different monitors use their saved monitor assignments; if a monitor is missing, those zones map to the primary monitor.

Saving with the same name asks before replacing a layout. Delete also asks for confirmation.

**Hide to tray** keeps the app running. Right-click its tray icon to restore a layout or quit. The window's close button also hides it to the tray.

## Logi Tune

Select `Lattice.exe` as the application to launch. No command-line arguments are required. Choose your saved layout in the app and click Restore, or use its tray menu.

## What this version does

- Saves selected windows' positions, sizes, monitor work areas, and maximized state.
- Direct positioning with halves, thirds, quarters, centering, maximizing, exact coordinates, and monitor selection.
- Dark application controls, dropdown menus, dialogs, and tray menu. Windows title-bar styling depends on operating-system support.
- Drag previews and snap-on-release for built-in or saved-layout zones, with Escape/right-click bypass.
- Restores matching open windows, with positions adjusted when monitor work areas change.
- Moves windows from a missing monitor onto the primary monitor and keeps target rectangles within its work area.
- Matches saved window handles and process lifetimes first, so switching tabs or documents doesn't break restoration while the same windows remain open. After an app restarts, it falls back to unique application/title matches, then an unambiguous single remaining window from that application.
- Reports missing or ambiguous windows instead of guessing among them.

## Limits

- Closed apps are not launched. Documents and browser tabs are not reopened.
- This version does not automatically arrange newly opened apps or register global positioning hotkeys.
- Drag snapping uses standard Windows title-bar move events. Apps with custom drag behavior or higher permissions may not participate. Windows Snap or another window manager can also influence the final position.
- Minimized windows are restored visibly. Virtual desktop membership and window stacking order are not saved.
- Windows with app-enforced minimum sizes or custom placement behavior may adjust the requested geometry.
- If an app runs as administrator, Lattice may also need to run as administrator to move it.
- Multiple monitors and changed monitor arrangements are supported in the positioning logic. Physical multi-monitor and mixed-DPI configurations have not been tested here.
- This is a locally built, unsigned executable.

## Your saved layouts

Layouts are stored in `%LOCALAPPDATA%\Lattice\layouts.xml`. A previous copy is kept as `layouts.xml.bak` when saving. To transfer your layouts, quit Lattice on both computers and copy that file into the same folder on the other computer. App names and window titles still need to match; monitor names may differ.

**Upgrading from the first version:** existing layouts still load, but they don't contain window identities. Arrange the windows as desired, click Refresh, and save each layout again once. This captures their current identities and titles. If several same-app windows restart with new titles, they may still need to be re-saved. Restore results now name each unmatched window and explain whether its app is absent or the match is ambiguous.

Layout files contain application names and window titles, which can include document names. No network requests are made by the app. During a supported drag, input hooks detect only Escape and right-click cancellation; no keyboard or mouse input is recorded.

## Source and build

`src/` contains the source. Run `build.ps1` in PowerShell to compile it with the .NET Framework compiler included on this PC. The app uses Windows Forms and Windows APIs.

```powershell
.\build.ps1                       # produces bin\Lattice.exe
.\test.ps1                        # core tests
.\test.ps1 -IncludeWindowTests    # also opens temporary test windows
.\test.ps1 -RequireDragInput      # requires an interactive desktop; exercises real mouse drags
```

GitHub Actions builds the executable and runs core tests on Windows. Successful runs provide a `Lattice-Windows` download artifact.

## Validation

Passed automated checks for saved-layout XML round trips, exact-title matching, single-window dynamic titles, ambiguous-window skipping, negative monitor coordinates, disconnected-monitor scaling, preset geometry, drag zone selection, cancellation for the remainder of a drag, and cancellation reset for the next drag.

Passed controlled Windows checks for external window discovery, all 11 preset buttons, exact position/size fields, centering, maximizing, snapping from maximized state, undo, fresh title/identity capture, restoration after a title change with multiple same-app windows, drag-event hook registration, and saved-layout zone geometry. The dark interface was rendered and visually checked.

The build session cannot access an interactive input desktop, so physical drag highlighting and Escape/right-click interaction have **not** been verified here. `-IncludeWindowTests` reports that skip explicitly; `-RequireDragInput` requires those tests to run. Interactive tests move the pointer and use temporary test windows, so let them finish before using the mouse. Your everyday apps and physical multi-monitor setup still need a tryout.

## Project direction

The following ideas from the original project outline remain planned: customizable keyboard shortcuts, keyboard-driven monitor cycling, configurable gaps, and animations.

Lattice belongs to the productivity suite described in the original project outline:

- **CataList** — workflow launcher and quick indexer
- **ReAgent** — automation and reactive desktop triggers
- **Lattice** — window tiling and structural layout manager

This initial build does not yet integrate with CataList or ReAgent.
