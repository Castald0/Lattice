# Lattice

> Structural precision for your desktop workspace.

**Lattice** is a lightweight, intuitive window management utility designed to organize your open applications into harmonious, snap-to-grid layouts. Inspired by crystal structures and mosaic geometry, Lattice eliminates desktop friction by letting you tile, resize, and command your workspace effortlessly.

---

## Features

- **Geometric Tiling**: Snap windows into halves, thirds, quadrants, or custom ratios with zero overlapping.
- **Fluid Keyboard Control**: Position and resize any active window using intuitive, customizable hotkeys.
- **Multi-Display Alignment**: Move windows across monitors while preserving their proportional layout.
- **Persistent Spaces**: Save and restore dedicated layouts for coding, design, research, or communications.
- **Zero Overhead**: Minimal memory footprint with buttery-smooth animations.

---

## Quick Start

### Installation

Clone the repository and install dependencies:

```bash
git clone https://github.com/your-org/lattice.git
cd lattice
make install
```

*(Pre-built binaries are also available on our [Releases](https://github.com/your-org/lattice/releases) page).*

---

## Default Shortcuts

| Action | Shortcut |
| :--- | :--- |
| **Snap Left / Right** | `Alt + Shift + ←` / `→` |
| **Snap Top / Bottom** | `Alt + Shift + ↑` / `↓` |
| **Maximize / Restore** | `Alt + Shift + F` |
| **Center Focus** | `Alt + Shift + C` |
| **Cycle Display** | `Alt + Shift + Enter` |

*All keybindings can be customized in `~/.config/lattice/config.json`.*

---

## Configuration

Customize padding, gap sizes, and hotkeys via your user configuration:

```json
{
  "gaps": {
    "outer": 12,
    "inner": 8
  },
  "animation_speed_ms": 120,
  "default_layout": "bsp"
}
```

---

## The Suite

Lattice integrates cleanly into the productivity toolchain:

- **CataList** – Workflow launcher and quick indexer
- **ReAgent** – Automation and reactive desktop triggers
- **Lattice** – Window tiling and structural layout manager

---

## License

MIT © [Your Name / Organization]
