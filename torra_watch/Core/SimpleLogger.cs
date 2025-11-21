using System.Collections.Concurrent;

namespace torra_watch.Core
{
    internal class SimpleLogger
    {
        private static readonly BlockingCollection<string> _queue = new();
        private static readonly Thread _writerThread;
        private static volatile bool _initialized;

        //static Log()
        //{
        //    _writerThread = new Thread(WriterLoop) { IsBackground = true, Name = "LogWriter" };
        //    _writerThread.Start();
        //    _initialized = true;

        //    return null;
        //}

        public static void Info(string msg) => Enqueue("INFO", msg);
        public static void Warn(string msg) => Enqueue("WARN", msg);
        public static void Error(string msg) => Enqueue("ERROR", msg);

        private static void Enqueue(string level, string msg)
        {
            if (!_initialized) return;
            var line = $"{DateTime.UtcNow:O} {level} {msg}";
            _queue.Add(line);
            System.Diagnostics.Debug.WriteLine(line);
        }

        private static string LogFilePath()
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TorraWatch", "logs");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            var name = $"torrawatch-{DateTime.UtcNow:yyyyMMdd}.log";
            return Path.Combine(dir, name);
        }

        private static void WriterLoop()
        {
            var path = LogFilePath();
            using var sw = new StreamWriter(new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite)) { AutoFlush = true };
            foreach (var line in _queue.GetConsumingEnumerable())
            {
                var current = LogFilePath();
                if (!string.Equals(current, path, StringComparison.OrdinalIgnoreCase))
                {
                    sw.Flush();
                    path = current;
                }
                sw.WriteLine(line);
            }
        }
    }
}
