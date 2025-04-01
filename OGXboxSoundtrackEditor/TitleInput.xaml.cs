using System;
using System.Windows;
using System.Windows.Controls;

namespace OGXboxSoundtrackEditor
{
    /// <summary>
    /// Interaction logic for TitleInput.xaml
    /// </summary>
    public partial class TitleInput : Window
    {
        public TitleInput(string message, string title, int limit)
        {
            InitializeComponent();

            lblMessage.Content = message;
            Title = title;
            txtTrackTitle.MaxLength = limit;
        }

        private void btnOk_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        private void txtTrackTitle_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!string.IsNullOrEmpty(txtTrackTitle.Text))
            {
                btnOk.IsEnabled = true;
            }
            else
            {
                btnOk.IsEnabled = false;
            }
        }

        private void Window_ContentRendered(object sender, EventArgs e)
        {
            txtTrackTitle.SelectAll();
            txtTrackTitle.Focus();
        }
        
        public string TrackTitle
        {
            get { return txtTrackTitle.Text; }
        }
    }
}
