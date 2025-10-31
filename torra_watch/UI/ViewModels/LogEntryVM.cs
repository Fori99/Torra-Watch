using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace torra_watch.UI.ViewModels
{
    public enum LogLevel
    {
        Debug = 0,
        Info = 1,
        Success = 2,
        Warning = 3,
        Error = 4
    }

    // Plain data model — no UI methods here
    public sealed class LogEntryVM
    {
        // Use a single name for time; most of your code used "Timestamp"
        public DateTime Timestamp { get; set; } = DateTime.Now;   // or DateTime.UtcNow if you prefer
        public LogLevel Level { get; set; } = LogLevel.Info;
        public string Message { get; set; } = "";
        public string? Source { get; set; }   // optional: where it came from
    }
}

