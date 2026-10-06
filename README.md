# Lattice

> Structural precision for your desktop workspace.

Project: https://github.com/Castald0/Lattice

A dark Windows desktop app for positioning windows, saving their existing arrangements, and snapping dragged windows into preset zones. Built independently with original source code; no Mosaico installation or registration is needed.

## Start

Version 0.3.3 reopens missing apps before restoring their positions, in addition to the menu-lifetime fixes below. **Open this app if no window is found** defaults on in App/file rules. Each missing app is requested once; already-visible app windows are reused. Open your apps and re-save older layouts once to capture current executable paths and packaged-app identities (important for Store apps such as ChatGPT and Terminal). Reopening an app does not guarantee its previous document or browser page: use a specific file/link rule for that.

Version 0.3.2 closes open dropdowns before Refresh changes their choices and keeps a single tray menu alive while layouts change. Quit runs after the tray click finishes. Regression checks exercise Refresh with open dropdowns and layout changes with an open tray menu. The version is shown in the app; unexpected errors include the executable path and stack trace, also saved to `%LOCALAPPDATA%\Lattice\last-error.txt`. These changes address additional menu-lifetime paths; the reported crash's exact stack was unavailable in earlier versions.

Version 0.3.1 fixes a dropdown-menu lifetime bug that could stop Lattice with “Cannot access a disposed object: ContextMenuStrip.” Menus now remain alive until their owning control is disposed. Repeated menu open/close cycles are covered by the Windows tests. Existing layouts need no conversion for this fix.

Download or build, then double-click `Lattice.exe`. No installer is needed. Keep the executable wherever you want; the source and build script are optional.

1. Open your apps, then click **Refresh** in Lattice.
2. Select a window's row and use **Left half**, **Right third**, a corner, **Center**, or **Maximize**. Choose a display to place it on another monitor, or enter X, Y, Width, and Height and click **Apply position**.
3. Check the windows you want to include, enter a layout name, and click **Save layout**. This captures their current positions; manually arranged windows work too.
4. Select a saved layout to see its **Saved preview** monitor map. Click **Restore** to return matching windows to those positions. **Open windows** returns to the window list.

Selecting a row chooses the window to position. Checkboxes choose what a saved layout includes. **Undo last move** reverses the last move made with the positioning buttons.

## App positions and specific files

Each saved position defaults to **App position — any document or view**. A Word position can hold whichever Word document you currently have open. The same applies to Excel, Teams, and Outlook. With multiple windows, Lattice prefers the original window, then a matching title, then the next available app window. Each window is used once.

To open a particular document in a saved position:

1. Select the layout, then **App/file rules**.
2. Select its app and choose **Specific file or link**.
3. Use **Browse file**, **Use current Word/Excel file**, or paste an app link.
4. Enable **Open this file or link when restoring**, then **Save rules**.

Local or synced Word/Excel files are matched by full path, so a different document with the same filename is not used. An already-open verified file is reused. Teams uses links to a chat, channel, or file; Outlook can use supported links or files such as `.msg`. For other files, supply identifying window title text. Links open through their registered application; a title hint helps identify the resulting window. This does not verify the internal view of Teams or Outlook. A plain Word/Excel web link is rejected because it may open a browser; use a local/synced file or an Office app link.

**Restore maximized (fill this display)** is also available in the rules editor. This controls standard Windows maximization, not app-specific reading, focus, or full-screen modes.

## Accurate saved geometry

Saving waits for the selected windows to settle and reads their actual visible bounds. If an app makes a quarter-screen window taller to satisfy a minimum size, that accepted height is what Lattice saves. On an unchanged monitor work area, restore uses those saved coordinates and dimensions without scaling or clamping them back into a preset. Windows' invisible resize borders are accounted for.

Restore verifies the resulting geometry and retries once. If an app still forces another size or position, the results show the saved and actual bounds. A changed display work area is scaled to fit; that can intentionally produce different coordinates. Maximized windows are saved separately and their restored state is checked.

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
- Defaults to app positions that survive document/title changes; optional file/link rules reserve their matching windows before general app positions.
- Shows a visual saved monitor map, window dimensions, and maximized state.
- Reports unmatched specific targets, missing app windows, and app-adjusted geometry.

## Limits

- App-only positions reopen a missing app using its saved launcher unless disabled in App/file rules. Missing or changed launchers require opening the app and re-saving the layout. Browser tabs and previous documents are not individually recovered by app-only rules; use specific file/link rules. Apps may restore their own previous session according to their own settings.
- This version does not automatically arrange newly opened apps or register global positioning hotkeys.
- Drag snapping uses standard Windows title-bar move events. Apps with custom drag behavior or higher permissions may not participate. Windows Snap or another window manager can also influence the final position.
- Minimized windows are restored visibly. Virtual desktop membership and window stacking order are not saved.
- Windows with app-enforced minimum sizes or custom placement behavior may adjust the requested geometry.
- If an app runs as administrator, Lattice may also need to run as administrator to move it.
- Multiple monitors and changed monitor arrangements are supported in the positioning logic. Physical multi-monitor and mixed-DPI configurations have not been tested here.
- This is a locally built, unsigned executable.

## Your saved layouts

Layouts are stored in `%LOCALAPPDATA%\Lattice\layouts.xml`. A previous copy is kept as `layouts.xml.bak` when saving. To transfer your layouts, quit Lattice on both computers and copy that file into the same folder on the other computer. App names and window titles still need to match; monitor names may differ.

**Upgrading:** quit the old version through its tray menu before opening the new executable. Existing layouts still load as app positions. Arrange the windows as desired, click Refresh, and save each layout again once to capture settled visible bounds and maximized state with the new logic. Overwriting a layout preserves its matching app/file rules.

Layout files contain application names, window titles, executable paths, and any file paths or links you configure. The app has no telemetry or direct network requests; opening a configured link can cause its registered app to access the network. During a supported drag, input hooks detect only Escape and right-click cancellation; no keyboard or mouse input is recorded.

## Source and build

`src/` contains the source. Run `build.ps1` in PowerShell to compile it with the .NET Framework compiler included on this PC. The app uses Windows Forms and Windows APIs.

```powershell
.\build.ps1                       # produces bin\Lattice.exe
.\test.ps1                        # core tests
.\test.ps1 -IncludeWindowTests    # also opens temporary test windows
.\test.ps1 -RequireDragInput      # requires an interactive desktop; exercises real mouse drags
.\test-office.ps1                # optional disposable Word/Excel file integration checks
```

GitHub Actions builds the executable and runs core tests on Windows. Successful runs provide a `Lattice-Windows` download artifact.

## Validation

Automated checks cover saved-layout XML round trips, app/title/identity matching, distinct window assignment, exact Word/Excel target-path matching, launch/wait behavior, negative monitor coordinates, disconnected-monitor scaling, preset geometry, drag zone selection, cancellation for the remainder of a drag, and cancellation reset for the next drag.

Controlled Windows checks cover external window discovery, all 11 preset buttons, exact position/size fields, centering, maximizing, snapping from maximized state, undo, fresh title/identity capture, restoration after a title change with multiple same-app windows, a window that enforces a minimum height, saved maximized-to-normal restoration, drag-event hook registration, and saved-layout zone geometry.

The closed-app test launches a disposable executable through the real launcher, waits for its new window, and verifies its restored coordinates. Controlled tests also cover launch-once behavior, already-open apps, opt-out, and launch errors. Packaged-app activation is implemented but has not been exercised against ChatGPT or Terminal in this build session. Packaged identity capture follows Microsoft's [GetApplicationUserModelId API](https://learn.microsoft.com/en-us/windows/win32/api/appmodel/nf-appmodel-getapplicationusermodelid).

The build session cannot access an interactive input desktop, so physical drag highlighting and Escape/right-click interaction have **not** been verified here. `-IncludeWindowTests` reports that skip explicitly; `-RequireDragInput` requires those tests to run. Interactive tests move the pointer and use temporary test windows, so let them finish before using the mouse. Your everyday apps and physical multi-monitor setup still need a tryout.

Real Word/Excel integration could not be verified in this build session: launching isolated Office test instances failed with Windows' “A specified logon session does not exist” error. File matching and launch orchestration passed controlled tests, but the Office document-inspection integration still needs validation in a normal signed-in desktop session. The optional Office test uses disposable documents and dedicated instances.

## Project direction

The following ideas from the original project outline remain planned: customizable keyboard shortcuts, keyboard-driven monitor cycling, configurable gaps, and animations.

Lattice belongs to the productivity suite described in the original project outline:

- **CataList** — workflow launcher and quick indexer
- **ReAgent** — automation and reactive desktop triggers
- **Lattice** — window tiling and structural layout manager

This initial build does not yet integrate with CataList or ReAgent.
