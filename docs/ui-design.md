# UI Design Rules

These rules are binding for any UI work in `src/OrderOps.Web/`. They apply to every component change, page addition, and visual tweak. They override default shadcn/Tailwind ergonomics where the two conflict. Deviations require updating this document first — same gate as `coding-principles.md` §8.

The theme intent is **minimal**: restraint over flourish, neutral surfaces, single chromatic accent, soft corners, subtle shadows, fast and quiet animations.

---

## 1. Foundations

### 1.1 Color tokens

The palette is **slate + emerald accent**, defined as HSL CSS variables in `src/index.css`. All component colors flow through these tokens — never hardcode `bg-white`, `text-black`, `bg-emerald-600`, or any palette colour directly in component code.

| Token | Light | Dark | Use |
|---|---|---|---|
| `--background` | slate-50 | slate-950 | Page bg |
| `--foreground` | slate-950 | slate-50 | Primary text |
| `--card` | white | slate-900 | Card surface (a tier above bg) |
| `--card-foreground` | slate-950 | slate-50 | Text on cards |
| `--popover` | white | slate-900 | Popovers, dropdowns |
| `--popover-foreground` | slate-950 | slate-50 | Text in popovers |
| `--primary` | emerald-600 | emerald-500 | Primary actions, the only chromatic accent |
| `--primary-foreground` | white | slate-950 | Text on primary |
| `--secondary` | slate-100 | slate-800 | Secondary surfaces |
| `--secondary-foreground` | slate-900 | slate-50 | Text on secondary |
| `--muted` | slate-100 | slate-800 | Subtle bg (skeleton, code, captions) |
| `--muted-foreground` | slate-500 | slate-400 | Tertiary text |
| `--accent` | slate-100 | slate-800 | Hover bg on ghost/outline |
| `--accent-foreground` | slate-900 | slate-50 | Text on accent |
| `--destructive` | red-600 | red-700 | Destructive actions only |
| `--destructive-foreground` | white | slate-50 | Text on destructive |
| `--border` | slate-200 | slate-800 | All borders, divider lines |
| `--input` | slate-200 | slate-800 | Input borders |
| `--ring` | emerald-500 | emerald-400 | Focus ring (always visible) |

**Rules:**
- Single chromatic accent: emerald. No second accent without first updating this table.
- `bg-background` for page, `bg-card` for cards. Never invert (e.g. card colored darker than bg in light mode).
- Destructive (red) is the only allowed second hue, and only on destructive paths.

### 1.2 Typography

Font: **Inter**, loaded via `<link>` from Google Fonts in `index.html` (Latin subset, weights 400/500/600/700) and configured as the default `sans` family in `tailwind.config.js`. Fallback chain: `Inter, ui-sans-serif, system-ui, -apple-system, "Segoe UI", sans-serif`.

| Use | Tailwind classes |
|---|---|
| Page title (h1) | `text-2xl font-semibold tracking-tight` |
| Section title (h2 / `<CardTitle>`) | `text-lg font-semibold tracking-tight` |
| Body (default for dashboard density) | `text-sm` |
| Small / caption | `text-xs text-muted-foreground` |
| Code / IDs / SKU | `font-mono text-xs` |
| Table header | `text-xs uppercase tracking-wide text-muted-foreground` |

**Rules:**
- Don't use weights 800 / 900 — too heavy for a minimal theme.
- Don't use font sizes outside the Tailwind scale (no arbitrary `text-[15px]`).
- One typeface only. No display fonts, no serif accent.

### 1.3 Radius

Single radius token: `--radius: 0.625rem` (10px).

Derived steps (defined in `tailwind.config.js`):
- `lg = var(--radius)` — Cards, dialogs
- `md = calc(var(--radius) - 2px)` — Buttons, inputs, selects
- `sm = calc(var(--radius) - 4px)` — Badges, chips, tooltips

**Rules:**
- Don't introduce additional radius tokens.
- Don't mix radii within a single composite component (a card and its inner button respect their own tier; never override).
- Avatars / fully-round elements use `rounded-full`. Everything else uses the tokens above.

### 1.4 Shadows

Shadows signal **layering**, not drama. Subtle and soft.

| Class | When |
|---|---|
| (no shadow) | Default for inline content, page background, nav |
| `shadow-sm` | Resting cards |
| `shadow-md` | Cards on hover (interactive only); popovers |
| `shadow-lg` | Dialogs, dropdowns, command palettes |

**Rules:**
- Don't layer multiple shadows. One token per element.
- Don't use any shadow stronger than `shadow-lg`.
- Don't use coloured shadows. Greyscale only.
- Cards do **not** auto-hover. Apply `hover:shadow-md transition-shadow duration-200` only when the card is itself interactive (wrapped in `<Link>` or `<button>`).

### 1.5 Animations

Small, fast, intentional. They confirm interactions; they never delay them.

| Effect | Duration | Easing | Where |
|---|---|---|---|
| Color transition (hover, active) | 150ms | `ease-out` | All interactive elements |
| Shadow lift | 200ms | `ease-out` | Cards with `data-clickable` semantics |
| Modal / dialog enter+exit | 200ms / 150ms | shadcn defaults via `tailwindcss-animate` | Dialogs, sheets |
| Skeleton pulse | 1.5s loop | shadcn default | Loading placeholders |
| Page fade-in (optional) | 200ms | `ease-out` | Route transitions |

**Rules:**
- Default duration: **150ms**. Anything ≥300ms requires justification.
- Animate `transform` and `opacity` only — never `width`, `height`, `top`, `left` (causes reflow + jank).
- No bouncy springs. No flashy entrance animations. No coloured glows.
- Don't animate data (table rows appearing one-by-one). Animate UI state (modal open, hover lift).
- All animations must respect `prefers-reduced-motion: reduce` — Tailwind / shadcn defaults already do; verify on custom additions.

---

## 2. Components

### 2.1 Buttons

Use the shadcn `<Button>` (`src/components/ui/button.tsx`). Variants:

| Variant | When | Density per view |
|---|---|---|
| `default` | The primary action (emerald) | One per view; ideally one per card |
| `secondary` | Neutral action | Multiple OK |
| `outline` | Secondary action with explicit boundary | Multiple OK |
| `ghost` | Tertiary; nav, icon buttons, low-emphasis | Multiple OK |
| `destructive` | Destructive only | Always paired with a confirm dialog |
| `link` | Inline text actions | Sparingly |

Sizes: `default`, `sm`, `lg`, `icon`. **Don't** introduce custom sizes.

### 2.2 Cards

Use the shadcn `<Card>` for grouped content. Pages are composed of cards (or no cards) — never raw `<div>` boxes with hand-rolled borders.

- Heading region: `<CardHeader>` + `<CardTitle>` + `<CardDescription>`.
- Body: `<CardContent>`.
- Actions: `<CardFooter>`.
- Resting: `shadow-sm` (already on the primitive).
- Interactive: add `hover:shadow-md transition-shadow duration-200 cursor-pointer` at the call site, not on the primitive.

### 2.3 Interaction states

Every interactive element shows four distinguishable states:

- **Hover** — 150ms color transition. Subtle change (hover should not look like a different button).
- **Focus** — visible 2px `ring` in `--ring` color, 2px offset. **Mandatory** for keyboard a11y. Never disable focus rings.
- **Active** (pressed) — no scale transform > 0.99 if any. Quieter is better.
- **Disabled** — 50% opacity + `cursor-not-allowed`. Don't gate a11y-critical flows by disabled state alone.

### 2.4 Async surface — three explicit states

Every page or section that fetches data shows all three:

- **Loading** — `<Skeleton>` matching the eventual layout (rows, blocks, sized to expected content). Never spinners alone, never empty space.
- **Empty** — short message + optional CTA. Never empty space.
- **Error** — short message in `text-destructive`, with the API error code if present. Never raw stack traces, never bare HTTP status numbers.

---

## 3. Layout

- **Container**: shadcn's default `container`, `padding: 2rem`, `max-width: 1400px`.
- **Spacing scale**: Tailwind defaults (`space-y-2`, `gap-4`, etc.). Don't introduce arbitrary spacing values.
- **Vertical rhythm**: 4 / 8 / 16 / 24 / 32 px units only.
- **Density**: dashboard density. Default body is `text-sm`. Tables: `text-sm` rows, `text-xs uppercase tracking-wide` headers.
- **Whitespace**: generous around content blocks, tight inside dense lists. The page should breathe at the section level.

---

## 4. Dark mode

The theme toggle lives top-right of the nav: tri-state cycling button using lucide icons (`Sun` → `Moon` → `Monitor` → `Sun` …).

- State persists in `localStorage` under key `orderops:theme`.
- `system` reads `prefers-color-scheme` and reacts to OS-level changes.
- Toggle adds/removes `.dark` on `<html>`.

**Rules:**
- All component styles **must** use the §1.1 CSS variable tokens. Never hardcode `bg-white` / `text-black`.
- Test every new component in both light and dark before merging.
- Don't ship dark-only or light-only colors. If a value differs between modes, define both in `:root` and `.dark`.

---

## 5. shadcn usage

- Primitives are added via `npx shadcn@latest add <name>` and committed to `src/components/ui/`.
- Post-add edits are allowed, but must respect §1–§4. Any non-trivial edit gets a one-line `// Modified: <reason>` comment at the top of the component file.
- Don't mix component libraries (no Radix-direct, no Headless UI). Everything goes through shadcn (which is Radix under the hood — single supply chain).
- Icons: **lucide-react** only. Sizes: `size-4` (16px) inline, `size-5` (20px) nav, `size-6` (24px) prominent. Don't import icon SVGs by hand.

---

## 6. Out of scope

- Custom route transitions beyond the optional fade-in.
- Branded illustrations / hero imagery.
- Themed surfaces beyond slate + emerald (and red-on-destructive).
- Internationalization, RTL — English-only.
- Print styles.

---

## How to apply during a session

1. Read this file at the start of any UI session, alongside `coding-principles.md`.
2. When in doubt about a color, radius, shadow, or duration: re-read §1 before reaching for a value.
3. New components must pass a §2.3 / §2.4 review (focus rings + three async states) before being committed.
4. Updates to this file require an explicit decision logged in the same change — same gate as `coding-principles.md` §8.
