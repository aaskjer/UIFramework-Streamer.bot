# UIFramework logo

**U + i = User interface.** A pixel U and i on a 16 × 16 grid (32 px per cell), broken four ways at
once: blocks with gaps on top (teleport), strips torn sideways in the middle (slice), checker dither below
(dither), pixels dripping off the bottom and leaking up from the top (melt). The dot of the i is amber and
beams away block by block: that dot is the "I".

| File | Use |
|---|---|
| `uiframework-mark.svg` | The mark on its dark tile. Any size from 64 px up. |
| `uiframework-mark-16.svg` | Hand-set pixel version for 16 to 48 px (favicons, tab and window icons): one cell is one pixel; keeps the tear, a drip and the beaming dot. |
| `uiframework-mark-{512,256,128,64}.png` | The mark as PNG. |
| `uiframework-mark-small-{48,32,16}.png` | The pixel version as PNG. |
| `uiframework.ico` | Windows icon: the pixel version at 16, 32, 48, the full mark at 256. |
| `uiframework-logo-light.svg` / `.png` | Mark and wordmark for light backgrounds. |
| `uiframework-logo-dark.svg` / `.png` | Mark and wordmark for dark backgrounds. |
| `uiframework-wordmark-light.svg` / `-dark.svg` | The wordmark alone. |
| `social-preview.png` | 1280 × 640, for the GitHub repository's social preview (Settings › General). |

The wordmark is drawn as pixels on a 5 × 7 grid, not set in a font, so it looks the same everywhere. The
cyan and pink copies are offset a quarter cell, row 3 slips half a cell right, and the I's top bar is amber
like the dot in the mark.

Colours:

| | Dark | Light |
|---|---|---|
| Ground | `#0B0B1A` | `#FFFFFF` |
| Letters | `#FFFFFF` | `#0B0B1A` |
| Cyan | `#22E0FF` | `#00A9CC` |
| Pink | `#FF4FD8` | `#FF4FD8` |
| Amber (the I) | `#FFC857` | `#FFB000` |

In a README, both versions follow GitHub's theme:

```html
<picture>
  <source media="(prefers-color-scheme: dark)" srcset="assets/logo/uiframework-logo-dark.png">
  <img src="assets/logo/uiframework-logo-light.png" alt="UIFramework - your interface" width="520">
</picture>
```
