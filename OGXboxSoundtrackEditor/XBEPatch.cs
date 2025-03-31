using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OGXboxSoundtrackEditor
{
    public partial class XBEPatch : Form
    {
        public XBEPatch()
        {
            Application.EnableVisualStyles();
            InitializeComponent();

            if (Properties.Settings.Default.MusicDrive == "G")
            {
                radioButtonG.Checked = true;
            }
        }

        private void SetStatus(string text)
        {
            labelStatus.Invoke((MethodInvoker)delegate {
                labelStatus.Text = text;
            });
        }

        private string HexString(string hex)
        {
            return BitConverter.ToString(Encoding.Default.GetBytes(hex)).Replace("-", " ");
        }

        private void PatchXBE(string file)
        {
            string XBEMagic = "58-42-45-48";

            using (BinaryReader br = new BinaryReader(File.OpenRead(file)))
            {
                string XBEMagicRead = BitConverter.ToString(br.ReadBytes(0x04));
                if (XBEMagicRead != XBEMagic)
                {
                    SetStatus("File not an XBE");
                    MessageBox.Show("The file does not appear to be an XBE.", "Not an XBE", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            SetStatus("Patching...");

            int PartitionNumber;

            if (radioButtonF.Checked == true)
            {
                PartitionNumber = 6;
            }
            else
            {
                PartitionNumber = 7;
            }

            string[] OldPaths = {
                "5C 44 65 76 69 63 65 5C 48 61 72 64 64 69 73 6B 30 5C ?? 61 72 74 69 74 69 6F 6E ?? 5C 54 44 41 54 41 5C 46 46 46 45 30 30 30 30 5C 4D 55 53 49 43 5C", // \Device\Harddisk0\(P|p)artition?\TDATA\FFFE0000\MUSIC\
                "5C 44 65 76 69 63 65 5C 48 61 72 64 64 69 73 6B 30 5C ?? 61 72 74 69 74 69 6F 6E ?? 5C 54 44 41 54 41 5C 46 46 46 45 30 30 30 30 5C 4D 55 53 49 43 5C 53 54 2E 44 42" // \Device\Harddisk0\(P|p)artition?\TDATA\FFFE0000\MUSIC\ST.DB
            };
            string[] NewPaths = {
                HexString($"\\Device\\Harddisk0\\Partition{PartitionNumber}\\TDATA\\FFFE0000\\MUSIC\\"),
                HexString($"\\Device\\Harddisk0\\Partition{PartitionNumber}\\TDATA\\FFFE0000\\MUSIC\\ST.DB")
            };

            if (PatchUtility.SearchAndReplace(file, OldPaths, NewPaths))
            {
                SetStatus("Patched!");
                MessageBox.Show("XBE patched!", "Patched XBE", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                SetStatus("Not patched");
                MessageBox.Show("XBE not patched. This title may not support custom soundtracks.", "Not Patched", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Title = "Choose an XBE";
                ofd.Filter = "Xbox Executable (*.xbe)|*.xbe";

                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    Task.Run(() => PatchXBE(ofd.FileName));
                }
            }
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
