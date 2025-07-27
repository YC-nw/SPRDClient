using System.Diagnostics;
using System.Windows;
using System.Threading;
using System.Threading.Channels;
using System.Runtime.InteropServices;
using System.IO;

namespace SPRDClient
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            MainWindow m = new MainWindow();

            if (e.Args.Length > 0 && e.Args[0] == "debug")
            {
                [DllImport("kernel32.dll")]
                static extern bool AllocConsole();
                [DllImport("kernel32.dll")]
                static extern bool FreeConsole();
                AllocConsole();
                Console.WriteLine("已进入Debug模式.");
                if (e.Args.Length > 1 && e.Args[1] == "--highlevel")
                {
                    var channel = Channel.CreateBounded<string>(new BoundedChannelOptions(20)
                    {
                        SingleReader = true,
                        SingleWriter = true,
                        FullMode = BoundedChannelFullMode.Wait
                    });
                    Directory.CreateDirectory("log");
                    FileStream fsn = File.Create($".\\log\\normal-debug-{DateTime.Now:yyyy.MM.dd-hh-mm-ss}.txt");
                    FileStream fsh = File.Create($".\\log\\highlevel-debug-{DateTime.Now:yyyy.MM.dd-hh-mm-ss}.txt");

                    StreamWriter swn = new StreamWriter(fsn) {AutoFlush = true };
                    StreamWriter swh = new StreamWriter(fsh) { AutoFlush = true };

                    Console.WriteLine($"常规日志已开始记录至.\\log\\normal-debug-{DateTime.Now:yyyy.MM.dd-hh-mm-ss}.txt.");
                    Console.WriteLine($"高级发包日志已开始记录至.\\log\\highlevel-debug-{DateTime.Now:yyyy.MM.dd-hh-mm-ss}.txt.");

                    _ = Task.Run(async () =>
                    {
                        await foreach (var message in channel.Reader.ReadAllAsync())
                        {
                            swh.WriteLine(message);
                        }
                    });

                    m.sprdFlashUtils.Log += swn.WriteLine;
                    m.sprdFlashUtils.Handler.Log += log => channel.Writer.TryWrite(log);
                    m.sprdFlashUtils.Handler.Verbose = true;
                }
                m.TitleBar1.Title = "SPRDClient - Debug Mode";
                m.sprdFlashUtils.Log += Console.WriteLine;
                Current.Exit += (object sender, ExitEventArgs e) => FreeConsole();
            }
            m.Show();
        }


    }

}
