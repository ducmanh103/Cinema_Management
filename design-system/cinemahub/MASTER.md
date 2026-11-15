# CinemaHub — Master Design System (`ui-ux-pro-max`)

> **Project:** CinemaHub — Archival & Editorial Cinema Platform
> **Skill:** `ui-ux-pro-max` (`--variance 9 --motion 8 --density 4`) + `ui-styling` (Canvas Design System: *Concrete Poetry & Analog Meditation*)
> **Art Direction:** **35mm Archival Editorial (A24 × MUBI × Criterion Collection × Swiss Grid)**
> **Anti-AI-Slop Manifesto:** Zero blue-navy SaaS backgrounds (`#050711`), zero purple/indigo/cyan neon glow blobs, zero floating glass SaaS badges on hero images, zero bubbly `999px` gradient pills, zero generic 4-box SaaS KPI counters.

---

## 1. Materiality & Color Token Architecture (Warm Analog Carbon & Paper Ivory)

| Role | Token | Value | Usage |
|------|-------|-------|-------|
| Archival Carbon (Void) | `--bg-void` | `#0A0908` | Warm 35mm screening room black (zero blue tint) |
| Matte Espresso (Canvas) | `--bg-obsidian` | `#11100E` | Primary editorial page background |
| Elevated Studio Surface | `--bg-surface` | `#171513` | Solid architectural cards & ledger panels |
| Interactive Surface | `--bg-surface-hover` | `#201D1A` | Hover & active editorial cells |
| Masthead Surface | `--bg-glass-heavy` | `rgba(10, 9, 8, 0.94)` | Sticky editorial masthead & drawers |
| Film Leader Vermilion | `--brand-primary` | `#D93829` | Solid editorial red CTA (no neon pink-orange gradient) |
| Anamorphic Brass / Gold | `--brand-gold` | `#D4A359` | Archival classification, ratings, VIP seats, brass accents |
| Silver Halide | `--brand-cyan` | `#C8C2B8` | Technical format specs (IMAX 70MM / DOLBY ATMOS) |
| Warm Ivory (Primary Text) | `--text-primary` | `#F3EFE6` | Uncoated paper ivory for headlines & primary copy |
| Warm Stone (Secondary) | `--text-secondary` | `#B5AEA2` | Editorial body copy & descriptions |
| Archival Muted | `--text-muted` | `#7A7369` | Timecodes, catalog indices, footnotes |
| Hairline Grid Border | `--border-glass` | `rgba(243, 239, 230, 0.12)` | Exposed `1px` Swiss grid lines & architectural frames |
| Brass Highlight Border | `--border-glow` | `rgba(212, 163, 89, 0.45)` | Active/hover frame border |

---

## 2. Typography System (Editorial Contrast + Vietnamese Readability)

- **Editorial Serif / Accent Display:** `'Cormorant Garamond'`, `'Playfair Display'`, Georgia, serif *(for italic dramatic film titles, rankings, and editorial pull-quotes)*
- **Architectural Display & UI Sans:** `'Be Vietnam Pro'`, `'Plus Jakarta Sans'`, system-ui, sans-serif *(for razor-sharp Vietnamese headlines, geometric uppercase labels, and body copy)*
- **35mm Timecode & Ticket Stub Mono:** `'JetBrains Mono'`, monospace *(for reel numbers `REEL 01 // 06`, aspect ratios `2.39:1`, runtime `135 MIN`, prices, and seat coordinates)*

---

## 3. Structural & Layout Rules (`--variance 9`)

1. **Exposed 1px Hairline Swiss Grid**: Sections, dossier tables, and movie catalogs use visible `1px solid rgba(243, 239, 230, 0.12)` structural borders with corner registration marks (`+` / crop corners) instead of floating blurry drop-shadow cards.
2. **Sharp Architectural Geometry**: Corner radii are `0px` to `4px` (or notched physical ticket stubs), eliminating bubbly `24px–32px` SaaS cards.
3. **35mm Film Grain Overlay**: Subtle fixed SVG fractal noise overlay (`opacity: 0.028`) gives tactile analog warmth across all screens.
4. **Physical Ticket Stub Mechanics**: Booking docks, showtime selectors, and vouchers mimic real perforated box-office tickets with dashed tear lines, serial numbers, and stamped age classifications (`P`, `T13`, `T16`, `T18`).
5. **No Emoji Icons**: 100% vector icons and typographic index markers (`01 //`, `02 //`, `REEL`).
