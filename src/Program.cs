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
                return; // already running; segment 4 will add "bring to front"
            }
            AppSettings settings = SettingsStore.Load();
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(OnUnhandled);
            MetricsCollector collector = new MetricsCollector();
            collector.Start();
            MiniBarWindow bar = new MiniBarWindow(collector, settings);
            System.Windows.Threading.Dispatcher d = bar.Dispatcher;
            d.UnhandledException += new System.Windows.Threading.DispatcherUnhandledExceptionEventHandler(OnDispatcher);
            Application app = new Application();
            app.ShutdownMode = ShutdownMode.OnMainWindowClose;
            Log.Write("startup ok");
            try
            {
                app.Run(bar);
            }
            finally
            {
                Log.Write("shutdown");
                collector.Stop();
                SettingsStore.Save(settings);
                GC.KeepAlive(mutex);
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
