using System;
using System.Reflection;
using System.Windows.Forms;

namespace OGXboxSoundtrackEditor
{
    partial class AboutForm : Form
    {
        public AboutForm()
        {
            Application.EnableVisualStyles();
            InitializeComponent();
            this.labelVersion.Text = String.Format("Version {0}", ProductVersion);
        }

        private void buttonOk_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
