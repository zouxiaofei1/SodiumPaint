<div align="center">
  <table>
    <tr>
      <td align="center" style="border: none;">
        <img src="./TabPaint/Resources/TabPaint.ico" width="100" height="100" alt="Tab Paint Logo">
      </td>
      <td align="left" style="border: none; vertical-align: middle;">
        <h1 style="margin: 0; font-size: 48px;">Tab Paint</h1>
        <p style="margin: 0; font-size: 18px;"><b>Notepad++ for Images on Windows</b></p>
      </td>
    </tr>
  </table>

  <p>
    Multi-Tab Management · Dual Mode (Viewer/Editor) · AI Powered · Seamless Drag & Drop
  </p>

  <!-- Badges -->
  <img src="https://img.shields.io/badge/Platform-Windows%2010%2F11-blue" alt="Platform">
  <img src="https://img.shields.io/badge/Language-C%23%20%7C%20WPF-purple" alt="Language">
  <img src="https://img.shields.io/badge/Status-Beta%20v0.9.1-orange" alt="Status">
  <img src="https://img.shields.io/badge/license-MIT-green" alt="License">
</div>

<div align="center">
  <a href="./README.md">简体中文</a> | <strong>English</strong>
</div>

---

![App Screenshot](./TabPaint/Resources/screenshot1.png)
![App Screenshot](./TabPaint/Resources/gif1.gif)

## ✨ Features

### · 🖼️ Dual Mode: Viewer & Editor
*   **Viewer Mode**: Minimalist interface for immersive browsing. Supports scroll-to-zoom and GIF playback.
*   **Editor Mode**: Just press **`Tab`**. The toolbar pops up instantly, seamlessly switching you to editing mode.

### · 📑 Manage Images Like Code
Supports **Multi-Tab** management. Open dozens of screenshots simultaneously and switch, compare, or batch process them quickly via the ImageBar—just like using an IDE.

### · 🤖 AI-Powered Toolkit
*   **AI Background Removal**: Built-in ONNX models for fast, offline background removal.
*   **OCR**: Extract text from screenshots instantly without external tools.
*   **Smart Tools**: Screen Color Picker, Auto-Trim Whitespace, One-Click Border.

### · 🖱️ Seamless Drag & Drop Workflow
*   **Clipboard Listener**: Automatically prompts to paste new screenshots as new tabs (Ctrl+V).
*   **Comprehensive Drag & Drop Support**:
    *   Drag files/web images -> Insert into Canvas.
    *   Drag thumbnails from ImageBar -> Generate files on Desktop / Insert into Word / Send to IM apps.
    *   Drag selections -> Insert directly into PPT or documents.

---

## ⌨️ Shortcuts

Tab Paint is designed for keyboard efficiency:

| Shortcut | Function |
| :--- | :--- |
| **`Tab`** | **Toggle Viewer / Editor Mode** |
| `Ctrl` + `N` | New Canvas / New from Clipboard |
| `Ctrl` + `W` | Close Current Tab |
| `Ctrl` + `S` | Save (Overwrite) |
| `Ctrl` + `L` / `R` | Rotate Left / Right |
| `Space` + Drag | Pan Tool (Move Canvas) |
| `Del` | Delete file to Recycle Bin (Undoable, enable in Settings) |
| `Ctrl` + `Wheel` | Zoom Canvas |

---

## 📥 Download & Install

### Requirements
*   **OS**: Windows 10 or Windows 11
*   **Runtime**: .NET 8.0 Desktop Runtime or higher (Installer will prompt if missing).

### Get Tab Paint
1.  **Github Releases (Recommended)**: [Download Latest Version](https://github.com/zouxiaofei1/TabPaint/releases)
    *   `TabPaint_Setup_Full.exe`: Full installer (Runtimes included).
    *   `TabPaint_Setup_Lite.exe`: Lightweight installer (Downloads runtimes if needed).
    *   `Portable.zip`: Portable version (Unzip and run).
2.  (Mirror Links/Cloud Drive if applicable)

---

## ❓ FAQ

**Q: It starts slightly slower than Honeyview/System Photos?**
A: Tab Paint is built with WPF, which requires a bit more initialization time. However, once open, switching tabs or modes is instantaneous. Our philosophy is: **Trade 0.2s of startup delay for 10 minutes of uninterrupted workflow.**

**Q: Does AI Background Removal require internet?**
A: No. It runs locally using ONNX Runtime. Internet is only required to download the runtime libraries during the initial setup (if using the Lite installer).

**Q: What formats are supported?**
A: Supports major formats including JPG, PNG, BMP, WEBP, ICO, GIF (View & Play), HEIC, and TIF.

---

## 📄 License & Contact

This project is open-source under the **MIT License**.
Powered by: `MicaWPF`, `SkiaSharp`, `XamlAnimatedGif`, `OnnxRuntime`, `WriteableBitmapEx`.

*   **Feedback**: Please submit [Issues](https://github.com/zouxiaofei1/TabPaint/issues) 