using Microsoft.Win32;
using SPRDClient.Utils;
using SPRDClientCore;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace SPRDClient.Pages
{
    public partial class SeniorPage : Page, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public string WritePartitionName
        {
            get => field;
            set
            {
                if (value.Length > 36)
                    return;
                field = value;
                OnPropertyChanged(nameof(WritePartitionName));
            }
        } = string.Empty;
        public string WriteImageFilePath
        {
            get => field;
            set
            {
                field = value;
                OnPropertyChanged(nameof(WriteImageFilePath));
            }
        } = string.Empty;
        public string WriteOffset
        {
            get => field;
            set
            {
                field = value;
                try
                {
                    writeOffset = SprdFlashUtils.StringToSize(field);
                }
                catch (Exception e)
                {
                    flashModel.snackbarService.Show("输入参数错误", $"{e.Message}", ControlAppearance.Danger, new SymbolIcon(SymbolRegular.Info12), new TimeSpan(0, 0, 0, 3));

                }
                OnPropertyChanged(nameof(WriteOffset));
            }
        } = string.Empty;
        private ulong writeOffset;
        public string ReadPartitionName
        {
            get => field;
            set
            {
                if (value.Length > 36)
                    return;
                field = value;
                if (!flashModel.isActing)
                    PartExist = flashModel.sprdFlashUtils.CheckPartitionExist(value);
                OnPropertyChanged(nameof(ReadPartitionName));
            }
        } = string.Empty;
        public string ReadSize
        {
            get => field;
            set
            {
                field = value;
                try
                {
                    readSize = SprdFlashUtils.StringToSize(field);
                }
                catch (Exception e)
                {
                    flashModel.snackbarService.Show("输入参数错误", $"{e.Message}", ControlAppearance.Danger, new SymbolIcon(SymbolRegular.Info12), new TimeSpan(0, 0, 0, 3));

                }
                OnPropertyChanged(nameof(ReadSize));
            }
        } = string.Empty;
        private ulong readSize;
        public string ReadOffset
        {
            get => field;
            set
            {
                field = value;
                try
                {
                    readOffset = SprdFlashUtils.StringToSize(field);
                }
                catch (Exception e)
                {
                    flashModel.snackbarService.Show("输入参数错误", $"{e.Message}", ControlAppearance.Danger, new SymbolIcon(SymbolRegular.Info12), new TimeSpan(0, 0, 0, 3));

                }
                OnPropertyChanged(nameof(ReadOffset));
            }
        } = string.Empty;
        private ulong readOffset;
        public bool PartExist
        {
            get => field;
            set
            {
                field = value;
                PartMessage = value ? "" : "分区不存在";
                OnPropertyChanged(nameof(PartExist));
            }

        } = false;
        public bool IsUsingNewFdl2
        {
            get => flashModel.IsUsingNewFdl2;
            set => OnPropertyChanged(nameof(IsUsingNewFdl2));
        }
        public string PartMessage
        {
            get => field;
            set
            {
                field = value;
                OnPropertyChanged(nameof(PartMessage));
            }
        } = string.Empty;
        public string SaveFileDirectory
        {
            get => flashModel.SaveFileDirectory;
            set
            {
                flashModel.SaveFileDirectory = value;
                OnPropertyChanged(nameof(SaveFileDirectory));
            }
        }
        private FlashModel flashModel;
        public SeniorPage(FlashModel flashModel)
        {
            InitializeComponent();
            this.flashModel = flashModel;
            DataContext = this;
        }
        private void OnPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));

        }
        private void BrowserButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Wpf.Ui.Controls.Button button)
            {
                if (button.Tag is string tag)
                {
                    switch (tag)
                    {
                        case "read":
                            OpenFolderDialog openFolderDialog = new OpenFolderDialog();
                            openFolderDialog.DefaultDirectory = Environment.CurrentDirectory;
                            if (openFolderDialog.ShowDialog() == true)
                            {

                                flashModel.SaveFileDirectory = openFolderDialog.FolderName;
                            }
                            break;
                        case "write":
                            OpenFileDialog openFileDialog = new OpenFileDialog();
                            openFileDialog.Filter = "镜像文件|*.img;*.bin|所有文件|*.*";
                            if (openFileDialog.ShowDialog() == true)
                            {
                                WriteImageFilePath = openFileDialog.FileName;
                            }
                            break;
                    }
                }
            }
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Wpf.Ui.Controls.Button btn)
                if (btn.Tag is string tag)
                    switch (tag)
                    {
                        case "read":
                            if (string.IsNullOrWhiteSpace(ReadPartitionName)) return;
                            if (readSize == 0)
                            {
                                await flashModel.CustomReadButton_Click(btn, ReadPartitionName, 0, readOffset);
                                return;
                            }
                            await flashModel.CustomReadButton_Click(btn, ReadPartitionName, readSize, readOffset);
                            break;
                        case "write":
                            await flashModel.CustomWriteButton_Click(btn, WritePartitionName, WriteImageFilePath, 0);
                            break;
                    }
        }

        private void TextBox_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }

        private void TextBox_Drop(object sender, DragEventArgs e)
        {
            string[]? temp = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (temp != null)
                if (sender is Wpf.Ui.Controls.TextBox textbox)
                    Dispatcher.Invoke(() =>
                    {
                        textbox.Text = temp[0];
                    });

        }

        private async void Button_Click_1(object sender, RoutedEventArgs e)
        {
            if (flashModel.isActing) return;
            if (sender is Wpf.Ui.Controls.Button btn)
                if (btn.Tag is string tag)
                    switch (tag)
                    {
                        case "repartition":
                            OpenFileDialog dialog = new()
                            {
                                DefaultDirectory = Environment.CurrentDirectory,
                                Filter = "分区表文件 (*.xml)|*.xml|所有文件 (*.*)|*.*"
                            };
                            if (dialog.ShowDialog() == true)
                            {
                                string xmlContent = File.ReadAllText(dialog.FileName);
                                List<Partition> partitions = SprdFlashUtils.LoadPartitionsXml(xmlContent);
                                if (partitions.Count > 0)
                                    await Task.Run(() => flashModel.sprdFlashUtils.Repartition(partitions));
                                flashModel.snackbarService.Show("重新分区成功", $"已重新分区（分区数：{partitions.Count}）", ControlAppearance.Success, new SymbolIcon(SymbolRegular.Save16), new TimeSpan(0, 0, 0, 4));
                            }
                            break;
                        case "disable dm-verity":
                            flashModel.DisableDmButton_Click(sender, e);
                            break;
                        case "enable dm-verity":
                            flashModel.EnableDmButton_Click(sender, e);
                            break;
                        case "recovery":
                            flashModel.ResetToRecoveryButton_Click(sender,e);
                            break;
                        case "fastbootd":
                            flashModel.ResetToFastbootdButton_Click(sender, e);
                            break;
                        case "factory reset": 
                            flashModel.FactoryResetButton_Click(sender, e);
                            break;
                    }
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            IsUsingNewFdl2 = flashModel.IsUsingNewFdl2;
        }
    }
}
