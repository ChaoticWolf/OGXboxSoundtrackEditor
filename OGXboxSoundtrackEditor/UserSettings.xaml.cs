using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace OGXboxSoundtrackEditor
{
    public partial class UserSettings : Window
    {
        public string outputFolder;
        public string ftpIpAddress;
        public string ftpUsername;
        public string ftpPassword;
        public int bitrate;

        public UserSettings()
        {
            InitializeComponent();

            outputFolder = Properties.Settings.Default.outputFolder;
            ftpIpAddress = Properties.Settings.Default.ftpIpAddress;
            ftpUsername = Properties.Settings.Default.ftpUsername;
            ftpPassword = Properties.Settings.Default.ftpPassword;
            bitrate = Properties.Settings.Default.bitrate;
            txtOutputDirectory.Text = outputFolder;
            txtIpAddress.Text = ftpIpAddress;
            txtUsername.Text = ftpUsername;
            txtPassword.Text = ftpPassword;

            if (bitrate == 96000)
            {
                cboBitrate.SelectedIndex = 0;
            }
            else if (bitrate == 128000)
            {
                cboBitrate.SelectedIndex = 1;
            }
            else if (bitrate == 192000)
            {
                cboBitrate.SelectedIndex = 2;
            }
            else if (bitrate == 256000)
            {
                cboBitrate.SelectedIndex = 3;
            }
            else if (bitrate == 320000)
            {
                cboBitrate.SelectedIndex = 4;
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {

        }

        private void btnOK_Click(object sender, RoutedEventArgs e)
        {
            //Verify output folder
            if (!Directory.Exists(txtOutputDirectory.Text)) {
                System.Windows.MessageBox.Show("Invalid output directory.", "Output Directory Invalid", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            
            //Verify IP
            if (!Regex.IsMatch(txtIpAddress.Text.Trim(), "^((25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\\.){3}(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$"))
            {
                System.Windows.MessageBox.Show("Invalid IP.", "IP Invalid", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            Properties.Settings.Default.outputFolder = txtOutputDirectory.Text;
            Properties.Settings.Default.ftpIpAddress = txtIpAddress.Text.Trim();
            Properties.Settings.Default.ftpUsername = txtUsername.Text;
            Properties.Settings.Default.ftpPassword = txtPassword.Text;

            if (cboBitrate.SelectedIndex == 0)
            {
                Properties.Settings.Default.bitrate = 96000;
            }
            else if (cboBitrate.SelectedIndex == 1)
            {
                Properties.Settings.Default.bitrate = 128000;
            }
            else if (cboBitrate.SelectedIndex == 2)
            {
                Properties.Settings.Default.bitrate = 192000;
            }
            else if (cboBitrate.SelectedIndex == 3)
            {
                Properties.Settings.Default.bitrate = 256000;
            }
            else if (cboBitrate.SelectedIndex == 4)
            {
                Properties.Settings.Default.bitrate = 320000;
            }

            Properties.Settings.Default.Save();
            
            DialogResult = true;
        }

        private void btnBrowse_Click(object sender, RoutedEventArgs e)
        {
            FolderBrowserDialog fDialog = new FolderBrowserDialog();
            if (fDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                txtOutputDirectory.Text = fDialog.SelectedPath;
            }
        }
    }
}
