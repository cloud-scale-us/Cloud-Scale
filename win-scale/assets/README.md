# Scale Streamer Assets

This directory contains all graphical assets for the Scale RTSP Streamer application and installer.

## Directory Structure

### `/icons`
Application icons in various states and sizes.

**Required Files:**
- `app-icon.ico` - Main application icon (256x256, 128x128, 64x64, 48x48, 32x32, 16x16)
- `tray-icon-connected.ico` - System tray icon when connected (green)
- `tray-icon-disconnected.ico` - System tray icon when disconnected (gray)
- `tray-icon-error.ico` - System tray icon on error (red)
- `desktop-shortcut.ico` - Desktop shortcut icon
- `file-association.ico` - `.scaleconfig` file icon

**Icon Requirements:**
- Format: ICO (multi-size)
- Sizes: 16x16, 32x32, 48x48, 64x64, 128x128, 256x256
- Color depth: 32-bit with alpha transparency

### `/installer`
WiX installer UI images.

**Required Files:**
- `banner.png` - Top banner (493 x 58 pixels)
- `dialog.png` - Dialog background (493 x 312 pixels)
- `license.rtf` - End User License Agreement
- `welcome.png` - Welcome screen image (164 x 164 pixels)
- `finished.png` - Completion screen image (164 x 164 pixels)

**Image Requirements:**
- Format: PNG with transparency OR BMP (24-bit)
- Follow WiX Toolset UI size specifications
- Professional appearance
- Cloud-Scale branding

### `/branding`
Corporate branding assets.

**Required Files:**
- `cloud-scale-logo.png` - Logo only (transparent PNG, 512x512)
- `cloud-scale-wordmark.png` - Logo with "Cloud-Scale" text (transparent PNG)
- `favicon.ico` - Web interface favicon (16x16, 32x32)
- `splash-screen.png` - Application startup screen (800 x 600 pixels)

**Branding Requirements:**
- Transparent backgrounds (PNG)
- High resolution for scaling
- Consistent color scheme
- Professional quality

### `/overlays`
Video stream overlay templates and fonts.

**Required Files:**
- `default-background.png` - Default video background (1920 x 1080)
- `corporate-template.png` - Corporate branding template overlay
- `fonts/RobotoMono-Bold.ttf` - Monospace font for weight display
- `fonts/Inter-Regular.ttf` - UI font

**Overlay Requirements:**
- Resolution: Match video output (1920x1080 recommended)
- Format: PNG with alpha channel
- Optimize for readability on various backgrounds
- Consider contrast for industrial environments

---

## Asset Creation Guide

### 1. Logo Preparation

Use the existing logo at `installer/logo.png` as a starting point:

1. **Remove background** using [remove.bg](https://www.remove.bg/)
2. **Resize** to 512x512 for main logo
3. **Create variations** with different states (connected/disconnected/error)
4. **Generate ICO files** from PNG using online converter or ImageMagick:
   ```bash
   magick convert logo.png -define icon:auto-resize=256,128,64,48,32,16 app-icon.ico
   ```

### 2. System Tray Icons

Create three states with color indicators:

- **Connected** (Green): Normal operation
  - Green checkmark or indicator
  - Bright, active appearance

- **Disconnected** (Gray): Idle/offline
  - Grayscale version
  - Muted appearance

- **Error** (Red): Problem detected
  - Red exclamation or indicator
  - Attention-grabbing

**Example using ImageMagick:**
```bash
# Green (connected)
magick convert app-icon.png -colorize 30,60,30 tray-icon-connected.ico

# Gray (disconnected)
magick convert app-icon.png -colorspace Gray tray-icon-disconnected.ico

# Red (error)
magick convert app-icon.png -colorize 60,10,10 tray-icon-error.ico
```

### 3. Installer Images

**Banner** (493 x 58):
- Cloud-Scale logo on left
- Product name: "Scale RTSP Streamer"
- Version number
- Subtle gradient background

**Dialog** (493 x 312):
- Branded background
- Professional appearance
- Not too busy (text overlay will appear)
- Complementary colors

**Welcome/Finished** (164 x 164):
- Cloud-Scale logo centered
- High contrast
- Clear and recognizable

### 4. Fonts

**For Weight Display:**
- Use monospace font (digits same width)
- Bold weight for visibility
- Recommended: Roboto Mono, Consolas, Source Code Pro

**For UI:**
- Clean, modern sans-serif
- Good readability at small sizes
- Recommended: Inter, Segoe UI, Roboto

**License Note:** Ensure fonts have appropriate licenses for commercial distribution.

---

## Quick Start

### Option 1: Use Existing Logo

If you already have a logo:

1. Place logo in `branding/cloud-scale-logo.png`
2. Run the asset generation script (coming soon)
3. Manually adjust sizes if needed

### Option 2: Download Free Assets

Use free, commercial-friendly resources:

**Icons:**
- [Iconify](https://iconify.design/) - Free SVG icons
- [Flaticon](https://www.flaticon.com/) - Icon packs
- [Icons8](https://icons8.com/) - Free icons with attribution

**Fonts:**
- [Google Fonts](https://fonts.google.com/) - 100% free
  - Roboto Mono: https://fonts.google.com/specimen/Roboto+Mono
  - Inter: https://fonts.google.com/specimen/Inter

**Images:**
- [Unsplash](https://unsplash.com/) - Free stock photos
- [Pexels](https://www.pexels.com/) - Free industrial images

### Option 3: Professional Design

For best results, hire a professional designer to create:
- Complete icon set
- Installer graphics
- Video overlay templates
- Brand identity package

---

## Current Status

| Asset | Status | Action Required |
|-------|--------|-----------------|
| `app-icon.ico` | ⚠️ Missing | Create from logo |
| `tray-icon-*.ico` | ⚠️ Missing | Create 3 variations |
| `banner.png` | ⚠️ Missing | Design WiX banner |
| `dialog.png` | ⚠️ Missing | Design WiX dialog background |
| `cloud-scale-logo.png` | ✅ Exists at `installer/logo.png` | Move and remove background |
| `license.rtf` | ✅ Exists at `installer/license.rtf` | Verify content |
| Fonts | ⚠️ Missing | Download from Google Fonts |

---

## Tools

**Image Editing:**
- [GIMP](https://www.gimp.org/) - Free, open-source
- [Paint.NET](https://www.getpaint.net/) - Free (Windows)
- [Photopea](https://www.photopea.com/) - Free, browser-based

**Icon Converters:**
- [ConvertICO](https://convertico.com/) - Online PNG to ICO
- [ICO Convert](https://icoconvert.com/) - Multi-size ICO creator
- [ImageMagick](https://imagemagick.org/) - Command-line tool

**Font Tools:**
- [FontForge](https://fontforge.org/) - Font editor
- [Google Fonts](https://fonts.google.com/) - Free font repository

---

## Integration with Installer

Once assets are created, the WiX installer will automatically include them:

```xml
<!-- Icons -->
<Icon Id="AppIcon" SourceFile="$(var.AssetsDir)\icons\app-icon.ico" />
<Property Id="ARPPRODUCTICON" Value="AppIcon" />

<!-- Installer UI -->
<WixVariable Id="WixUIBannerBmp" Value="$(var.AssetsDir)\installer\banner.png" />
<WixVariable Id="WixUIDialogBmp" Value="$(var.AssetsDir)\installer\dialog.png" />

<!-- Desktop Shortcut -->
<Shortcut Icon="$(var.AssetsDir)\icons\desktop-shortcut.ico" ... />
```

Build script will validate all required assets exist before compiling.

---

**Last Updated**: 2026-01-24
**Version**: 2.0.0
**Maintainer**: Cloud-Scale Engineering
