# CinemaHub — Master Design System (ui-ux-pro-max)

> **Project:** CinemaHub — Next-Gen Immersive Cinema & Ticket Booking Platform
> **Skill:** `ui-ux-pro-max` (Variance: 7/10 | Motion: 8/10 | Density: 4/10)
> **Visual Direction:** Obsidian Aurora & Spotlight Gold (OLED Dark + Frosted Glass + Kinetic Storytelling + Bento Grids)

---

## 1. Color Token Architecture (OLED High-Contrast Dark Mode)

| Role | Token | Value | Usage |
|------|-------|-------|-------|
| Void Canvas | `--bg-void` | `#050711` | Deepest OLED background |
| Primary Canvas | `--bg-obsidian` | `#090D1A` | Main page background |
| Elevated Surface | `--bg-surface` | `rgba(17, 23, 43, 0.72)` | Glass cards, bento modules |
| Interactive Surface | `--bg-surface-hover` | `rgba(26, 35, 64, 0.86)` | Card hover & active states |
| Sticky Glass | `--bg-glass-heavy` | `rgba(8, 11, 22, 0.88)` | Navbar, bottom dock, modals |
| Primary Brand CTA | `--brand-primary` | `#FF2E55` | Primary action (Laser Crimson-Rose) |
| Primary Brand Gradient | `--brand-gradient` | `linear-gradient(135deg, #FF2E55 0%, #FF6B35 100%)` | Main booking CTAs |
| Spotlight Gold | `--brand-gold` | `#F5B942` | IMDb ratings, VIP seats, luxury accents |
| Dolby Cyan | `--brand-cyan` | `#38BDF8` | Tech formats (IMAX/Atmos), live counters |
| Aurora Indigo | `--brand-indigo` | `#6366F1` | Ambient glow, AI Assistant, secondary accents |
| Emerald Success | `--brand-emerald` | `#10B981` | Available seats, confirmed tickets |
| Text Primary | `--text-primary` | `#F8FAFC` | Headlines & primary copy (AAA contrast) |
| Text Secondary | `--text-secondary` | `#CBD5E1` | Body descriptions (≥ 7:1 contrast) |
| Text Muted | `--text-muted` | `#94A3B8` | Metadata, captions (≥ 4.5:1 contrast) |
| Border Subtle | `--border-glass` | `rgba(255, 255, 255, 0.09)` | Hairline glass borders |
| Border Spotlight | `--border-glow` | `rgba(245, 185, 66, 0.45)` | Active/hover gold highlight |

---

## 2. Typography System (Full Vietnamese Diacritics Support)

- **Display / Headings:** `'Space Grotesk'`, `'Plus Jakarta Sans'`, `'Be Vietnam Pro'`, sans-serif
- **Body / UI Controls:** `'Plus Jakarta Sans'`, `'Be Vietnam Pro'`, `'Inter'`, system-ui, sans-serif
- **Monospace / Ticket Codes / Timers:** `'JetBrains Mono'`, monospace

---

## 3. Motion Choreography Rules (`--motion 8`)

1. **Scroll Reveal (`[data-animate]`)**: Staggered entrance (`y: 28px -> 0`, `opacity: 0 -> 1`, `duration: 0.65s`, `cubic-bezier(0.16, 1, 0.3, 1)`).
2. **Interactive Spotlight Cards (`.cinema-spotlight`)**: Radial light follows pointer (`--mouse-x`, `--mouse-y`) on desktop.
3. **3D Perspective Poster Tilt (`.cinema-tilt`)**: Subtle `rotateX`/`rotateY` on pointer move, smooth spring reset on leave.
4. **Live Number Counters (`[data-count-to]`)**: Smoothly interpolates numbers when scrolled into view.
5. **Reduced Motion Compliance**: `prefers-reduced-motion: reduce` disables parallax, 3D tilt, and autoplay transitions immediately.

---

## 4. Responsive & Mobile-First Ergonomics

- **Touch Targets**: All interactive controls ≥ `44x44px` (`48px` on mobile drawers and bottom dock).
- **Mobile Bottom Navigation Dock**: Fixed native-like bottom bar on `< 992px` with `padding-bottom: env(safe-area-inset-bottom)`.
- **3D Seat Map Viewport**: Horizontal pan/scroll + "Thu phóng vừa màn hình" (Fit-to-screen zoom toggle) on mobile phones (`375px`).
- **No Emoji Icons**: 100% vector icons (`Bootstrap Icons` & inline SVGs).
