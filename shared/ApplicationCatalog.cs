using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
namespace OrbitWheelLite
{
    class ApplicationChoice
    {
        public string Name { get; set; }
        public string Target { get; set; }
        public string DisplayLabel { get { return Name + " — " + Target; } }
        public override string ToString() { return Name; }
    }

    static class ApplicationCatalog
    {
        private static string ResolveAppsFolderPath(string path)
        {
            if (String.IsNullOrWhiteSpace(path)) return path;
            Dictionary<string, string> roots = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                {"{6D809377-6AF0-444B-8957-A3773F02200E}", Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)},
                {"{7C5A40EF-A0FB-4BFC-874A-C0F2E0B9FA8E}", Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)},
                {"{1AC14E77-02E7-4E5D-B744-2EB1AE5198B7}", Environment.GetFolderPath(Environment.SpecialFolder.System)},
                {"{D65231B0-B2F1-4857-A4CE-A8E7C6EA7D27}", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SysWOW64")},
                {"{F38BF404-1D43-42F2-9305-67DE0B28FC23}", Environment.GetFolderPath(Environment.SpecialFolder.Windows)}
            };
            foreach (KeyValuePair<string,string> root in roots) {
                if (path.StartsWith(root.Key + "\\", StringComparison.OrdinalIgnoreCase)) {
                    string candidate = Path.Combine(root.Value, path.Substring(root.Key.Length + 1));
                    if (File.Exists(candidate)) return candidate;
                }
            }
            return path;
        }

        public static System.Threading.Tasks.Task<List<ApplicationChoice>> LoadAsync()
        {
            var completion = new System.Threading.Tasks.TaskCompletionSource<List<ApplicationChoice>>();
            var thread = new System.Threading.Thread(delegate() {
                try { completion.SetResult(LoadCore()); } catch (Exception error) { completion.SetException(error); }
            });
            thread.IsBackground = true; thread.SetApartmentState(System.Threading.ApartmentState.STA); thread.Start();
            return completion.Task;
        }
        public static List<ApplicationChoice> Load()
        {
            try { return LoadCore(); } catch { return new List<ApplicationChoice>(); }
        }
        private static List<ApplicationChoice> LoadCore()
        {
            List<ApplicationChoice> result = new List<ApplicationChoice>();
            {
                Type shellType = Type.GetTypeFromProgID("Shell.Application");
                object shell = Activator.CreateInstance(shellType);
                object folder = shellType.InvokeMember("NameSpace", BindingFlags.InvokeMethod, null, shell, new object[] { "shell:AppsFolder" });
                object items = folder.GetType().InvokeMember("Items", BindingFlags.InvokeMethod, null, folder, null);
                int count = Convert.ToInt32(items.GetType().InvokeMember("Count", BindingFlags.GetProperty, null, items, null));
                for (int i = 0; i < count; i++) {
                    object item = items.GetType().InvokeMember("Item", BindingFlags.InvokeMethod, null, items, new object[] { i });
                    string name = Convert.ToString(item.GetType().InvokeMember("Name", BindingFlags.GetProperty, null, item, null));
                    string path = Convert.ToString(item.GetType().InvokeMember("Path", BindingFlags.GetProperty, null, item, null));
                    path = ResolveAppsFolderPath(path);
                    if (!String.IsNullOrWhiteSpace(name) && !String.IsNullOrWhiteSpace(path))
                        result.Add(new ApplicationChoice { Name = name, Target = File.Exists(path) ? path : path.StartsWith("shell:", StringComparison.OrdinalIgnoreCase) ? path : "shell:AppsFolder\\" + path });
                }
            }
            result.Sort(delegate(ApplicationChoice a, ApplicationChoice b) { return String.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase); });
            return result;
        }
    }

}
