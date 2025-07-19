using Microsoft.Win32;
using SPRDClient.Utils;
using System.Windows;
using System.Windows.Controls;

namespace SPRDClient.Pages
{
    public partial class SettingsPage : Page
    {
        public FlashModel flashModel;
        public SettingsPage(FlashModel flashModel)
        {
            InitializeComponent();
            this.flashModel = flashModel;
            DataContext = this.flashModel;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            OpenFolderDialog openFolderDialog = new OpenFolderDialog();
            openFolderDialog.DefaultDirectory = Environment.CurrentDirectory;
            if (openFolderDialog.ShowDialog() == true)
            {
                flashModel.SaveFileDirectory = openFolderDialog.FolderName;
            }
        }
    }
}
