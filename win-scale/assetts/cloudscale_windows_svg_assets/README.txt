CloudScale – Windows SVG Asset Pack (transparent backgrounds)

Files included:
- cloudscale_icon.svg
  Primary icon mark (cloud + satellite + dish). Transparent background.
- cloudscale_tray_monochrome_64.svg
  Single-color (white) tray icon, designed for dark taskbars.
- cloudscale_tray_blue_64.svg
  Single-color (blue) tray icon, designed for light taskbars.
- cloudscale_logo_horizontal.svg
  Icon + wordmark + tagline (transparent background).
- installer_banner_493x58.svg
  Standard installer top banner size (WiX/MSI/NSIS friendly).
- installer_dialog_493x312.svg
  Standard installer side image size (WiX/MSI friendly).

Notes:
- These SVGs use the Montserrat font if available; otherwise they fall back to Arial.
  If you need font-perfect rendering, rasterize to PNG at build time (or convert text to outlines in a vector editor).

Quick conversions (examples):
1) PNG:
   inkscape input.svg --export-type=png --export-width=256 --export-filename=out.png

2) ICO (generate multiple PNG sizes then pack):
   inkscape cloudscale_icon.svg --export-type=png --export-width=256 --export-filename=256.png
   inkscape cloudscale_icon.svg --export-type=png --export-width=128 --export-filename=128.png
   inkscape cloudscale_icon.svg --export-type=png --export-width=64  --export-filename=64.png
   inkscape cloudscale_icon.svg --export-type=png --export-width=48  --export-filename=48.png
   inkscape cloudscale_icon.svg --export-type=png --export-width=32  --export-filename=32.png
   inkscape cloudscale_icon.svg --export-type=png --export-width=16  --export-filename=16.png
   convert 256.png 128.png 64.png 48.png 32.png 16.png cloudscale.ico

Recommended Windows icon sizes:
16, 24, 32, 48, 64, 128, 256
