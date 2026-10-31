using System;
using System.IO;

namespace MiniPerfApp
{
    public static class Log
    {
        private static readonly object _lock = new object();
        private static string Path_
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MiniPerfApp", "log.txt"); }
        }
        public static void Write(string msg)
        {
            try
            {
                lock (_lock)
                {
                    Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path_));
                    File.AppendAllText(Path_, DateTime.Now.ToString("HH:mm:ss.fff ") + msg + "\r\n");
                }
            }
            catch { }
        }
    }
}
