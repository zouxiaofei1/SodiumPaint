# 🎨 TabPaint (Alpha)

![Platform](https://img.shields.io/badge/Platform-Windows%2010%2F11-blue) ![Language](https://img.shields.io/badge/Language-C%23%20%7C%20WPF-purple) ![Status](https://img.shields.io/badge/Status-Alpha%20v0.8.6-orange) ![License](https://img.shields.io/badge/license-MIT-green)

![App Screenshot](./TabPaint/Resources/screenshot.png)

> **English** | [中文](#chinese)

---

## 🇬🇧 English Introduction

**TabPaint** is a lightweight image editor and viewer tailored for Windows 10/11, built with C#, WPF (.NET), and Win32 APIs (Mica/DWM).

It fits perfectly into the **"10-second edit" workflow**: ideal for when you need to screenshot, annotate, crop, and paste into a document instantly. It bridges the gap between a simple image viewer and an editor, combining the familiarity of MS Paint with **browser-style tabs**, seamless drag-and-drop integration, and advanced features like AI cutout.

### 🚧 Alpha Warning (v0.8.6)
**Current Status: Active Development**
This software is in **Alpha Testing**. 
*   ⚠️ **Stability**: v0.8.6 introduces significant architectural changes (View/Edit modes, Dark Mode, etc.). While heavily tested, edge cases may occur.
*   **Recommendation**: Excellent for quick viewing, marking, and format conversion. Frequent saving is recommended for complex edits.

### ✨ Key Features (v0.8.x Updates)
*   **Dual Mode Architecture**:
    *   **Viewer Mode**: Clean, immersive interface for browsing images and playing GIFs. Supports EXIF data display.
    *   **Editor Mode**: Full suite of editing tools. Toggle instantly with `Tab`.
*   **Advanced AI & Tools**:
    *   **AI Cutout**: Remove backgrounds instantly (Offline ONNX Runtime).
    *   **OCR**: Extract text from images using Windows Media OCR.
    *   **Smart Tools**: Color difference cutout, screen color picker, and auto-trim whitespace.
*   **Enhanced UI/UX**:
    *   **Dark Mode**: Full system-aware Dark/Light theme support.
    *   **Responsive Toolbar**: Tools adapt to window size; improved icons and cursor feedback.
    *   **Visual Upgrades**: Canvas shadows, animated selection borders (marching ants), and rulers.
*   **Performance**:
    *   Optimized for high-resolution images (4K/8K+).
    *   Faster startup (<200ms) and smoother zooming logic.

### 🗺️ Roadmap

| Feature | Status | Note |
| :--- | :---: | :--- |
| **Viewer/Editor Split** | ✅ | Completed in v0.8. Immersive viewing experience. |
| **Dark Mode** | ✅ | Fully implemented in v0.8.6. |
| **AI Integration** | ✅ | Background removal and OCR added. |
| **GIF Support** | ✅ | Playback support added (Edit support pending). |
| **Plugin System** | 📅 | Future Goal: Allow external tools integration. |
| **Vector Layers** | 📅 | Future Goal: Re-editable text and shapes. |

---
<a name="chinese"></a>

## 🇨🇳 中文介绍

**TabPaint** 是一款基于 C# WPF 和 Win32 API 开发的现代化 Windows 图片编辑与查看工具，采用 Win11 风格的无边框 Mica 特效窗口。

它的定位介于“看图软件”和“专业绘图软件”之间，专为 **“10秒内快速修图”** 场景设计：截图 -> 标注 -> 裁剪 -> 拖拽发送。v0.8 版本带来了革命性的**看图/绘图模式分离**和**暗黑模式**支持。

### 🚧 Alpha 版本预警 (v0.8.6)
**当前状态：活跃开发中**
本项目目前处于 **Alpha 内测阶段**。
*   ⚠️ **稳定性**：v0.8 系列进行了大量底层重构（包括渲染模式和内存管理）。虽然修复了数百个 Bug，但请对重要文件保持备份习惯。
*   **建议**：完全可以替代系统自带的照片查看器和画图工具。

### ✨ v0.8 核心更新亮点
*   **看图与绘图模式分离**：
    *   **看图模式**：沉浸式体验，支持 GIF 播放，EXIF 信息查看，滚轮缩放/切图丝般顺滑。
    *   **绘图模式**：一键 `Tab` 切换，工具栏自动展开，专注于创作。
*   **AI 与智能工具**：
    *   **一键抠图**：集成 ONNX Runtime，支持离线 AI 智能移除背景。
    *   **OCR 文字识别**：调用 Windows 原生 API，支持选区截图识字。
    *   **实用工具箱**：屏幕取色器（带放大镜）、色差抠图、反色、自动色阶、智能裁切空白。
*   **视觉与交互升级**：
    *   **深色模式**：完整支持跟随系统的深色/浅色主题切换。
    *   **界面优化**：新增标尺、画布阴影、蚂蚁线选区动画、响应式工具栏。
    *   **文件支持**：新增 WebP 保存支持，优化 HEIC/TIFF 查看体验。
*   **性能飞跃**：
    *   启动速度优化至 <200ms。
    *   针对 4K/8K 超大分辨率图片的加载与渲染进行了深度优化。

### 📜 最近更新 (Changelog)

<details open>
<summary><b>v0.8.6 (Latest Stable)</b></summary>

*   **新增**：完整支持 Dark Mode (深色模式)，图标与主题实时响应系统设置。
*   **新增**：响应式工具栏 (Responsive Toolbar)，根据窗口宽度自动折叠/展开工具。
*   **优化**：全面规范化鼠标指针样式 (画笔、拖拽、文本工具等)。
*   **兼容性**：优化与 Snipaste 等截图工具的剪贴板交互。
*   **修复**：修复了 ImageBar 在大图快速滚动时发白的问题。
*   **修复**：修复了未命名文件撤销重做逻辑导致的覆盖 Bug。
</details>

<details>
<summary>点击展开 v0.8.0 - v0.8.5 详细更新日志</summary>

**v0.8.5**
*   **UI**：新增选区“蚂蚁线”动画效果，画布边缘增加阴影与灰色边框。
*   **功能**：支持粘贴文字直接转换为可编辑文本框；支持 Shift 等比例缩放。
*   **修复**：大图文件夹加载机制优化，修复缩略图点击无响应问题。
*   **修复**：Ctrl+A 全选逻辑修正，修复概率性全白 Bug。

**v0.8.4**
*   **新增**：画图模式支持 WebP 格式保存。
*   **新增**：ImageBar 拖拽跳转功能，支持触控板手势操作。
*   **优化**：画布调整大小逻辑重构，支持数值输入。
*   **修复**：修复了 GIF 在画图模式下误播放的问题。

**v0.8.3**
*   **新增**：标尺工具；支持 GIF 播放 (看图模式)。
*   **新增**：EXIF 信息显示面板。
*   **新增**：文件删除功能 (Del 键删除至回收站，支持撤销)。
*   **性能**：内存管理优化，解决切换图片内存占用过高问题。

**v0.8.2**
*   **新增**：屏幕取色器 (带放大镜)、自动色阶、反色功能。
*   **新增**：设置中心重构 (通用/画图/看图/快捷键/高级)。
*   **优化**：Shape 工具与 Selection 工具的撤销逻辑分离。
*   **修复**：修复了透明图片拖拽产生白底的 Bug。

**v0.8.1**
*   **重磅**：新增 AI 一键抠图 (ONNX Runtime)。
*   **重磅**：新增 OCR 文字识别与色差抠图。
*   **优化**：支持 ICO, HEIC, TIF 格式查看。
*   **修复**：高 DPI 下选区错位及画布遮罩闪烁问题。

**v0.8.0 (Major Update)**
*   **架构**：实现看图模式与画图模式的分离。
*   **交互**：隐藏非必要 UI 元素，实现沉浸式看图。
*   **性能**：启动速度大幅优化，加入大图加载进度条。
*   **操作**：新增 `Ctrl+L/R` 旋转，双击全屏。
</details>

### 🐛 已知问题
*   **超大图编辑**：虽然性能已优化，但编辑 16K+ 分辨率图片时，部分滤镜操作可能仍有延迟。
*   **GIF 编辑**：目前仅支持 GIF 播放，编辑后保存为 GIF 只能保存第一帧（建议保存为 APNG 或 WebP 计划中）。

---

### 📥 Download / 下载
Please check the [Releases](../../releases) page for the latest build.
请前往 [Releases](../../releases) 页面下载最新构建版本。
