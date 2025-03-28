using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;

namespace OGXboxSoundtrackEditor
{
    public partial class UserSettings : Window
    {
        public UserSettings()
        {
            InitializeComponent();

            string outputFolder = Properties.Settings.Default.outputFolder;
            string ftpIpAddress = Properties.Settings.Default.ftpIpAddress;
            string ftpUsername = Properties.Settings.Default.ftpUsername;
            string ftpPassword = Properties.Settings.Default.ftpPassword;
            int ftpPort = Properties.Settings.Default.ftpPort;
            bool ftpActiveMode = Properties.Settings.Default.ActiveMode;
            int bitrate = Properties.Settings.Default.bitrate;
            string MusicDrive = Properties.Settings.Default.MusicDrive;

            txtOutputDirectory.Text = outputFolder;
            txtIpAddress.Text = ftpIpAddress;
            txtUsername.Text = ftpUsername;
            txtPassword.Text = ftpPassword;
            intPort.Value = ftpPort;
            if (ftpActiveMode)
            {
                cbActiveMode.IsChecked = true;
            }
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
            if (MusicDrive == "E")
            {
                cboMusicDrive.SelectedIndex = 0;
            }
            else if (MusicDrive == "F")
            {
                cboMusicDrive.SelectedIndex = 1;
            }
            else if (MusicDrive == "G")
            {
                cboMusicDrive.SelectedIndex = 2;
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

            if (cboBitrate.SelectedIndex == 2 || cboBitrate.SelectedIndex == 3 || cboBitrate.SelectedIndex == 4)
            {
                MessageBoxResult DialogResult = System.Windows.MessageBox.Show("Please note that some games may have issues playing songs that are more than 128 kbps. Continue?", "Higher Bitrate", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (DialogResult == MessageBoxResult.No)
                {
                    return;
                }
            }

            Properties.Settings.Default.outputFolder = txtOutputDirectory.Text;
            Properties.Settings.Default.ftpIpAddress = txtIpAddress.Text.Trim();
            Properties.Settings.Default.ftpUsername = txtUsername.Text;
            Properties.Settings.Default.ftpPassword = txtPassword.Text;
            Properties.Settings.Default.ftpPort = intPort.Value;
            if (cbActiveMode.IsChecked == true)
            {
                Properties.Settings.Default.ActiveMode = true;
            }
            else
            {
                Properties.Settings.Default.ActiveMode = false;
            }
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
            if (cboMusicDrive.SelectedIndex == 0)
            {
                Properties.Settings.Default.MusicDrive = "E";
            }
            else if (cboMusicDrive.SelectedIndex == 1)
            {
                Properties.Settings.Default.MusicDrive = "F";
            }
            else if (cboMusicDrive.SelectedIndex == 2)
            {
                Properties.Settings.Default.MusicDrive = "G";
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

        private void txtIpAddress_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!string.IsNullOrEmpty(txtIpAddress.Text))
            {
                btnOK.IsEnabled = true;
            }
            else
            {
                btnOK.IsEnabled = false;
            }
        }
    }
}
