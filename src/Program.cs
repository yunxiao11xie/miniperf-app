using System;
using System.Threading;
using System.Windows;

namespace MiniPerfApp
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            bool createdNew;
            Mutex mutex = new Mutex(true, "MiniPerfApp_SingleInstance", out createdNew);
            if (!createdNew)
            {
                // second launch: signal the running instance to show itself
                try
                {
                    EventWaitHandle ev = EventWaitHandle.OpenExisting("MiniPerfApp_ShowEvent");
                    ev.Set();
                }
                catch { }
                return;
            }
            EventWaitHandle showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "MiniPerfApp_ShowEvent");
            AppSettings settings = SettingsStore.Load();
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(OnUnhandled);
            MetricsCollector collector = new MetricsCollector();
            collector.Start();
            TopProcesses.Start();
            MiniBarWindow bar = new MiniBarWindow(collector, settings);
            System.Windows.Threading.Dispatcher d = bar.Dispatcher;
            d.UnhandledException += new System.Windows.Threading.DispatcherUnhandledExceptionEventHandler(OnDispatcher);
            Application app = new Application();
            app.ShutdownMode = ShutdownMode.OnMainWindowClose;
            Log.Write("startup ok");
            CreateDesktopShortcutIfNeeded(settings);
            System.Threading.Thread signalThread = new System.Threading.Thread(new ParameterizedThreadStart(WaitShowSignal));
            signalThread.IsBackground = true;
            signalThread.Start(new object[] { showEvent, bar });
            try
            {
                app.Run(bar);
            }
            finally
            {
                Log.Write("shutdown");
                TopProcesses.Stop();
                collector.Stop();
                SettingsStore.Save(settings);
                GC.KeepAlive(mutex);
            }
        }

        private static void WaitShowSignal(object state)
        {
            object[] arr = (object[])state;
            EventWaitHandle ev = (EventWaitHandle)arr[0];
            MiniBarWindow bar = (MiniBarWindow)arr[1];
            while (true)
            {
                try { if (!ev.WaitOne(2000)) continue; } catch { return; }
                bar.Dispatcher.BeginInvoke(new Action(delegate
                {
                    try
                    {
                        bar.Show();
                        bar.FocusBar();
                        if (bar.Panel != null && !bar.Panel.IsPanelVisible)
                        {
                            bar.Panel.ShowBelow(bar.Left, bar.Top + bar.Height + 6);
                        }
                        Log.Write("second-launch signal: shown");
                    }
                    catch (Exception ex) { Log.Write("signal show err: " + ex.Message); }
                }));
            }
        }

        private static void CreateDesktopShortcutIfNeeded(AppSettings settings)
        {
            try
            {
                if (!settings.FirstRun) return;
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string lnk = System.IO.Path.Combine(desktop, "MiniPerf.lnk");
                if (System.IO.File.Exists(lnk)) { settings.FirstRun = false; return; }
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                object shell = System.Activator.CreateInstance(shellType);
                object sc = shellType.InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, shell, new object[] { lnk });
                Type scType = sc.GetType();
                string exe = System.Reflection.Assembly.GetExecutingAssembly().Location;
                scType.InvokeMember("TargetPath", System.Reflection.BindingFlags.SetProperty, null, sc, new object[] { exe });
                scType.InvokeMember("WorkingDirectory", System.Reflection.BindingFlags.SetProperty, null, sc, new object[] { System.IO.Path.GetDirectoryName(exe) });
                scType.InvokeMember("Description", System.Reflection.BindingFlags.SetProperty, null, sc, new object[] { "MiniPerf hardware monitor" });
                scType.InvokeMember("Save", System.Reflection.BindingFlags.InvokeMethod, null, sc, null);
                settings.FirstRun = false;
                Log.Write("desktop shortcut created");
            }
            catch (Exception ex)
            {
                Log.Write("shortcut err: " + ex.Message);
            }
        }

        private static void OnUnhandled(object sender, UnhandledExceptionEventArgs e)
        {
            Log.Write("UNHANDLED: " + e.ExceptionObject);
        }

        private static void OnDispatcher(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            Log.Write("DISPATCHER: " + e.Exception);
            e.Handled = true;
        }
    }
}
