using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Effects;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;

namespace SPRDClient.Utils
{
    public class FlashModel : INotifyPropertyChanged
    {
        public string GettingPartitionsLogText
        {
            get => field;
            set
            {
                field = value;
                OnPropertyChanged(nameof(GettingPartitionsLogText));
            }
        } = String.Empty;
        public GetPartitionsMethod GetPartitionsMethod
        {
            get => field;
            set
            {
                field = value;
                IsUsingNewFdl2 = field != GetPartitionsMethod.TraverseCommonPartitions;
            }
        }
        public string StatusLogText
        {
            get => field;
            set { field = value; OnPropertyChanged(nameof(StatusLogText)); }
        } = String.Empty;

        public int BlockSize
        {
            get { return sprdFlashUtils.PerBlockSize; }
            set
            {
                if (value <= 128)
                {
                    value = 128;
                }
                if (value >= 0xffa0)
                {
                    value = 0xffa0;
                }
                sprdFlashUtils.PerBlockSize = (ushort)((ushort)value % 2 == 1 ? value + 1 : value);
                OnPropertyChanged(nameof(BlockSize));
            }
        }
        public bool UseAsyncSpeedUp
        {
            get => field;
            set
            {
                field = value;
                OnPropertyChanged(nameof(UseAsyncSpeedUp));
            }
        } = true;
        public bool IsUsingNewFdl2
        {
            get => field;
            private set
            {
                field = value;
                OnPropertyChanged(nameof(IsUsingNewFdl2));
            }
        }
        public bool SkipSignVerify
        {
            get => field;
            set
            {
                field = value;
                OnPropertyChanged(nameof(SkipSignVerify));
            }
        }
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

        public int Timeout { get => sprdFlashUtils.Timeout; set { sprdFlashUtils.Timeout = value; OnPropertyChanged(nameof(Timeout)); } }
        public bool Verbose { get => sprdFlashUtils.Verbose; set { sprdFlashUtils.Verbose = value; OnPropertyChanged(nameof(Verbose)); } }
        public string SaveFileDirectory { get => field; set { field = value; OnPropertyChanged(nameof(SaveFileDirectory)); } } = Environment.CurrentDirectory;
       
        public List<Partition> partitions = [];
        public ObservableCollection<PartitionDisplay> DisplayPartitions { get;private set; } = [];
        public bool isActing { get; private set; } = false;
        private bool isReadingPartition = false;

        private CancellationTokenSource cts = new();

        public SprdFlashUtils sprdFlashUtils;
        public SnackbarService snackbarService;
        private ContentDialogService contentDialogService;
        private Grid rootGrid;
        public FlashModel(SprdFlashUtils sprdFlashUtils, SnackbarService snackbarService, ContentDialogService contentDialogService, Grid grid)
        {
            this.sprdFlashUtils = sprdFlashUtils;
            this.snackbarService = snackbarService;
            this.contentDialogService = contentDialogService;

            rootGrid = grid;
        }
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null) { PropertyChanged(this, new PropertyChangedEventArgs(propertyName)); }
        }
        public async Task<bool> CheckConfirm(string title, string message, string closeButtonText = "取消", string primaryButtonText = "确定")
        {
            if (rootGrid != null)
                rootGrid.Effect = new BlurEffect() { Radius = 12 };
            bool result = await contentDialogService.ShowSimpleDialogAsync(
                  new SimpleContentDialogCreateOptions()
                  {
                      Title = title,
                      CloseButtonText = closeButtonText,
                      Content = message,
                      PrimaryButtonText = primaryButtonText
                  }
                  ) == ContentDialogResult.Primary;
            if (rootGrid != null)
                rootGrid.Effect = null;
            return result;
        }
        public async Task<ContentDialogResult> CheckConfirm(string title, string message, string secondButtonText, string closeButtonText = "取消", string primaryButtonText = "确定")
        {
            if (rootGrid != null)
                rootGrid.Effect = new BlurEffect() { Radius = 12 };
            ContentDialogResult contentDialogResult = await contentDialogService.ShowSimpleDialogAsync(
                  new SimpleContentDialogCreateOptions()
                  {
                      Title = title,
                      CloseButtonText = closeButtonText,
                      Content = message,
                      SecondaryButtonText = secondButtonText,
                      PrimaryButtonText = primaryButtonText
                  }
                  );
            if (rootGrid != null)
                rootGrid.Effect = null;
            return contentDialogResult;
        }
        private async Task CancelActions()
        {
            await Task.Run(() =>
            {
                if (isReadingPartition)
                {
                    isReadingPartition = false;
                }
                isActing = false;
                cts.Cancel();
                cts.Dispose();
                cts = new CancellationTokenSource();
            });
        }
        private async Task<bool> CheckReadConfirm()
        {
            if (isReadingPartition)
            {
                if (await CheckConfirm("读取确认", "当前正在读取分区，是否停止读取操作？", "继续读取", "停止读取"))
                {
                    await CancelActions();
                    return false;
                }
            }

            if (isActing)
            {
                snackbarService.Show("警告", "当前正在进行其他操作，无法读取分区!",
                                   ControlAppearance.Caution,
                                   new SymbolIcon(SymbolRegular.Info12),
                                   TimeSpan.FromSeconds(3));
                return false;
            }
            return true;
        }
        public async void ReadButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Wpf.Ui.Controls.Button button)
            {
                if (button.Tag is PartitionDisplay partition)
                {
                    await CustomReadButton_Click(button, partition.Name, 0, 0);
                    return;
                }
            }
        }
        public async Task CustomReadButton_Click(Wpf.Ui.Controls.Button button, string partName, ulong size, ulong offset)
        {
            if (!await CheckReadConfirm()) return;

            object originContent = button.Content;

            button.Content = "停止读取";
            isActing = true;
            isReadingPartition = true;

            try
            {
                snackbarService.Show("Fdl2阶段分区操作", $"开始读取 {partName} 分区",
                                   ControlAppearance.Info,
                                   new SymbolIcon(SymbolRegular.ArrowDownload16),
                                   TimeSpan.FromSeconds(3));

                await Task.Run(async () =>
                {
                    using (FileStream fileStream = File.Create($"{partName}.img"))
                    {
                        if (UseAsyncSpeedUp)
                            await sprdFlashUtils.ReadPartitionCustomizeAsync(
                                fileStream, partName, size == 0 ? sprdFlashUtils.GetPartitionSize(partName) : size, cts.Token, offset);
                        else sprdFlashUtils.ReadPartitionCustomize(fileStream, partName, size == 0 ? sprdFlashUtils.GetPartitionSize(partName) : size, offset);
                    }
                });

                snackbarService.Show("Fdl2阶段操作成功", $"{partName} 分区读取完毕",
                                   ControlAppearance.Success,
                                   new SymbolIcon(SymbolRegular.ArrowDownloadOff16),
                                   TimeSpan.FromSeconds(3));
            }
            catch (OperationCanceledException)
            {
                snackbarService.Show("操作已取消", $"{partName} 分区读取已中断",
                                   ControlAppearance.Caution,
                                   new SymbolIcon(SymbolRegular.DismissCircle16),
                                   TimeSpan.FromSeconds(3));
            }
            catch (Exception ex)
            {
                snackbarService.Show("Fdl2阶段操作失败", $"读取 {partName} 分区时发生异常！\n错误: {ex.Message}",
                                   ControlAppearance.Danger,
                                   new SymbolIcon(SymbolRegular.ErrorCircle12),
                                   TimeSpan.FromSeconds(6));

                await CheckConfirm("读取分区失败", $"读取 {partName} 分区时发生异常！\n错误: {ex.Message}",
                                 "忽略", string.Empty);
            }
            finally
            {
                isActing = false;
                isReadingPartition = false;
                button.Content = originContent;
            }
        }
        public async void WriteButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Wpf.Ui.Controls.Button button && button.Tag is PartitionDisplay partition)
            {
                var openFileDialog = new OpenFileDialog();
                if (openFileDialog.ShowDialog() != true) return;

                string filePath = openFileDialog.FileName;
                if (!File.Exists(filePath)) return;

                await CustomWriteButton_Click(button, partition.Name, filePath, 0);
            }
        }

        public async Task CustomWriteButton_Click(
            Wpf.Ui.Controls.Button button,
            string partName,
            string filePath,
            ulong offset = 0)
        {
            if (isActing)
            {
                snackbarService.Show("警告", "当前正在进行其他操作，无法写入分区!",
                                   ControlAppearance.Caution,
                                   new SymbolIcon(SymbolRegular.Info12),
                                   TimeSpan.FromSeconds(3));
                return;
            }

            bool originEnabled = button.IsEnabled;

            bool skipVerify = SkipSignVerify && IsUsingNewFdl2
                && partName != "splloader"
                && partName != "ubipac"
                && partName != "sml";

            button.IsEnabled = false;
            isActing = true;

            try
            {
                snackbarService.Show("Fdl2阶段分区操作", $"开始写入 {partName} 分区",
                                   ControlAppearance.Info,
                                   new SymbolIcon(SymbolRegular.ArrowUpload16),
                                   TimeSpan.FromSeconds(3));

                await Task.Run(async () =>
                {
                    List<Partition> partList = GetPartitionListWithoutSplloader();
                    using (FileStream fileStream = File.OpenRead(filePath))
                    {
                        if (offset == 0)
                        {
                            if (UseAsyncSpeedUp)
                                if (skipVerify)
                                {
                                    await sprdFlashUtils.WritePartitionWithoutVerifyAsync(partName, partList, fileStream, cts.Token);
                                }
                                else
                                    await sprdFlashUtils.WritePartitionAsync(partName, fileStream, cts.Token);
                            else
                            if (skipVerify)
                            {
                                sprdFlashUtils.WritePartitionWithoutVerify(partName, partList, fileStream);
                            }
                            else
                                sprdFlashUtils.WritePartition(partName, fileStream);
                        }
                        else
                        {
                            if (skipVerify)
                            {
                                sprdFlashUtils.WritePartitionWithoutVerify(partName, partList, fileStream, offset);
                            }
                            else
                                sprdFlashUtils.WritePartition(partName, fileStream, offset);
                        }
                    }
                });

                snackbarService.Show("Fdl2阶段操作成功", $"{partName} 分区写入完毕",
                                   ControlAppearance.Success,
                                   new SymbolIcon(SymbolRegular.Checkmark12),
                                   TimeSpan.FromSeconds(3));
            }
            catch (Exception ex)
            {
                snackbarService.Show("Fdl2阶段操作失败", $"写入 {partName} 分区时发生异常！\n错误: {ex.Message}",
                                   ControlAppearance.Danger,
                                   new SymbolIcon(SymbolRegular.ErrorCircle12),
                                   TimeSpan.FromSeconds(6));

                await CheckConfirm("刷写分区失败", $"刷写 {partName} 分区时发生异常！\n错误: {ex.Message}",
                                 "忽略", string.Empty);
            }
            finally
            {
                isActing = false;
                button.IsEnabled = originEnabled;
            }
        }
        public async void EraseButton_Click(object sender, RoutedEventArgs e)
        {
            if (isActing)
            {
                snackbarService.Show("警告", $"当前正在进行其他操作，无法擦除分区!", ControlAppearance.Caution, new SymbolIcon(SymbolRegular.Info12), new TimeSpan(0, 0, 0, 3));
                return;
            }
            if (sender is Wpf.Ui.Controls.Button button)
            {
                var partition = (PartitionDisplay)button.Tag;
                if (partition.Name == "userdata")
                {
                    ContentDialogResult tmp = await CheckConfirm(title: "擦除分区确认", message: $"确定要擦除{partition.Name}分区吗？\n方法一：开机后进入recovery执行双清操作\n方法二：直接擦除userdata分区\n方法二可能会使设备成砖 !", secondButtonText: "方法二", primaryButtonText: "方法一");
                    button.IsEnabled = false;
                    snackbarService.Show("Fdl2阶段分区操作", $"开始擦除{partition.Name}分区", ControlAppearance.Info, new SymbolIcon(SymbolRegular.ArrowUpload16), new TimeSpan(0, 0, 0, 3));
                    isActing = true;
                    try
                    {
                        switch (tmp)
                        {
                            case ContentDialogResult.None: break;
                            case ContentDialogResult.Primary:
                                await Task.Run(() =>
                                sprdFlashUtils.ResetToCustomMode(CustomModesToReset.FactoryReset)
                                );
                                break;
                            case ContentDialogResult.Secondary:
                                await Task.Run(() => sprdFlashUtils.ErasePartition(partition.Name));
                                break;
                        }
                        snackbarService.Show("Fdl2阶段操作成功", $"{partition.Name}分区擦除完毕", ControlAppearance.Info, new SymbolIcon(SymbolRegular.ArrowDownloadOff16), new TimeSpan(0, 0, 0, 3));
                    }
                    catch (ResponseTimeoutReachedException)
                    {
                        if (await contentDialogService.ShowSimpleDialogAsync(new SimpleContentDialogCreateOptions() { CloseButtonText = "我已知晓", Content = $"擦除{partition.Name}分区响应超时！\n是否重试？", Title = "读取分区失败" }) == ContentDialogResult.Primary)
                        {
                            await Task.Run(() => sprdFlashUtils.ErasePartition(partition.Name));
                        }
                        ;
                    }
                    catch (Exception ex)
                    {
                        snackbarService.Show("Fdl2阶段操作失败", $"擦除{partition.Name}分区时发生异常！\n错误:{ex.Message}", ControlAppearance.Danger, new SymbolIcon(SymbolRegular.ErrorCircle12), new TimeSpan(0, 0, 0, 6));
                        await CheckConfirm("擦除分区失败", $"擦除{partition.Name}分区时发生异常！\n错误:{ex.Message}", "忽略", string.Empty);
                    }
                    isActing = false;
                    button.IsEnabled = true;
                    return;
                }
                if (await CheckConfirm(title: "擦除分区确认", message: $"确定要擦除{partition.Name}分区吗？\n{"此操作非普通格式化，会将文件系统一并擦除"}\n一旦擦除，设备可能变砖且数据将永久丢失 !"))
                {
                    button.IsEnabled = false;
                    snackbarService.Show("Fdl2阶段分区操作", $"开始擦除{partition.Name}分区", ControlAppearance.Info, new SymbolIcon(SymbolRegular.ArrowUpload16), new TimeSpan(0, 0, 0, 3));
                    isActing = true;
                    try
                    {
                        await Task.Run(() => sprdFlashUtils.ErasePartition(partition.Name));
                        snackbarService.Show("Fdl2阶段操作成功", $"{partition.Name}分区擦除完毕", ControlAppearance.Info, new SymbolIcon(SymbolRegular.ArrowDownloadOff16), new TimeSpan(0, 0, 0, 3));
                    }
                    catch (ResponseTimeoutReachedException)
                    {
                        if (await contentDialogService.ShowSimpleDialogAsync(new SimpleContentDialogCreateOptions() { CloseButtonText = "我已知晓", Content = $"擦除{partition.Name}分区响应超时！\n是否重试？", Title = "读取分区失败" }) == ContentDialogResult.Primary)
                            await Task.Run(() => sprdFlashUtils.ErasePartition(partition.Name));
                    }
                    catch (Exception ex)
                    {
                        snackbarService.Show("Fdl2阶段操作失败", $"擦除{partition.Name}分区时发生异常！\n错误:{ex.Message}", ControlAppearance.Danger, new SymbolIcon(SymbolRegular.ErrorCircle12), new TimeSpan(0, 0, 0, 6));
                        await CheckConfirm("擦除分区失败", $"擦除{partition.Name}分区时发生异常！\n错误:{ex.Message}", "忽略", string.Empty);
                    }
                    isActing = false;
                    button.IsEnabled = true;
                }
            }
        }
        public async Task SavePartitions()
        {
            if (GetPartitionsMethod == GetPartitionsMethod.TraverseCommonPartitions)
            {
                snackbarService.Show("警告", "当前分区表获取方法不支持保存分区表！", ControlAppearance.Caution, new SymbolIcon(SymbolRegular.Info12), new TimeSpan(0, 0, 0, 3));
                return;
            }
            SaveFileDialog sfd = new SaveFileDialog
            {
                DefaultDirectory = Environment.CurrentDirectory,
                Title = "保存分区表",
                Filter = "分区表文件 (*.xml)|*.xml|所有文件 (*.*)|*.*"
            };
            if (sfd.ShowDialog() == true)
            {
                try
                {

                    await Task.Run(() =>
                    {
                        if (partitions.Count == 0)
                        {
                            return;
                        }
                        using (FileStream fs = new(sfd.SafeFileName, FileMode.Create))
                            SprdFlashUtils.SavePartitionsToXml(GetPartitionListWithoutSplloader(), fs);
                    });
                    snackbarService.Show("保存分区表成功", $"已保存分区表到 {Path.GetFileName(sfd.FileName)}", ControlAppearance.Success, new SymbolIcon(SymbolRegular.Save16), new TimeSpan(0, 0, 0, 2));
                }
                catch (Exception ex)
                {
                    snackbarService.Show("保存分区表失败", $"错误: {ex.Message}", ControlAppearance.Danger, new SymbolIcon(SymbolRegular.ErrorCircle16), new TimeSpan(0, 0, 0, 5));
                }
            }
        }
        public List<Partition> GetPartitionListWithoutSplloader()
        {
            List<Partition> partitions = this.partitions.ToList();
            partitions.RemoveAt(0);
            return partitions;
        }
        public void PowerOnButton_Click(object sender, RoutedEventArgs e)
        {
            sprdFlashUtils.PowerOnDevice();
            Application.Current.Shutdown();
        }
        public void PowerOffButton_Click(object sender, RoutedEventArgs e)
        {
            sprdFlashUtils.ShutdownDevice();
            Application.Current.Shutdown();
        }
        public async void BackupButton_Click(object sender, RoutedEventArgs e, List<Partition>? partitions)
        {
            if (!await CheckReadConfirm()) return;
            isActing = true;
            isReadingPartition = true;
            if (sender is Wpf.Ui.Controls.Button btn)
            {
                object originContent = btn.Content;
                btn.Content = "停止备份";
                try
                {
                    await Task.Run(async () =>
            {
                if (partitions != null)
                    foreach (Partition partition in partitions)
                    {
                        if (!isReadingPartition)
                            break;
                        if (partition.Name == "ubipac" || partition.Name == "userdata" || partition.Name == "cache")
                            continue;
                        await Application.Current.Dispatcher.BeginInvoke(() => snackbarService.Show("Fdl2阶段分区操作", $"开始读取{partition.Name}分区", ControlAppearance.Info, new SymbolIcon(SymbolRegular.ArrowDownload16), new TimeSpan(0, 0, 0, 3)), System.Windows.Threading.DispatcherPriority.ContextIdle);
                        using (FileStream fs = File.Create(Path.Combine(SaveFileDirectory, $"SPRD-Backup-{partition.Name}.img")))
                        {
                            if (UseAsyncSpeedUp)
                                await sprdFlashUtils.ReadPartitionCustomizeAsync(
                                    partDataStream: fs,
                                    partName: partition.Name,
                                    size: partition.Size << (20 - partition.IndicesToMB),
                                    ct: cts.Token
                                );
                            else
                                sprdFlashUtils.ReadPartitionCustomize(
                                    partDataStream: fs,
                                    partName: partition.Name,
                                    size: partition.Size << (20 - partition.IndicesToMB)
                                    );

                            await Application.Current.Dispatcher.BeginInvoke(() => snackbarService.Show("Fdl2阶段操作成功", $"{partition.Name}分区读取完毕", ControlAppearance.Info, new SymbolIcon(SymbolRegular.ArrowDownloadOff16), new TimeSpan(0, 0, 0, 3)), System.Windows.Threading.DispatcherPriority.ContextIdle);

                        }
                    }
            });

                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    snackbarService.Show("Fdl2阶段操作失败", $"备份全机时发生异常！\n错误:{ex.Message}", ControlAppearance.Danger, new SymbolIcon(SymbolRegular.ErrorCircle12), new TimeSpan(0, 0, 0, 6));
                    await CheckConfirm("备份全机失败", $"备份全机时发生异常！\n错误:{ex.Message}", "忽略", string.Empty);
                }
                isActing = false;
                isReadingPartition = false;
                btn.Content = originContent;
            }
        }

        public async void RestoreButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Wpf.Ui.Controls.Button btn)
            {
                if (isActing)
                {
                    snackbarService.Show("警告", $"当前正在进行其他操作，无法写入分区!", ControlAppearance.Caution, new SymbolIcon(SymbolRegular.Info12), new TimeSpan(0, 0, 0, 3));
                    return;
                }
                OpenFolderDialog openFolderDialog = new OpenFolderDialog();
                openFolderDialog.DefaultDirectory = Environment.CurrentDirectory;
                if (openFolderDialog.ShowDialog() == true)
                {
                    string[] files = Directory.GetFiles(openFolderDialog.FolderName, "SPRD-Backup-*.img");
                    isActing = true;
                    btn.IsEnabled = false;
                    try
                    {
                        await Task.Run(() =>
                        {
                            foreach (string file in files)
                            {
                                string partName = file.Split(new[] { "SPRD-Backup-", ".img" }, StringSplitOptions.RemoveEmptyEntries)[1];
                                if (partName == "ubipac" || partName == "userdata" || partName == "cache") continue;
                                Application.Current.Dispatcher.BeginInvoke(() => snackbarService.Show("Fdl2阶段分区操作", $"开始写入{partName}分区", ControlAppearance.Info, new SymbolIcon(SymbolRegular.ArrowDownload16), new TimeSpan(0, 0, 0, 2)), System.Windows.Threading.DispatcherPriority.ContextIdle);
                                sprdFlashUtils.WritePartition(partName, File.OpenRead(file));
                                Application.Current.Dispatcher.BeginInvoke(() => snackbarService.Show("Fdl2阶段分区操作", $"{partName}分区写入完成", ControlAppearance.Info, new SymbolIcon(SymbolRegular.ArrowDownload16), new TimeSpan(0, 0, 0, 2)), System.Windows.Threading.DispatcherPriority.ContextIdle);
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        snackbarService.Show("Fdl2阶段操作失败", $"恢复备份时发生异常！\n错误:{ex.Message}", ControlAppearance.Danger, new SymbolIcon(SymbolRegular.ErrorCircle12), new TimeSpan(0, 0, 0, 6));
                        await CheckConfirm("恢复备份失败", $"恢复备份时发生异常！\n错误:{ex.Message}", "忽略", string.Empty);
                    }
                    isActing = false;
                    btn.IsEnabled = true;
                }
            }
        }
        public async void FactoryResetButton_Click(object sender)
        {
            if (isActing)
            {
                snackbarService.Show("警告", $"当前正在进行其他操作，无法执行此操作!", ControlAppearance.Caution, new SymbolIcon(SymbolRegular.Info12), new TimeSpan(0, 0, 0, 3));
                return;
            }
            isActing = true;
            try
            {
                await Task.Run(() => sprdFlashUtils.ResetToCustomMode(CustomModesToReset.FactoryReset));
            }
            catch (Exception ex)
            {
                snackbarService.Show("Fdl2阶段操作失败", $"启用开机自动恢复出厂设置时发生异常！\n错误:{ex.Message}", ControlAppearance.Danger, new SymbolIcon(SymbolRegular.ErrorCircle12), new TimeSpan(0, 0, 0, 6));
                await CheckConfirm("操作失败", $"启用开机自动恢复出厂设置时发生异常！\n错误:{ex.Message}", "忽略", string.Empty);
            }
            isActing = false;
            snackbarService.Show("操作成功", "已尝试启用开机自动恢复出厂设置", ControlAppearance.Success, new SymbolIcon(SymbolRegular.Save16), new TimeSpan(0, 0, 0, 2));
        }
        public async void ResetToRecoveryButton_Click(object sender)
        {
            if (isActing)
            {
                snackbarService.Show("警告", $"当前正在进行其他操作，无法执行此操作!", ControlAppearance.Caution, new SymbolIcon(SymbolRegular.Info12), new TimeSpan(0, 0, 0, 3));
                return;
            }
            isActing = true;
            try
            {
                await Task.Run(() => sprdFlashUtils.ResetToCustomMode(CustomModesToReset.Recovery));
            }
            catch (Exception ex)
            {
                snackbarService.Show("Fdl2阶段操作失败", $"启用开机自动进入Rec时发生异常！\n错误:{ex.Message}", ControlAppearance.Danger, new SymbolIcon(SymbolRegular.ErrorCircle12), new TimeSpan(0, 0, 0, 6));
                await CheckConfirm("操作失败", $"启用开机自动进入Rec时发生异常！\n错误:{ex.Message}", "忽略", string.Empty);
            }
            isActing = false;
            snackbarService.Show("操作成功", "已尝试开机自动进入Recovery模式", ControlAppearance.Success, new SymbolIcon(SymbolRegular.Save16), new TimeSpan(0, 0, 0, 2));
        }
        public async void ResetToFastbootdButton_Click(object sender)
        {
            if (sender is Wpf.Ui.Controls.Button button)
            {
                if (isActing)
                {
                    snackbarService.Show("警告", $"当前正在进行其他操作，无法执行此操作!", ControlAppearance.Caution, new SymbolIcon(SymbolRegular.Info12), new TimeSpan(0, 0, 0, 3));
                    return;
                }
                isActing = true;
                button.IsEnabled = false;
                try
                {
                    await Task.Run(() => sprdFlashUtils.ResetToCustomMode(CustomModesToReset.Fastboot));
                }
                catch (Exception ex)
                {
                    snackbarService.Show("Fdl2阶段操作失败", $"启用开机时进入fastboot时发生异常！\n错误:{ex.Message}", ControlAppearance.Danger, new SymbolIcon(SymbolRegular.ErrorCircle12), new TimeSpan(0, 0, 0, 6));
                    await CheckConfirm("启用开机时进入fastboot失败", $"启用开机时进入fastboot时发生异常！\n错误:{ex.Message}", "忽略", string.Empty);
                }
                isActing = false;
                button.IsEnabled = true;
                snackbarService.Show("操作成功", "已尝试开机自动进入Fastbootd模式", ControlAppearance.Success, new SymbolIcon(SymbolRegular.Save16), new TimeSpan(0, 0, 0, 2));
            }
        }
        public async void EnableDmButton_Click(object sender)
        {
            if (sender is Wpf.Ui.Controls.Button button)
            {

                if (isActing)
                {
                    snackbarService.Show("警告", $"当前正在进行其他操作，无法执行此操作!", ControlAppearance.Caution, new SymbolIcon(SymbolRegular.Info12), new TimeSpan(0, 0, 0, 3));
                    return;
                }
                isActing = true;
                button.IsEnabled = false;
                try
                {
                    await Task.Run(() => sprdFlashUtils.SetDmVerityStatus(true, GetPartitionListWithoutSplloader()));

                }
                catch (Exception ex)
                {
                    snackbarService.Show("Fdl2阶段操作失败", $"启用DM-Verity时发生异常！\n错误:{ex.Message}", ControlAppearance.Danger, new SymbolIcon(SymbolRegular.ErrorCircle12), new TimeSpan(0, 0, 0, 6));
                    await CheckConfirm("启用DM-Verity失败", $"启用DM-Verity时发生异常！\n错误:{ex.Message}", "忽略", string.Empty);
                }
                isActing = false;
                button.IsEnabled = true;
                snackbarService.Show("操作成功", "已尝试启用DM-Verity", ControlAppearance.Success, new SymbolIcon(SymbolRegular.Save16), new TimeSpan(0, 0, 0, 2));
            }
        }
        public async void DisableDmButton_Click(object sender)
        {
            if (sender is Wpf.Ui.Controls.Button button)
            {
                if (isActing)
                {
                    snackbarService.Show("警告", $"当前正在进行其他操作，无法执行此操作!", ControlAppearance.Caution, new SymbolIcon(SymbolRegular.Info12), new TimeSpan(0, 0, 0, 3));
                    return;
                }
                isActing = true;
                button.IsEnabled = false;
                try
                {
                    await Task.Run(() => sprdFlashUtils.SetDmVerityStatus(false, GetPartitionListWithoutSplloader()));
                }
                catch (Exception ex)
                {
                    snackbarService.Show("Fdl2阶段操作失败", $"禁用DM-Verity时发生异常！\n错误:{ex.Message}", ControlAppearance.Danger, new SymbolIcon(SymbolRegular.ErrorCircle12), new TimeSpan(0, 0, 0, 6));
                    await CheckConfirm("禁用DM-Verity失败", $"禁用DM-Verity时发生异常！\n错误:{ex.Message}", "忽略", string.Empty);
                }
                isActing = false;
                button.IsEnabled = true;
                snackbarService.Show("操作成功", "已尝试禁用DM-Verity", ControlAppearance.Success, new SymbolIcon(SymbolRegular.Save16), new TimeSpan(0, 0, 0, 2));
            }
        }
        public async void SetActiveButton_Click(object sender, SlotToSetActive slot)
        {
            if (sender is Wpf.Ui.Controls.Button button)
            {
                if (isActing)
                {
                    snackbarService.Show("警告", $"当前正在进行其他操作，无法执行此操作!", ControlAppearance.Caution, new SymbolIcon(SymbolRegular.Info12), new TimeSpan(0, 0, 0, 3));
                    return;
                }
                isActing = true;
                button.IsEnabled = false;
                try
                {
                    await Task.Run(() => sprdFlashUtils.SetActiveSlot(slot));
                }
                catch (Exception ex)
                {
                    snackbarService.Show("Fdl2阶段操作失败", $"设置{slot}为活动槽位时发生异常！\n错误:{ex.Message}", ControlAppearance.Danger, new SymbolIcon(SymbolRegular.ErrorCircle12), new TimeSpan(0, 0, 0, 6));
                    await CheckConfirm($"设置{slot}为活动槽位失败", $"设置{slot}为活动槽位时发生异常！\n错误:{ex.Message}", "忽略", string.Empty);
                }
                isActing = false;
                button.IsEnabled = true;
                snackbarService.Show("操作成功", $"已尝试设置{slot}为活动槽位", ControlAppearance.Success, new SymbolIcon(SymbolRegular.Save16), new TimeSpan(0, 0, 0, 2));
            }
        }
    }
}
