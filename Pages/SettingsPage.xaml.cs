using Microsoft.Win32;
using SPRDClient.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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
using System.Windows.Navigation;
using System.Windows.Shapes;

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
