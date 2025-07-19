using SPRDClientCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Wpf.Ui.Controls;
using SPRDClient.Pages;
using static SPRDClient.FlashWindow;
using Wpf.Ui;
using Wpf.Ui.Extensions;
using System.Windows.Media.Effects;
using SPRDClient.Utils;

namespace SPRDClient
{
    /// <summary>
    /// FlashWindow.xaml 的交互逻辑
    /// </summary>
    public partial class FlashWindow
    {
        /*        private void UpdatePercentage(int percentage)
                {
                    if (percentage >= 0 && percentage <= 100) Dispatcher.Invoke(() =>
                    {
                        if (ProgressRing1.Visibility == Visibility.Collapsed || ProgressRing1.Visibility == Visibility.Hidden)
                        {
                            AnimationControl.StartFadeInAnimation(ProgressRing1, 0.3);
                        }
                        ProgressRing1.Progress = percentage;
                    });
                }
        */
        HomePage homePage;
        SeniorPage seniorPage;
        LogPage logPage;
        SettingsPage settingsPage;

        FlashModel flashModel;

        ComPortMonitor? comPortMonitor;
        ContentDialogService contentDialogService = new ContentDialogService();
        public FlashWindow(SprdFlashUtils flashUtils, ComPortMonitor? comPortMonitor)
        {
            InitializeComponent();
            logPage = new LogPage();
            flashUtils.Log += logPage.CommonLog;
            flashUtils.Handler.Log += logPage.PacketLog;
            SnackbarService snackbarService = new SnackbarService();
            snackbarService.SetSnackbarPresenter(RootSnackbarPresenter);
            contentDialogService = new ContentDialogService();
            contentDialogService.SetDialogHost(RootContentPresenter);
            flashModel = new(flashUtils,snackbarService,contentDialogService,RootGrid);
            homePage = new HomePage(flashUtils,flashModel, snackbarService,contentDialogService) { RootGrid = RootGrid};
            settingsPage = new SettingsPage(flashModel);
            seniorPage = new(flashModel);
            string? temp = Application.ResourceAssembly?.GetName()?.Version?.ToString();
            if (temp != null)
                TitleBar1.Title += temp;
            contentDialogService.SetDialogHost(RootContentPresenter);
            this.comPortMonitor = comPortMonitor;
            this.comPortMonitor?.SetDisconnectedAction(() =>
            {
                Dispatcher.Invoke(async () =>
                {
                    RootGrid.Effect = new BlurEffect() { Radius = 10 };
                    ContentDialog contentDialog = new ContentDialog() {
                         CloseButtonIcon = new SymbolIcon(SymbolRegular.ArrowExit20),
                         CloseButtonText = "退出程序",
                         Title = "严重警告",
                         Content = "检测到设备已断开连接",
                    };
                    await contentDialogService.ShowAsync(contentDialog,CancellationToken.None);
                    Application.Current.Shutdown();
                });
            });
        }

        private void FluentWindow_Closed(object sender, EventArgs e)
        {
            Application.Current.Shutdown();
        }
        /*        private async void OnReadPartitionClicked(object sender, RoutedEventArgs e)
                {
                    string partitionName = ReadPartitionTextBox.Text;
                    await Task.Run(() =>
                    {
                        if (!string.IsNullOrEmpty(partitionName))
                        {
                            if (sender is Wpf.Ui.Controls.Button btn)
                            {
                                if (sprdFlashUtils.CheckPartitionExist(partitionName))
                                {
                                    Dispatcher.Invoke(() => btn.IsEnabled = false);
                                    sprdFlashUtils.ReadPartitionCustomize($"{partitionName}.img", partitionName, sprdFlashUtils.GetPartitionSize(partitionName));
                                    Dispatcher.Invoke(() => btn.IsEnabled = true);
                                }
                            }
                        }
                    });
                }

                private void OnBrowseImageClicked(object sender, RoutedEventArgs e)
                {
                    var dialog = new Microsoft.Win32.OpenFileDialog
                    {
                        Filter = "镜像文件|*.img;*.bin|所有文件|*.*"
                    };
                    bool? result = dialog.ShowDialog();
                    if (result == true)
                    {
                        ImagePathTextBox.Text = dialog.FileName;
                    }
                }
                private async void  OnWritePartitionClicked(object sender, RoutedEventArgs e)
                {
                    if (sender is Wpf.Ui.Controls.Button btn)
                    {
                        string partitionName = WritePartitionTextBox.Text;
                        string imagePath = ImagePathTextBox.Text;
                        await Task.Run(() =>
                        {
                            if (!System.IO.Path.Exists(imagePath))
                                return;
                            Dispatcher.Invoke(() => btn.IsEnabled = false);
                            sprdFlashUtils.WritePartition(partitionName, File.OpenRead(imagePath));
                            Dispatcher.Invoke(() => btn.IsEnabled = true);
                        });

                    }
                }
        */


        private void RootNavigation_SelectionChanged(NavigationView sender, RoutedEventArgs args)
        {
            if (RootNavigation.SelectedItem is NavigationViewItem item && item.Tag is string tag)
            {
                switch (tag)
                {
                    case "homepage":
                        RootNavigation.ReplaceContent(homePage);
                        break;
                    case "seniorpage":
                        RootNavigation.ReplaceContent(seniorPage);
                        break;
                    case "logpage":
                        RootNavigation.ReplaceContent(logPage);
                        break;
                    case "settingspage":
                        RootNavigation.ReplaceContent(settingsPage);
                        break;
                }
            }
        }

        private void FluentWindow_Loaded(object sender, RoutedEventArgs e)
        {
            RootNavigation.Navigate("homepage");
        }
    }

}

