using SPRDClient.Utils;
using System.IO;
using System.Windows;
using System.Xml.Serialization;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;

namespace SPRDClient
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 

    public partial class MainWindow
    {
        public SprdFlashUtils sprdFlashUtils;
        SprdProtocolHandler sprdProtocolHandler;
        const string configPath = "fdlToSend.xml";
        ExitEventHandler exitEventHandler;
        ComPortMonitor? comPortMonitor;
        Config config;
        SnackbarService snackbarService = new SnackbarService();
        public string StrAddress { get; set; } = "";
        public string FdlFilePath { get; set; } = "";
        public bool AutoSendNextTime { get; set; }
        public bool EnableReconnectMode { get; set; } = false;
        public bool KickToAutodloader
        {
            get => field; set
            {
                field = value;
                if (value)
                {
                    AnimationControl.StartFadeInAnimation(KickModeToggle);
                    snackbarService.Show("警告", "此功能在某些设备上无法正常工作\n且在某些设备上会使splloader分区被擦除\n请谨慎使用！", ControlAppearance.Caution, new TimeSpan(0, 0, 3));
                }
                else AnimationControl.StartFadeOutAnimation(KickModeToggle);
            }
        } = false;
        public bool boolKickMode { get => field; set { field = value; KickMode = value ? ModeOfChangingDiagnostic.CustomOneTimeMode : ModeOfChangingDiagnostic.CommonMode; } }
        private ModeOfChangingDiagnostic KickMode = ModeOfChangingDiagnostic.CommonMode;
        public bool AutoSend
        {
            get; set;
        }
        public string ExecAddressFilePath
        {
            get; set;
        } = "";
        public string ExecAddressStr
        {
            get => field;
            set
            {
                field = value;
                execAddress = (uint)SprdFlashUtils.StringToSize(value);
            }
        } = "";
        bool IsAbleToSendExecAddr
        {
            get
            {
                if (!File.Exists(ExecAddressFilePath) || string.IsNullOrWhiteSpace(ExecAddressStr)) return false;
                if (execAddress == 0) return false;
                return true;
            }
        }
        uint execAddress;
        public class Config
        {
            public string Fdl1ToSendPath { get; set; } = string.Empty;
            public string Fdl1ToSendAddress { get; set; } = string.Empty;
            public string Fdl2ToSendPath { get; set; } = string.Empty;
            public string Fdl2ToSendAddress { get; set; } = string.Empty;
            public static void SaveConfig(Config config, string path)
            {
                XmlSerializer serializer = new XmlSerializer(typeof(Config));
                using (FileStream fs = new FileStream(path, FileMode.Create))
                {
                    serializer.Serialize(fs, config);
                }
            }
            public static Config? LoadConfig(string path)
            {
                XmlSerializer serializer = new XmlSerializer(typeof(Config));
                using (FileStream fs = new FileStream(path, FileMode.Open))
                {
                    return serializer.Deserialize(fs) as Config;
                }
            }
        }
        bool DeviceConnected
        {
            get => field;
            set
            {
                field = value;
                if (value) Dispatcher.BeginInvoke(() =>
                {
                    ConnectedPanel.Visibility = Visibility.Visible;
                });
            }
        }
        Stages SprdVersion
        {
            get => field;
            set
            {
                Dispatcher.Invoke(() =>
                {
                    field = value;
                    SprdTextBlock.Text = value.ToString();
                });
            }
        }
        Stages Stage
        {
            get => field;
            set
            {
                switch (value)
                {
                    case Stages.Brom:
                        IsExecAddrHidden = false;
                        break;
                    case Stages.Fdl1:
                        IsExecAddrHidden = true;
                        Dispatcher.BeginInvoke(() => Height = Height == 700 ? 600 : Height);
                        break;
                    case Stages.Fdl2:
                        OnFdl2Stage();
                        break;
                }
                Dispatcher.Invoke(() =>
                {
                    field = value;
                    StageTextBlock.Text = value.ToString();
                    AnimationControl.StartFadeOutAnimation(ProgressRing1, 0.3);
                    AnimationControl.StartFadeOutAnimation(ProgressText1, 0.3);
                });
            }
        }
        bool IsExecAddrHidden
        {
            get => field;
            set
            {
                field = value;
                var visibility = value ? Visibility.Collapsed : Visibility.Visible;
                Dispatcher.BeginInvoke(() =>
                {
                        ed0.Visibility = visibility;
                        ed1.Visibility = visibility;
                        ed2.Visibility = visibility;
                        ed3.Visibility = visibility;
                });
            }
        }
        private void OnFdl2Stage()
        {
            if (AutoSendNextTime)
                Config.SaveConfig(config, configPath);
            Dispatcher.Invoke(() =>
            {
                Hide();
                FlashWindow flashWindow = new FlashWindow(sprdFlashUtils, comPortMonitor);
                flashWindow.Show();
            });

        }
        private void UpdatePercentage(int percentage)
        {
            if (percentage >= 0 && percentage <= 100) Dispatcher.Invoke(() =>
            {
                if (ProgressRing1.Visibility == Visibility.Collapsed || ProgressRing1.Visibility == Visibility.Hidden)
                {
                    AnimationControl.StartFadeInAnimation(ProgressRing1, 0.3);
                }
                if (ProgressText1.Visibility == Visibility.Collapsed || ProgressText1.Visibility == Visibility.Hidden)
                {
                    AnimationControl.StartFadeInAnimation(ProgressText1, 0.3);
                }
                ProgressRing1.Progress = percentage;
                ProgressText1.Text = $"{percentage}%";
            });
        }
        public MainWindow()
        {
            InitializeComponent();
            if (Path.Exists(configPath))
            {
                AutoSendToggle.Visibility = Visibility.Visible;
                AutoSendToggle.IsChecked = true;
                AutoSend = true;
            }
            else AutoSendToggle.Visibility = Visibility.Collapsed;
            sprdProtocolHandler = new(new HdlcEncoder());
            sprdFlashUtils = new SprdFlashUtils(sprdProtocolHandler, null, UpdatePercentage);
            DataContext = this;
            config = new Config();
            exitEventHandler = async (sender, e) =>
           {
               await Task.Run(() =>
               {
                   try
                   {
                       if (DeviceConnected)
                           sprdProtocolHandler?.Dispose();
                   }
                   catch (Exception) { }
               });
           };
            Application.Current.Exit += exitEventHandler;
            string? temp = Application.ResourceAssembly?.GetName()?.Version?.ToString();
            if (temp != null)
                TitleBar1.Title += temp;
            snackbarService.SetSnackbarPresenter(RootSnackbar);
        }
        public async void ConnectToDeviceAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    string port = SprdProtocolHandler.FindComPort("SPRD U2S DIAG");
                    if (KickToAutodloader)
                    {
                        SprdFlashUtils.ChangeDiagnosticMode(sprdProtocolHandler,
                            (text) => Dispatcher.BeginInvoke(() => WaitingLabel.Text = text),
                            (text) => Dispatcher.BeginInvoke(() => snackbarService.Show("通知", text, ControlAppearance.Success, new SymbolIcon(SymbolRegular.Connected16), new TimeSpan(0, 0, 0, 1))),
                            KickMode);
                    }
                    else
                    {
                        Dispatcher.BeginInvoke(() => WaitingLabel.Text = $"正在连接端口{port}");
                        if (!sprdProtocolHandler.TryConnectChannel(port)) throw new InvalidOperationException($"连接端口{port}失败");
                        Dispatcher.BeginInvoke(() => snackbarService.Show("通知", $"成功连接端口{port}", ControlAppearance.Success, new SymbolIcon(SymbolRegular.Connected16), new TimeSpan(0, 0, 0, 1, 800)));
                    }
                    bool isDisconnected = false;
                    comPortMonitor = new(port, async () =>
                    {
                        if (!isDisconnected)
                        {
                            isDisconnected = true;
                            Dispatcher.Invoke(() => snackbarService.Show("设备断开连接", $"端口{port}断开连接\n1.5秒后退出程序 ! ", ControlAppearance.Danger, new SymbolIcon(SymbolRegular.ErrorCircle12), new TimeSpan(0, 0, 0, 5)));
                            await Task.Delay(1500);
                            Dispatcher.Invoke(Application.Current.Shutdown);
                        }
                    });
                    sprdProtocolHandler.Timeout = 1000;
                    var temp = sprdFlashUtils.ConnectToDevice(EnableReconnectMode);
                    Dispatcher.BeginInvoke(() => AnimationControl.StartFadeOutAnimation(WaitingPanel));
                    SprdVersion = temp.SprdMode;
                    Stage = temp.Stage;
                    DeviceConnected = true;
                    if (SprdVersion == Stages.Sprd4)
                    {
                        for (; Stage < Stages.Fdl2; Stage++)
                        {
                            sprdFlashUtils.ExecuteDataAndConnect(Stage);
                        }
                    }
                    if (AutoSend)
                    {
                        Config? config = Config.LoadConfig(configPath);
                        if (config != null && Path.Exists(config.Fdl1ToSendPath) && Path.Exists(config.Fdl2ToSendPath) && config.Fdl1ToSendAddress != null && config.Fdl2ToSendAddress != null)
                        {
                            Dispatcher.BeginInvoke(() => SendFdlButton.IsEnabled = false);
                            for (; Stage < Stages.Fdl2; Stage++)
                            {
                                string fdlToSendPath = Stage == 0 ?
                                    config.Fdl1ToSendPath
                                    :
                                    config.Fdl2ToSendPath;
                                sprdFlashUtils.SendFile(File.OpenRead(fdlToSendPath),
                                    Stage == 0 ?
                                    StringToUint(config.Fdl1ToSendAddress)
                                    :
                                    StringToUint(config.Fdl2ToSendAddress));
                                Dispatcher.BeginInvoke(() => snackbarService.Show($"{Stage.ToString()}阶段操作成功", $"已发送{Path.GetFileName(FdlFilePath)}", ControlAppearance.Success, new SymbolIcon(SymbolRegular.Send16), new TimeSpan(0, 0, 0, 1, 700)));
                                sprdFlashUtils.ExecuteDataAndConnect(Stage);
                                Dispatcher.BeginInvoke(() => snackbarService.Show($"{Stage.ToString()}阶段操作成功", $"已连接{Stage.ToString()}", ControlAppearance.Success, new SymbolIcon(SymbolRegular.Send16), new TimeSpan(0, 0, 0, 1, 700)));
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    sprdProtocolHandler?.Dispose();
                    System.Windows.MessageBox.Show($"发生错误：{ex.Message}");
                    Dispatcher.InvokeShutdown();
                }
            });
        }
        private void TextBoxPreviewDragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }
        private void TextBoxPreviewDrop(object sender, DragEventArgs e)
        {
            string[]? temp = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (temp != null)
                if (sender is TextBox textbox)
                    Dispatcher.Invoke(() =>
                    {
                        textbox.Text = temp[0];
                    });
        }
        private uint StringToUint(string s)
        {
            bool isHex = false;
            string tempAddress = s;
            if (s.ToLower().StartsWith("0x"))
            {
                tempAddress = s.Substring(2); isHex = true;
            }
            return uint.Parse(tempAddress, isHex ? System.Globalization.NumberStyles.HexNumber : System.Globalization.NumberStyles.Any);
        }
        private async void SendFdlButton_Click(object sender, RoutedEventArgs e)
        {
            await Task.Run(() =>
            {
                bool isHex = false;
                int address = 0;
                string tempAddress = StrAddress;
                if (string.IsNullOrEmpty(StrAddress) || string.IsNullOrEmpty(FdlFilePath))
                {

                    Dispatcher.BeginInvoke(() => snackbarService.Show("提示", "fdl路径或地址不能为空", ControlAppearance.Caution, new SymbolIcon(SymbolRegular.Info12), new TimeSpan(0, 0, 0, 1, 500)));
                    return;
                }
                if (tempAddress.ToLower().StartsWith("0x"))
                {
                    tempAddress = StrAddress.Substring(2); isHex = true;
                }

                if (!Path.Exists(FdlFilePath) || !int.TryParse(tempAddress, isHex ? System.Globalization.NumberStyles.HexNumber : System.Globalization.NumberStyles.Any, null, out address))
                {
                    Dispatcher.BeginInvoke(() => Dispatcher.BeginInvoke(() => snackbarService.Show("提示", "fdl路径或发送地址错误", ControlAppearance.Caution, new SymbolIcon(SymbolRegular.Info12), new TimeSpan(0, 0, 0, 1, 500))));
                    return;
                }
                Dispatcher.Invoke(() => SendFdlButton.IsEnabled = false);
                switch (Stage)
                {
                    case Stages.Brom:
                        config.Fdl1ToSendPath = FdlFilePath;
                        config.Fdl1ToSendAddress = StrAddress;
                        break;
                    case Stages.Fdl1:
                        config.Fdl2ToSendPath = FdlFilePath;
                        config.Fdl2ToSendAddress = StrAddress;
                        break;
                }
                sprdFlashUtils.Timeout = 10000;
                sprdFlashUtils.SendFile(File.OpenRead(FdlFilePath), (uint)address);
                if (Stage == Stages.Brom && IsAbleToSendExecAddr)
                {
                    sprdFlashUtils.SendFile(File.OpenRead(ExecAddressFilePath), execAddress, sendEndData: false);
                }
                Dispatcher.BeginInvoke(() => snackbarService.Show($"{Stage}阶段操作成功", $"已发送{Path.GetFileName(FdlFilePath)}{(Stage == Stages.Brom && IsAbleToSendExecAddr ? $"及{Path.GetFileName(ExecAddressFilePath)}" : "")}", ControlAppearance.Success, new SymbolIcon(SymbolRegular.Send16), new TimeSpan(0, 0, 0, 1, 700)));
                sprdFlashUtils.ExecuteDataAndConnect(Stage);
                Stage++;
                Dispatcher.BeginInvoke(() => snackbarService.Show($"{Stage}阶段操作成功", $"已连接{Stage}", ControlAppearance.Success, new SymbolIcon(SymbolRegular.Send16), new TimeSpan(0, 0, 0, 1, 700)));

                Dispatcher.Invoke(() => SendFdlButton.IsEnabled = true);
            });
        }

        private void FluentWindow_Loaded(object sender, RoutedEventArgs e)
        {
            ContentDialogService contentDialog = new ContentDialogService();
            contentDialog.SetDialogHost(RootContentDialogPresenter);
            ConnectToDeviceAsync();
        }
    }
}