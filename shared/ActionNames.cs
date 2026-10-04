using System;
using System.Collections.Generic;
namespace OrbitWheelLite
{
    static class ActionNames
    {
        private static readonly Dictionary<string, string> Names = new Dictionary<string, string> {
            {"None","无操作"}, {"App","打开程序"}, {"Folder","打开文件夹"}, {"Command","执行命令"},
            {"Explorer","打开资源管理器"}, {"Settings","打开 OrbitWheel 设置"},
            {"Lock","锁定电脑"}, {"Sleep","进入睡眠"}, {"Shutdown","关闭电脑"},
            {"Restart","重新启动"}, {"VolumeUp","增大音量"}, {"VolumeDown","减小音量"},
            {"Mute","静音 / 取消静音"}
        };
        public static string Chinese(string id) { return Names.ContainsKey(id) ? Names[id] : "无操作"; }
        public static string Id(string chinese)
        {
            foreach (KeyValuePair<string,string> pair in Names) if (pair.Value == chinese) return pair.Key;
            return "None";
        }
        public static object[] AllChinese()
        {
            List<object> result = new List<object>();
            foreach (string id in new string[] { "None","App","Folder","Command","Explorer","Settings","Lock","Sleep","Shutdown","Restart","VolumeUp","VolumeDown","Mute" })
                result.Add(Chinese(id));
            return result.ToArray();
        }
    }

}
