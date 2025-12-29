using System;
using System.Text.Json.Serialization;
using System.Windows.Media;

namespace TabPaint
{
    // 定义配置的数据模型
    public class AppSettings
    {

        #region 未实装设置区域
        // === 常规设置 ===

        [JsonPropertyName("theme_mode")]
        public string ThemeMode { get; set; } = "System"; // System, Light, Dark

        [JsonPropertyName("language")]
        public string Language { get; set; } = "en-US";

        [JsonPropertyName("auto_check_updates")]
        public bool AutoCheckUpdates { get; set; } = true;

        // === 绘图行为 ===

        [JsonPropertyName("default_brush_color")]
        public string DefaultBrushColorHex { get; set; } = "#FF000000"; // 默认黑色

        [JsonPropertyName("default_brush_size")]
        public double DefaultBrushSize { get; set; } = 5.0;

        [JsonPropertyName("enable_autosave")]
        public bool EnableAutoSave { get; set; } = true;

        [JsonPropertyName("autosave_interval_seconds")]
        public int AutoSaveIntervalSeconds { get; set; } = 3; // 对应待办事项16

        // === 界面/UX ===

        [JsonPropertyName("show_transparent_grid")]
        public bool ShowTransparentGrid { get; set; } = true; // 灰白格子

        [JsonPropertyName("remember_clipboard_monitor")]
        public bool RememberClipboardMonitor { get; set; } = false; // 对应待办事项20

        // === 快捷键 (预留) ===
        // 后续可以用 Dictionary<string, Key> 来存储
        #endregion

    }
}
