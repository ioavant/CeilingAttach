# Ceiling Attach — product facts

> Source of truth for the website and Autodesk App Store copy.
> Edit here; both the site and the store listing pull from this document.

## Identity

| Field | Value |
|---|---|
| Product name | Ceiling Attach |
| Publisher | Vixeldorf |
| Category | MEP (Mechanical, Electrical, Plumbing) |
| Current version | 1.0.0.0 |
| Supported Revit versions | 2022 · 2023 · 2024 · 2025 · 2026 |
| Help / website URL | https://www.vixeldorf.com |
| Support email | yoav@vixeldorf.com |
| Support SLA | 5 business days |

---

## One-liner

Automatically snaps ceiling-mounted MEP elements and Spaces to the underside of the nearest floor or roof above them, with a configurable gap — one click instead of manual offset edits.

---

## What problem it solves

Positioning ceiling-mounted MEP elements — light fixtures, smoke detectors, sprinkler heads, grilles, diffusers — at a precise gap from the structural ceiling above is repetitive work. Each element has to be moved individually, and the target height depends on where the slab actually is in the model, which varies across a floor plate. Ceiling Attach automates that calculation: select the elements, click the button, done.

---

## How it works (one core mechanic)

The add-in casts a vertical ray upward from each selected element. It finds the nearest floor slab or roof (including geometry from Revit-linked models) and moves the element so its top sits at the configured gap from that surface. MEP Spaces get their upper limit adjusted instead. Everything runs inside a single Revit transaction — one undo reverts all changes.

---

## Features

- Supports both **Floor slabs** and **Roof slabs** as target surfaces
- Finds ceiling geometry in **Revit-linked models** automatically
- Processes **multiple elements and Spaces** in one operation
- **Configurable gap** — exact clearance from the ceiling underside (mm)
- **Configurable search height** — limits how far above to scan, preventing false matches from upper floors
- **Selection filter** — only model elements are pickable; annotations, detail lines, and model lines are excluded automatically
- **Per-element result report** — shows direction and distance moved for each element, and lists any that had no ceiling found within range

---

## Step-by-step usage

1. Open a Revit project containing MEP elements or Spaces to snap to the ceiling.
2. *Optional* — click the arrow under the "Attach To Ceiling" button → **Settings** to set gap and search height (both in mm).
3. Select the elements or Spaces to attach (multi-select supported; annotations are excluded automatically).
4. Click **Attach To Ceiling** on the **Vixeldorf** tab → **Ceiling Attach** panel.
5. A results dialog lists every element moved (direction + distance in mm) and any that were skipped.
6. **Ctrl+Z** undoes everything at once.

---

## Version history

### 1.0.0.0 — initial release
- Attach MEP elements and Spaces to the nearest Floor or Roof above them
- Configurable gap and search height
- Multi-element selection with per-element result report
- Revit-linked model geometry included in search

---

## Known limitations

- **No slab on the ray** — elements beside a stairwell opening or curtain wall have no slab directly above them. They are reported as "no ceiling found" and skipped; increasing the search height does not help because no geometry exists on that ray. This is expected behaviour.
- **Family origin offset** — the ray starts from the element's Revit location point. For families whose origin sits far from their visible body, the ceiling matched may differ from what a visual check suggests.
- **Spaces are slower to process** — Revit's spatial index must be refreshed after each Space is modified, so large Space selections take proportionally longer than the same count of MEP elements.

---

## Data storage

Ceiling Attach stores nothing outside of Revit. All changes are native parameter edits (element offsets and Space upper limits) written inside a standard Revit transaction — fully visible in undo history and safe for worksharing. No external database, no Extensible Storage, no files written outside the model.

---

## Installation

The installer (`CeilingAttach_Setup_Vixeldorf.msi`) is a standard Windows MSI built with WiX:

- **DLL** → `C:\ProgramData\Vixeldorf\CeilingAttach\CeilingAttach.dll`
- **Addin manifests** → `C:\ProgramData\Autodesk\Revit\Addins\{year}\Vixeldorf_CeilingAttach_{year}.addin` for each selected Revit version (2022–2026)

Uninstall through **Windows Settings → Apps** or **Control Panel → Programs and Features** ("Vixeldorf Ceiling Attach"). Removes the DLL and all manifests.
