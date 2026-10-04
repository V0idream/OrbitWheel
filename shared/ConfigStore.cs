using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
#if NET
using System.Text.Json;
#else
using System.Web.Script.Serialization;
#endif

namespace OrbitWheelLite
{
    // This exact source is compiled into both the .NET Framework host and WinUI.
    static class ConfigStore
    {
        public static readonly string Folder = Environment.GetEnvironmentVariable("ORBITWHEEL_CONFIG_DIR") ??
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OrbitWheel");
        public static readonly string FilePath = Path.Combine(Folder, "config.json");
        internal static Exception LastReadError { get; private set; }

        public static AppConfig Load()
        {
            AppConfig config; string revision;
            if (TryLoad(out config, out revision)) return config;
            if (File.Exists(FilePath)) throw new InvalidDataException("配置无法读取，原文件已保留：" + FilePath);
            config = AppConfig.Default();
            if (!TrySave(config, "", out revision)) return Load();
            return config;
        }

        public static bool TryLoad(out AppConfig config, out string revision)
        {
            // Writers replace the file atomically. An open handle pins either the
            // old or new complete file, so readers need no writer mutex/timeout.
            for (int attempt = 0; ; attempt++) {
                if (ReadSnapshot(out config, out revision)) return true;
                // Windows may briefly reject a new open while ReplaceFile is
                // completing. Retry only I/O failures, never malformed JSON.
                if (!(LastReadError is IOException) || attempt == 9) return false;
                Thread.Sleep(10);
            }
        }

        private static bool ReadSnapshot(out AppConfig config, out string revision)
        {
            config = null; revision = "";
            LastReadError = null;
            try {
                byte[] data;
                using (FileStream stream = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (MemoryStream bytes = new MemoryStream()) { stream.CopyTo(bytes); data = bytes.ToArray(); }
                string text = Encoding.UTF8.GetString(data).TrimStart('\uFEFF');
#if NET
                config = JsonSerializer.Deserialize<AppConfig>(text);
#else
                config = new JavaScriptSerializer().Deserialize<AppConfig>(text);
#endif
                Normalize(config);
                revision = Hash(data);
                return true;
            } catch (Exception error) {
                if (error is OutOfMemoryException || error is StackOverflowException) throw;
                LastReadError = error;
                config = null; return false;
            }
        }

        public static void Save(AppConfig config)
        {
            string revision;
            if (!TrySave(config, null, out revision)) throw new IOException("无法保存配置。");
        }

        // null permits an unconditional save (legacy fixtures); production
        // editors pass the revision they loaded, and never silently overwrite.
        public static bool TrySave(AppConfig config, string expectedRevision, out string revision)
        {
            Normalize(config);
#if NET
            string text = JsonSerializer.Serialize(config);
#else
            string text = new JavaScriptSerializer().Serialize(config);
#endif
            byte[] data = new UTF8Encoding(false).GetBytes(text);
            revision = "";
            string identity = Hash(Encoding.UTF8.GetBytes(Path.GetFullPath(FilePath).ToUpperInvariant()));
            using (Mutex mutex = new Mutex(false, "Local\\OrbitWheel.Config." + identity)) {
                bool owned = false;
                string temporary = null;
                try {
                    try { owned = mutex.WaitOne(5000); } catch (AbandonedMutexException) { owned = true; }
                    if (!owned) throw new IOException("配置正由另一个进程保存，请重试。");
                    AppConfig current; string currentRevision;
                    bool valid = TryLoad(out current, out currentRevision);
                    if (expectedRevision != null && ((!valid && File.Exists(FilePath)) || currentRevision != expectedRevision))
                        return false;
                    Directory.CreateDirectory(Folder);
                    temporary = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
                    using (FileStream stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None)) {
                        stream.Write(data, 0, data.Length); stream.Flush(true);
                    }
                    if (File.Exists(FilePath)) File.Replace(temporary, FilePath, null);
                    else File.Move(temporary, FilePath);
                    temporary = null;
                    revision = Hash(data);
                    return true;
                } finally {
                    if (temporary != null && File.Exists(temporary)) File.Delete(temporary);
                    if (owned) mutex.ReleaseMutex();
                }
            }
        }

        private static string Hash(byte[] data)
        {
            using (SHA256 hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(data)).Replace("-", "");
        }

        private static void Normalize(AppConfig config)
        {
            if (config == null) throw new InvalidDataException("配置为空。");
            if (config.Pages == null || config.Pages.Count == 0) config.Pages = AppConfig.Default().Pages;
            foreach (WheelPage page in config.Pages) {
                if (page == null) throw new InvalidDataException("页面为空。");
                if (page.Actions == null) page.Actions = new System.Collections.Generic.List<ActionItem>();
                while (page.Actions.Count < 6) page.Actions.Add(new ActionItem { Name = "空", Type = "None", Target = "" });
                if (page.Actions.Count > 6) page.Actions.RemoveRange(6, page.Actions.Count - 6);
                foreach (ActionItem action in page.Actions) if (action == null) throw new InvalidDataException("动作为空。");
                if (page.Name == null) page.Name = "未命名页面";
            }
            if (String.IsNullOrEmpty(config.Mode)) config.Mode = "Hold";
            if (String.IsNullOrEmpty(config.Style)) config.Style = "液态玻璃";
        }
    }
}
