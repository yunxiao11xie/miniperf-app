using System;
using System.IO;
using System.Xml.Serialization;

namespace MiniPerfApp
{
    public class AppSettings
    {
        public double BarX { get; set; }
        public double BarY { get; set; }
        public double Opacity { get; set; }
        public bool Topmost { get; set; }
        public bool FirstRun { get; set; }

        public AppSettings()
        {
            BarX = -1;
            BarY = -1;
            Opacity = 0.9;
            Topmost = true;
            FirstRun = true;
        }
    }

    public static class SettingsStore
    {
        private static string Dir
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MiniPerfApp"); }
        }
        private static string File_
        {
            get { return Path.Combine(Dir, "settings.xml"); }
        }

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(File_))
                {
                    XmlSerializer s = new XmlSerializer(typeof(AppSettings));
                    using (FileStream fs = File.OpenRead(File_))
                    {
                        return (AppSettings)s.Deserialize(fs);
                    }
                }
            }
            catch { }
            return new AppSettings();
        }

        public static void Save(AppSettings s)
        {
            try
            {
                Directory.CreateDirectory(Dir);
                XmlSerializer x = new XmlSerializer(typeof(AppSettings));
                using (StreamWriter sw = new StreamWriter(File_))
                {
                    x.Serialize(sw, s);
                }
            }
            catch { }
        }
    }
}
