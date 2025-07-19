using SPRDClient.Utils;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace SPRDClient.Pages
{
    /// <summary>
    /// HomePage.xaml 的交互逻辑
    /// </summary>
    public partial class HomePage : Page, INotifyPropertyChanged

    {
        private SnackbarService snackbarService;
        private ContentDialogService contentDialogService;
        public event PropertyChangedEventHandler? PropertyChanged;
        public Grid? RootGrid;


        public struct PartitionDisplay
        {
            public PartitionDisplay(Partition partition)
            {
                Name = partition.Name;
                DisplaySize = (int)Math.Ceiling((decimal)partition.Size / (1UL << partition.IndicesToMB));
            }
            public string Name { get; set; }
            public int DisplaySize { get; set; }
        }
        private ObservableCollection<PartitionDisplay> DisplayPartitions = new ObservableCollection<PartitionDisplay>();
        private SprdFlashUtils sprdFlashUtils;
        private FlashModel flashModel;
        public HomePage(SprdFlashUtils sprdFlash, FlashModel flashModel, SnackbarService snackbarService, ContentDialogService contentDialogService)
        {
            InitializeComponent();
            this.snackbarService = snackbarService;
            this.contentDialogService = contentDialogService;
            PartitionsListView.ItemsSource = DisplayPartitions;
            sprdFlashUtils = sprdFlash;
            this.flashModel = flashModel;
            flashModel.Timeout = 10000;
            sprdFlashUtils.Verbose = false;
            sprdFlashUtils.UpdateStatus += (string log) => flashModel.StatusLogText = log;
            sprdFlashUtils.Percentage = 10000.0F;
            sprdFlashUtils.UpdatePercentage += UpdatePercentage;
            DataContext = flashModel;
            AnimationControl.StartFadeInAnimation(LoadingPanel, 0.9);
        }
        private void OnPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }
        private void HomePage_Loaded(object sender, RoutedEventArgs e)
        {
            if (DisplayPartitions.Count == 0)
            {
                snackbarService.Show("Fdl2阶段操作成功", $"已连接Fdl2", ControlAppearance.Success, new SymbolIcon(SymbolRegular.Connected16), new TimeSpan(0, 0, 0, 2));
                LoadPartitionsAsync();
            }
        }
        private void UpdatePercentage(int percentage)
        {
            Dispatcher.Invoke(() =>
            {
                if (percentage >= 0 && percentage <= 10000) Dispatcher.Invoke(() =>
            {
                if (OperationProgress.Visibility == Visibility.Collapsed || OperationProgress.Visibility == Visibility.Hidden)
                {
                    AnimationControl.StartFadeInAnimation(OperationProgress, 0.3);
                }
                if (OperationProgress.Visibility == Visibility.Collapsed || OperationProgress.Visibility == Visibility.Hidden)
                {
                    AnimationControl.StartFadeInAnimation(OperationProgress, 0.3);
                }

                OperationProgress.Value = percentage;
                ProgressText1.Text = $"{percentage / 100}%";
            });
            });
        }

        private async void LoadPartitionsAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    flashModel.partitions = GetDevicePartitions();
                    Dispatcher.Invoke((Delegate)(() =>
                    {
                        foreach (var partition in flashModel.partitions)
                        {
                            this.DisplayPartitions.Add(new PartitionDisplay(partition));
                        }
                    }));
                    Dispatcher.BeginInvoke(() =>
                    {
                        AnimationControl.StartFadeOutAnimation(LoadingPanel, 1.6);
                        AnimationControl.StartFadeInAnimation(PartitionsListView, 0.7);
                        snackbarService.Show("分区表获取成功", $"成功获取{flashModel.partitions.Count}个分区", ControlAppearance.Success, new SymbolIcon(SymbolRegular.Connected16), new TimeSpan(0, 0, 0, 5));
                    }, System.Windows.Threading.DispatcherPriority.ContextIdle);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"加载分区失败: {ex.Message}");
                    Dispatcher.InvokeShutdown();
                }
            });
        }
        private List<Partition> GetDevicePartitions()
        {
            var temp = sprdFlashUtils.GetPartitionsAndStorageInfo(
                SpecifiedLog: (string log) => flashModel.GettingPartitionsLogText += $"\n{log}",
                CheckConfirm: () => Dispatcher.Invoke(() => flashModel.CheckConfirm("获取分区表方法确认", "方法一获取分区表失败，请选择获取分区表方法\n推荐兼容模式", closeButtonText: "方法二", primaryButtonText: "兼容模式"))
            );
            List<Partition> partitions = temp.partitions;
            flashModel.GetPartitionsMethod = temp.finalMethod;
            if (flashModel.IsUsingNewFdl2)
                flashModel.SkipSignVerify = true;

            return partitions;
        }
        #region ui处理
        #endregion
        #region 按钮点击事件
        private void ReadButton_Click(object sender, RoutedEventArgs e)
        {
            flashModel.ReadButton_Click(sender, e);
        }
        private void WriteButton_Click(object sender, RoutedEventArgs e)
        {
            flashModel.WriteButton_Click(sender, e);
        }
        private void EraseButton_Click(object sender, RoutedEventArgs e)
        {
            flashModel.EraseButton_Click(sender, e);
        }
        private void PowerOnButton_Click(object sender, RoutedEventArgs e)
        {
            sprdFlashUtils.PowerOnDevice();
            Application.Current.Shutdown();
        }

        private void PowerOffButton_Click(object sender, RoutedEventArgs e)
        {
            sprdFlashUtils.ShutdownDevice();
            Application.Current.Shutdown();
        }
        private void BackupButton_Click(object sender, RoutedEventArgs e)
        {
            flashModel.BackupButton_Click(sender, e, flashModel.partitions);
        }

        private void RestoreButton_Click(object sender, RoutedEventArgs e)
        {
            flashModel.RestoreButton_Click(sender, e);
        }
        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            await flashModel.SavePartitions();
        }

        #endregion

    }
}
