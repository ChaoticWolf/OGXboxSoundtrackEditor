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
            this.labelVersion.Text = String.Format("Version {0}", AssemblyVersion);
        }

        #region Assembly Attribute Accessors

        public string AssemblyVersion
        {
            get
            {
                return Assembly.GetExecutingAssembly().GetName().Version.ToString();
            }
        }
        #endregion

        private void buttonOk_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
