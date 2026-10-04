using System;
using System.Collections.Generic;
namespace OrbitWheelLite
{
    public class ActionItem
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string Target { get; set; }
    }

    public class WheelPage
    {
        public string Name { get; set; }
        public List<ActionItem> Actions { get; set; }
    }

    public class AppConfig
    {
        public int Modifiers { get; set; }
        public int KeyCode { get; set; }
        public string Mode { get; set; }
        public string Style { get; set; }
        public bool StartWithWindows { get; set; }
        public bool MouseGestures { get; set; }
        public List<WheelPage> Pages { get; set; }

        public static AppConfig Default()
        {
            return new AppConfig {
                Modifiers = 2,
                KeyCode = 32,
                Mode = "Hold",
                Style = "液态玻璃",
                StartWithWindows = false,
                MouseGestures = false,
                Pages = new List<WheelPage> {
                    new WheelPage {
                        Name = "常用",
                        Actions = new List<ActionItem> {
                            A("资源管理器", "Explorer", ""),
                            A("设置", "Settings", ""),
                            A("锁定", "Lock", ""),
                            A("音量 +", "VolumeUp", ""),
                            A("音量 -", "VolumeDown", ""),
                            A("睡眠", "Sleep", "")
                        }
                    }
                }
            };
        }

        private static ActionItem A(string name, string type, string target)
        {
            return new ActionItem { Name = name, Type = type, Target = target };
        }
    }

}
