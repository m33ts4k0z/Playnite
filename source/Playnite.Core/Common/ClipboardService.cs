using System;

namespace Playnite.Common
{
    // Clipboard access hook: core code can set clipboard text without
    // referencing a UI framework; the host app supplies the implementation
    // (WPF Clipboard today, Avalonia clipboard later).
    public static class ClipboardService
    {
        public static Action<string> SetText { get; set; } = _ => { };
    }
}
