using System.Threading.Channels;
using System.Windows.Controls;
using System.Windows.Threading;

namespace SPRDClient.Pages
{
    /// <summary>
    /// LogPage.xaml 的交互逻辑
    /// </summary>
    public partial class LogPage : Page
    {
        private readonly Channel<string> _packetLogChannel;
        private readonly Channel<string> _commonLogChannel;
        public LogPage()
        {
            InitializeComponent();
            _packetLogChannel = Channel.CreateBounded<string>(new BoundedChannelOptions(200)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.DropOldest
            });
            _commonLogChannel = Channel.CreateBounded<string>(new BoundedChannelOptions(200)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.DropOldest
            });
            Task.Run(PacketConsumeLogAsync);
            Task.Run(CommonConsumeLogAsync);
        }

        public void PacketLog(string message)
        {
            _packetLogChannel.Writer.TryWrite(message);
        }
        public void CommonLog(string message)
        {
            _commonLogChannel.Writer.TryWrite(message);
        }
        private async Task PacketConsumeLogAsync()
        {
            await foreach (var message in _packetLogChannel.Reader.ReadAllAsync())
            {
                await PacketTextLog.Dispatcher.BeginInvoke(() =>
                {
                    if (PacketTextLog.Text.Length >= 3000)
                        PacketTextLog.Clear();

                    PacketTextLog.AppendText($"{DateTime.Now.ToString("yyyy/MM/dd:HH:mm:ss")} : {message}{Environment.NewLine}");
                    PacketTextLog.ScrollToEnd();
                }, DispatcherPriority.ContextIdle);
            }
        }
        private async Task CommonConsumeLogAsync()
        {
            await foreach (var message in _commonLogChannel.Reader.ReadAllAsync())
            {
                await CommonTextLog.Dispatcher.BeginInvoke(() =>
                {
                    if (CommonTextLog.Text.Length >= 1000) CommonTextLog.Clear();
                    CommonTextLog.AppendText($"{DateTime.Now.ToString("yyyy/MM/dd:HH:mm:ss")} : {message}{Environment.NewLine}");
                    CommonTextLog.ScrollToEnd();
                }, DispatcherPriority.ContextIdle);
            }
        }
    }
}
