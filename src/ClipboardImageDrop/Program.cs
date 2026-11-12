using System;
using System.Threading;
using System.Windows.Forms;

namespace ClipboardImageDrop;

internal static class Program
{
    private static Mutex? _mutex;

    [STAThread]
    private static void Main()
    {
        const string mutexName = "ClipboardImageDrop_SingleInstance_Mutex";
        _mutex = new Mutex(true, mutexName, out bool createdNew);

        if (!createdNew)
        {
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApplicationContext());

        GC.KeepAlive(_mutex);
    }
}
