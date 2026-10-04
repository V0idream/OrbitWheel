using System;
using System.Diagnostics;
using System.IO;
#if NET
using System.Text.Json;
#else
using System.Web.Script.Serialization;
#endif
namespace OrbitWheelLite
{
    public class RuntimeReport
    {
        public int ProcessId { get; set; }
        public long Started { get; set; }
        public long Updated { get; set; }
        public string Revision { get; set; }
        public bool Passive { get; set; }
        public string Hotkey { get; set; }
        public string Startup { get; set; }
        public string Gestures { get; set; }
    }
    static class RuntimeState
    {
        private static string PathFor(string name) { return Path.Combine(ConfigStore.Folder, name + ".json"); }
        public static RuntimeReport Identity()
        {
            using (Process process = Process.GetCurrentProcess()) return new RuntimeReport {
                ProcessId = process.Id, Started = process.StartTime.ToUniversalTime().Ticks, Updated = DateTime.UtcNow.Ticks
            };
        }
        public static void Write(string name, RuntimeReport report)
        {
            report.Updated = DateTime.UtcNow.Ticks;
#if NET
            string text = JsonSerializer.Serialize(report);
#else
            string text = new JavaScriptSerializer().Serialize(report);
#endif
            string path = PathFor(name), temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try {
                Directory.CreateDirectory(ConfigStore.Folder);
                File.WriteAllText(temporary, text);
                if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
            } finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        public static RuntimeReport Read(string name)
        {
            try {
                string text;
                using (var stream = new FileStream(PathFor(name), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var reader = new StreamReader(stream)) text = reader.ReadToEnd();
#if NET
                RuntimeReport report = JsonSerializer.Deserialize<RuntimeReport>(text);
#else
                RuntimeReport report = new JavaScriptSerializer().Deserialize<RuntimeReport>(text);
#endif
                long age = DateTime.UtcNow.Ticks - report.Updated;
                if (age < 0 || age > TimeSpan.FromSeconds(3).Ticks) return null;
                using (Process process = Process.GetProcessById(report.ProcessId)) {
                    if (process.HasExited || process.StartTime.ToUniversalTime().Ticks != report.Started) return null;
                    string expected = name == "recording" ? "OrbitWheel.Settings" : "OrbitWheel";
                    if (!report.Passive && process.ProcessName != expected) return null;
                }
                return report;
            } catch { return null; }
        }
        public static void EndRecording() { try { File.Delete(PathFor("recording")); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
    }
}
