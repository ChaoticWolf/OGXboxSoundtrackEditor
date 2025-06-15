using FluentFTP;
using FluentFTP.Exceptions;
using Microsoft.Win32;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using WMPLib;

namespace OGXboxSoundtrackEditor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        FtpClient FTP;

        // wma stuff
        WindowsMediaPlayer wmp = new WindowsMediaPlayer();

        // saved settings variables
        string OutputFolder;
        string IPAddress;
        string Username;
        string Password;
        int Port;
        bool ActiveMode;
        string MusicPartition;
        int MusicDrive;
        int bitrate;

        bool blankSoundtrackAdded;
        bool SoundtracksEdited;
        string XboxMusicDirectory;

        List<string> ftpLocalPaths = new List<string>();
        List<string> ftpDestPaths = new List<string>();
        List<string> ftpSoundtrackIds = new List<string>();

        List<string> toDeleteFiles = new List<string>();

        // stuff not in the db file
        ObservableCollection<Soundtrack> soundtracks = new ObservableCollection<Soundtrack>();
        int songGroupCount = 0;
        int paddingBetween = 0;
        long fileLength = 0;

        //header
        int magic;
        int numSoundtracks;
        int nextSoundtrackId;
        int[] soundtrackIds = new int[100];
        int nextSongId;
        byte[] padding = new byte[96];

        public MainWindow()
        {
            InitializeComponent();

            LoadSettings();
        }

        private void LoadSettings()
        {
            OutputFolder = Properties.Settings.Default.OutputFolder;
            IPAddress = Properties.Settings.Default.IPAddress;
            Username = Properties.Settings.Default.Username;
            Password = Properties.Settings.Default.Password;
            Port = Properties.Settings.Default.Port;
            ActiveMode = Properties.Settings.Default.ActiveMode;
            MusicPartition = Properties.Settings.Default.MusicPartition;
            MusicDrive = Properties.Settings.Default.MusicDrive;
            bitrate = Properties.Settings.Default.bitrate;
        }

        private void SetStatus(string text)
        {
            Dispatcher.Invoke(new Action(() => {
                txtStatus.Text = text;
            }));
        }

        private bool ConnectToXbox()
        {
            //If the user hasn't set an IP, prompt to set one
            if (string.IsNullOrEmpty(IPAddress))
            {
                bool IPSet = Dispatcher.Invoke(new Func<bool>(() =>
                {
                    UserSettings Settings = new UserSettings();
                    if (Settings.ShowDialog() != true)
                    {
                        return false;
                    }

                    return true;
                }));

                if (!IPSet)
                {
                    return false;
                }

                LoadSettings();
            }

            FTP = new FtpClient(IPAddress, Username, Password, Port);
            if (ActiveMode)
            {
                FTP.Config.DataConnectionType = FtpDataConnectionType.PORT;
            }

            //Connect to the Xbox
            SetStatus("Connecting to Xbox " + IPAddress + "...");

            try
            {
                FTP.Connect();
                return true;
            }
            catch (FtpAuthenticationException ex)
            {
                SetStatus("Couldn't login to Xbox");
                MessageBox.Show("Could not login to the Xbox.\n" + ex.Message, "Couldn't Login", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                SetStatus("Could not connect to Xbox");
                MessageBox.Show("Could not connect to the Xbox.\n" + ex.Message, "Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            
            FTP.Disconnect();
            return false;
        }

        private bool GetMusicWorkingDirectory()
        {
            SetStatus("Searching for music directory...");

            XboxMusicDirectory = "";
            var server = FTP.SystemType;

            //Check for Dashlaunch or XeXMenu. Their FTP servers set working directories even if they don't exist
            if (server.Contains("DLiFTPD") || server.Contains("XeXMenu"))
            {
                if (server.Contains("DLiFTPD"))
                {
                    XboxMusicDirectory = "/Hdd/Compatibility/Xbox1/TDATA/FFFE0000/MUSIC/";
                }
                else
                {
                    XboxMusicDirectory = "/Hdd1/Compatibility/Xbox1/TDATA/FFFE0000/MUSIC/";
                }

                //We'll send this in case it doesn't exist
                FTP.CreateDirectory(XboxMusicDirectory);
                return true;
            }

            string[] XboxDrives =
            {
                MusicPartition, //E - Used by most dashboards
                $"HDD{MusicDrive}-{MusicPartition}", //PrometheOS
                $"{MusicPartition}:", //E: - Used by some dashboards like Avalaunch
                "Hdd1" //Xbox 360
            };

            foreach (string XboxDrive in XboxDrives)
            {
                if (FTP.DirectoryExists($"/{XboxDrive}/"))
                {
                    if (XboxDrive != "Hdd1")
                    {
                        XboxMusicDirectory = $"/{XboxDrive}/TDATA/fffe0000/music/";
                    }
                    else
                    {
                        XboxMusicDirectory = "/Hdd1/Compatibility/Xbox1/TDATA/FFFE0000/MUSIC/";
                    }
                    break;
                }
            }

            if (String.IsNullOrEmpty(XboxMusicDirectory))
            {
                SetStatus("Could not detect Xbox");
                MessageBox.Show("Could not determine if the FTP server is an Xbox.", "Xbox Not Detected", MessageBoxButton.OK, MessageBoxImage.Error);
                FTP.Disconnect();
                return false;
            }

            //Check if music directory is on the Xbox
            if (!FTP.DirectoryExists(XboxMusicDirectory))
            {
                if (!FTP.CreateDirectory(XboxMusicDirectory))
                {
                    SetStatus("Couldn't create music directory on Xbox");
                    FTP.Disconnect();
                    return false;
                }
            }

            return true;
        }

        private void NewDb()
        {
            // create header
            magic = 0x00000001;
            numSoundtracks = 0;
            nextSoundtrackId = 0;
            soundtrackIds = new int[100];
            nextSongId = 0;
            padding = new byte[96];

            songGroupCount = 0;
            paddingBetween = 0;

            Dispatcher.Invoke(new Action(() =>
            {
                soundtracks.Clear();
                listSoundtracks.ItemsSource = soundtracks;
            }));
        }

        private void OpenDb(byte[] bytes)
        {
            BinaryReader bReader = new BinaryReader(new MemoryStream(bytes), Encoding.Unicode);
            magic = bReader.ReadInt32();
            numSoundtracks = bReader.ReadInt32();
            nextSoundtrackId = bReader.ReadInt32();
            for (int i = 0; i < 100; i++)
            {
                soundtrackIds[i] = bReader.ReadInt32();
            }
            nextSongId = bReader.ReadInt32();
            for (int i = 0; i < 96; i++)
            {
                padding[i] = bReader.ReadByte();
            }

            for (int i = 0; i < numSoundtracks; i++)
            {
                Soundtrack s = new Soundtrack();
                s.magic = bReader.ReadInt32();
                s.id = bReader.ReadInt32();
                s.numSongs = bReader.ReadUInt32();
                // 12 bytes read
                for (int a = 0; a < 84; a++)
                {
                    s.songGroupIds[a] = bReader.ReadInt32();
                }
                // 336 bytes read
                songGroupCount++;
                for (int a = 1; a < 84; a++)
                {
                    if (s.songGroupIds[a] != 0)
                    {
                        songGroupCount++;
                    }
                }

                s.totalTimeMilliseconds = bReader.ReadInt32();
                // 4 bytes read
                for (int a = 0; a < 64; a++)
                {
                    s.name[a] = bReader.ReadChar();
                }
                bReader.ReadBytes(32);
                // 128 bytes read

                soundtracks.Add(s);
            }

            byte h;
            do
            {
                h = bReader.ReadByte();
                if (h != 0x73)
                {
                    paddingBetween++;
                }
            } while (h != 0x73);
            bReader.ReadBytes(3);

            for (int i = 0; i < songGroupCount; i++)
            {
                SongGroup sGroup = new SongGroup();
                if (i == 0)
                {
                    sGroup.magic = 0x00031073;
                }
                else
                {
                    sGroup.magic = bReader.ReadInt32();
                }
                sGroup.soundtrackId = bReader.ReadInt32();
                sGroup.id = bReader.ReadInt32();
                sGroup.padding = bReader.ReadInt32();
                for (int a = 0; a < 6; a++)
                {
                    sGroup.songId[a] = bReader.ReadInt16();
                    bReader.ReadInt16();
                }
                for (int a = 0; a < 6; a++)
                {
                    sGroup.songTimeMilliseconds[a] = bReader.ReadInt32();
                }
                for (int a = 0; a < 6; a++)
                {
                    char[] newArray = new char[32];
                    for (int b = 0; b < 32; b++)
                    {
                        newArray[b] = bReader.ReadChar();
                    }
                    sGroup.songNames[a] = newArray;
                }
                for (int a = 0; a < 64; a++)
                {
                    sGroup.paddingChar[a] = bReader.ReadByte();
                }
                for (int p = 0; p < soundtracks.Count; p++)
                {
                    if (soundtracks[p].id == sGroup.soundtrackId)
                    {
                        soundtracks[p].songGroups.Add(sGroup);
                    }
                }
            }

            bReader.Close();
        }

        private int GetDbSize()
        {
            int dbSize = 0;
            dbSize = 51200 - (numSoundtracks * 512);
            dbSize += 512 + (numSoundtracks * 512);
            foreach (Soundtrack sTrack in soundtracks)
            {
                dbSize = dbSize + (sTrack.songGroups.Count * 512);
            }
            return dbSize;
        }

        private byte[] GetDbBytes()
        {
            CalculateSongGroupIndexes();
            CalculateSoundtrackIds();

            byte[] dbBytes = new byte[GetDbSize()];

            MemoryStream mStream = new MemoryStream(dbBytes);
            BinaryWriter bWriter = new BinaryWriter(mStream, Encoding.Unicode);

            // header
            bWriter.Write(magic);
            bWriter.Write(numSoundtracks);
            bWriter.Write(nextSoundtrackId);
            for (int i = 0; i < 100; i++)
            {
                bWriter.Write(soundtrackIds[i]);
            }
            bWriter.Write(nextSongId);
            for (int i = 0; i < 96; i++)
            {
                bWriter.Write(padding[i]);
            }

            // soundtracks
            foreach (Soundtrack s in soundtracks)
            {
                bWriter.Write(s.magic);
                bWriter.Write(s.id);
                bWriter.Write(s.numSongs);
                for (int i = 0; i < 84; i++)
                {
                    bWriter.Write(s.songGroupIds[i]);
                }
                bWriter.Write(s.totalTimeMilliseconds);
                for (int i = 0; i < 64; i++)
                {
                    bWriter.Write(s.name[i]);
                }
                for (int i = 0; i < 32; i++)
                {
                    bWriter.Write(s.padding[i]);
                }
            }

            paddingBetween = 51200 - (numSoundtracks * 512);
            for (int i = 0; i < paddingBetween; i++)
            {
                bWriter.Write((byte)0);
            }

            // song groups
            foreach (Soundtrack t in soundtracks)
            {
                foreach (SongGroup s in t.songGroups)
                {
                    bWriter.Write(s.magic);
                    bWriter.Write(s.soundtrackId);
                    bWriter.Write(s.id);
                    bWriter.Write(s.padding);
                    for (int i = 0; i < 6; i++)
                    {
                        bWriter.Write((short)s.songId[i]);
                        if (s.songTimeMilliseconds[i] != 0)
                        {
                            bWriter.Write((short)s.soundtrackId);
                        }
                        else
                        {
                            bWriter.Write((short)0);
                        }
                    }
                    for (int i = 0; i < 6; i++)
                    {
                        bWriter.Write(s.songTimeMilliseconds[i]);
                    }
                    for (int i = 0; i < 6; i++)
                    {
                        if (s.songNames[i] == null)
                        {
                            bWriter.Write(new char[32]);
                        }
                        else
                        {
                            bWriter.Write(s.songNames[i]);
                        }
                    }
                    for (int i = 0; i < 64; i++)
                    {
                        bWriter.Write(s.paddingChar[i]);
                    }
                }
            }

            bWriter.Flush();
            bWriter.Close();
            return dbBytes;
        }

        private void CalculateSongGroupIndexes()
        {
            int curIndex = 0;
            foreach (Soundtrack sTrack in soundtracks)
            {
                sTrack.songGroupIds = new int[84];

                for (int i = 0; i < sTrack.songGroups.Count; i++)
                {
                    sTrack.songGroupIds[i] = curIndex;

                    curIndex++;
                }
            }
        }

        private void CalculateSoundtrackIds()
        {
            soundtrackIds = new int[100];

            for (int i = 0; i < soundtracks.Count; i++)
            {
                soundtrackIds[i] = soundtracks[i].id;
            }
        }

        private void ReorderSongsInGroups(Soundtrack soundtrack)
        {
            if (soundtrack.allSongs.Count == 0)
            {
                return;
            }

            soundtrack.songGroups.Clear();
            int totalSongGroups;
            if (soundtrack.allSongs.Count > 6)
            {
                totalSongGroups = soundtrack.allSongs.Count / 6;
                if (soundtrack.allSongs.Count % 6 > 0)
                {
                    totalSongGroups++;
                }
            }
            else
            {
                totalSongGroups = 1;
            }

            for (int i = 0; i < totalSongGroups; i++)
            {
                SongGroup tempGroup = new SongGroup();
                tempGroup.magic = 0x00031073;
                tempGroup.soundtrackId = soundtrack.id;
                tempGroup.id = i;
                tempGroup.padding = 0x00000001;

                soundtrack.songGroups.Add(tempGroup);
            }

            int currentIndex = 0;
            int currentSongGroupIndex = 0;
            foreach (Song song in soundtrack.allSongs)
            {
                if (currentIndex == 6)
                {
                    currentSongGroupIndex++;
                    currentIndex = 0;
                }

                char[] songName = new char[32];
                song.Name.CopyTo(0, songName, 0, song.Name.Length);
                soundtrack.songGroups[currentSongGroupIndex].songId[currentIndex] = song.id;
                soundtrack.songGroups[currentSongGroupIndex].songNames[currentIndex] = songName;
                soundtrack.songGroups[currentSongGroupIndex].songTimeMilliseconds[currentIndex] = song.TimeMs;

                currentIndex++;
            }
        }

        private void FindNextSongId()
        {
            int curSongId = 0;
            while (true)
            {
                bool foundCurSongId = false;
                foreach (Soundtrack sTrack in soundtracks)
                {
                    foreach (SongGroup sGroup in sTrack.songGroups)
                    {
                        for (int i = 0; i < 6; i++)
                        {
                            if (sGroup.songTimeMilliseconds[i] > 0)
                            {
                                if (sGroup.songId[i] == curSongId)
                                {
                                    foundCurSongId = true;
                                }
                            }
                        }
                    }
                }

                if (!foundCurSongId)
                {
                    nextSongId = curSongId;
                    break;
                }
                curSongId++;
            }
        }

        private void FindNextSoundtrackId()
        {
            int curSoundtrackId = 0;
            while (true)
            {
                bool foundCurSoundtrackId = false;
                foreach (Soundtrack sTrack in soundtracks)
                {
                    if (sTrack.id == curSoundtrackId)
                    {
                        foundCurSoundtrackId = true;
                    }
                }

                if (!foundCurSoundtrackId)
                {
                    nextSoundtrackId = curSoundtrackId;
                    break;
                }
                curSoundtrackId++;
            }
        }

        private void RemoveEmptySongGroups(Soundtrack sTrack)
        {
            if (sTrack.songGroups.Count == 0)
            {
                return;
            }
            for (int i = sTrack.songGroups.Count - 1; i >= 0; i--)
            {
                bool isEmpty = false;
                for (int a = 0; a < 6; a++)
                {
                    if (sTrack.songGroups[i].songTimeMilliseconds[a] > 0)
                    {
                        isEmpty = true;
                    }
                }
                if (!isEmpty)
                {
                    sTrack.songGroups.RemoveAt(i);
                }
            }
        }

        private bool ContainsSoundtracks()
        {
            if (soundtracks.Count > 0)
            {
                return true;
            }
            return false;
        }

        private void DeleteTracksFromXbox()
        {
            if (!ConnectToXbox())
            {
                return;
            }

            SetStatus("Deleting soundtracks from Xbox...");

            try
            {
                if (!GetMusicWorkingDirectory())
                {
                    return;
                }

                FTP.SetWorkingDirectory(XboxMusicDirectory);

                //TODO: Using EmptyDirectory and DeleteDirectory only seems to work on XBMC and PrometheOS, get it working for other dashboards
                FTP.EmptyDirectory(XboxMusicDirectory);

                SetStatus("Deleted soundtracks from Xbox");

                SoundtracksEdited = false;
            }
            catch (Exception ex)
            {
                SetStatus("Error deleting soundtracks");
            }

            FTP.Disconnect();
        }

        private async void mnuNew_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult DialogResult = MessageBox.Show("This will delete your entire soundtrack database from the Xbox. Are you sure?", "Delete Soundtrack Database", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (DialogResult != MessageBoxResult.Yes)
            {
                return;
            }

            NewDb();

            btnAddSoundtrack.IsEnabled = true;

            gridMain.IsEnabled = false;

            await Task.Run(() => DeleteTracksFromXbox());
            
            gridMain.IsEnabled = true;
            mnuSaveToXbox.IsEnabled = true;
        }

        private void OpenDbFromXbox()
        {
            if (!ConnectToXbox())
            {
                return;
            }

            SetStatus("Downloading soundtrack database...");

            try
            {
                if (!GetMusicWorkingDirectory())
                {
                    return;
                }

                FTP.SetWorkingDirectory(XboxMusicDirectory);

                //Some FTP servers don't support the NLST command sent by FileExists
                var files = FTP.GetListing(XboxMusicDirectory);
                if (files.All(file => file.Name != "ST.DB"))
                {
                    SetStatus("Database created");
                    NewDb();
                }
                else if (FTP.DownloadBytes(out byte[] DownloadedBytes, "ST.DB"))
                {
                    if (DownloadedBytes.Length == 0)
                    {
                        SetStatus("Database created");
                        NewDb();
                    }
                    else
                    {
                        OpenDb(DownloadedBytes);
                        SetStatus("Soundtrack database loaded");
                    }
                }
                else
                {
                    SetStatus("Couldn't download soundtrack database");
                    FTP.Disconnect();
                    return;
                }

                Dispatcher.Invoke(new Action(() =>
                {
                    mnuSaveToXbox.IsEnabled = true;
                    btnAddSoundtrack.IsEnabled = true;
                    listSoundtracks.ItemsSource = soundtracks;
                }));
            }
            catch (FtpException ex)
            {
                SetStatus("Couldn't retrieve soundtracks");
                MessageBox.Show("Couldn't retrieve soundtracks.\n" + ex.Message, "Couldn't Retrieve Soundtracks", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch
            {
                SetStatus("Error retrieving soundtracks");
            }

            FTP.Disconnect();
        }

        private async void mnuOpenDbFromXbox_Click(object sender, RoutedEventArgs e)
        {
            gridMain.IsEnabled = false;

            await Task.Run(() => OpenDbFromXbox());

            gridMain.IsEnabled = true;
        }

        private void UploadDbToXbox()
        {
            if (!ConnectToXbox())
            {
                return;
            }

            SetStatus("Uploading to Xbox...");

            try
            {
                if (!GetMusicWorkingDirectory())
                {
                    return;
                }

                FTP.SetWorkingDirectory(XboxMusicDirectory);

                FTP.UploadBytes(GetDbBytes(), "ST.DB", FtpRemoteExists.OverwriteInPlace);

                Dispatcher.Invoke(new Action(() =>
                {
                    progressBar.Maximum = ftpDestPaths.Count;
                }));

                for (int i = 0; i < ftpDestPaths.Count; i++)
                {
                    FTP.CreateDirectory(ftpSoundtrackIds[i]);

                    FTP.SetWorkingDirectory(XboxMusicDirectory + ftpSoundtrackIds[i]);

                    FTP.UploadFile(ftpLocalPaths[i], ftpDestPaths[i], FtpRemoteExists.OverwriteInPlace);

                    Dispatcher.Invoke(new Action(() =>
                    {
                        progressBar.Value++;
                    }));
                }

                SetStatus("Uploaded to Xbox");

                ftpDestPaths.Clear();
                ftpLocalPaths.Clear();
                ftpSoundtrackIds.Clear();

                SoundtracksEdited = false;
            }
            catch
            {
                SetStatus("Error uploading changes");
            }
            finally
            {
                Dispatcher.Invoke(new Action(() =>
                {
                    progressBar.Value = 0;
                }));
            }

            FTP.Disconnect();
        }

        private async void mnuSaveToXbox_Click(object sender, RoutedEventArgs e)
        {
            gridMain.IsEnabled = false;

            await Task.Run(() => UploadDbToXbox());

            gridMain.IsEnabled = true;
        }

        private void BackupFromXbox(string zipPath)
        {
            string copyPath = OutputFolder + "\\music\\";

            if (!ConnectToXbox())
            {
                return;
            }

            SetStatus("Backing up from Xbox...");

            try
            {
                if (!GetMusicWorkingDirectory())
                {
                    return;
                }

                FTP.SetWorkingDirectory(XboxMusicDirectory);

                //TODO: Only works with XBMC and PrometheOS, get it working for other dashboards
                FTP.DownloadDirectory(copyPath, XboxMusicDirectory, FtpFolderSyncMode.Update, FtpLocalExists.Overwrite);

                ZipFile.CreateFromDirectory(copyPath, zipPath);

                SetStatus("Backup created from Xbox");
            }
            catch
            {
                SetStatus("Backup error");
                MessageBox.Show("There was an error backing up the soundtracks.", "Backup Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            FTP.Disconnect();
            Directory.Delete(copyPath, true);
        }

        private async void mnuBackupFromXbox_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog sDialog = new SaveFileDialog();
            sDialog.Title = "Create Soundtrack Backup";
            sDialog.Filter = "ZIP Files (*.zip)|*.zip";
            sDialog.FileName = "Xbox Soundtrack Backup - " + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss") + ".zip";
            sDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (sDialog.ShowDialog() != true)
            {
                return;
            }

            gridMain.IsEnabled = false;

            await Task.Run(() => BackupFromXbox(sDialog.FileName));

            gridMain.IsEnabled = true;
        }

        private void UploadBackupToXbox(string zipPath)
        {
            string extractPath = OutputFolder + "\\music\\";

            if (!ConnectToXbox())
            {
                return;
            }

            SetStatus("Uploading backup to Xbox...");

            try
            {
                if (!GetMusicWorkingDirectory())
                {
                    return;
                }

                FTP.SetWorkingDirectory(XboxMusicDirectory);

                if (Directory.Exists(extractPath))
                {
                    Directory.Delete(extractPath, true);
                }

                ZipFile.ExtractToDirectory(zipPath, extractPath);

                FTP.UploadDirectory(extractPath, XboxMusicDirectory, FtpFolderSyncMode.Update, FtpRemoteExists.OverwriteInPlace);

                SetStatus("Backup uploaded to Xbox");
            }
            catch
            {
                SetStatus("Error uploading backup");
                MessageBox.Show("There was an error uploading the soundtrack backup.", "Backup Upload Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Dispatcher.Invoke(new Action(() =>
                {
                    progressBar.Value = 0;
                }));
            }

            FTP.Disconnect();
            Directory.Delete(extractPath, true);
        }

        private bool IsValidSoundtrackBackup(string zipPath)
        {
            try
            {
                using (var zipFile = ZipFile.OpenRead(zipPath))
                {
                    var entries = zipFile.Entries;
                    foreach (var entry in entries)
                    {
                        if (entry.Name.Equals("ST.DB"))
                        {
                            return true;
                        }
                    }

                    SetStatus("No database in backup");
                    MessageBox.Show("A soundtrack database was not found in the ZIP file you selected.", "No database found in backup", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }
            }
            catch (InvalidDataException)
            {
                SetStatus("Invalid ZIP");
                MessageBox.Show("The ZIP file you selected is invalid", "Invalid ZIP", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            catch
            {
                SetStatus("Error reading ZIP file");
                MessageBox.Show("There was an error reading the ZIP file.", "Error Reading ZIP File", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private async void mnuUploadBackupToXbox_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofDialog = new OpenFileDialog();
            ofDialog.Title = "Choose Soundtrack Backup";
            ofDialog.Filter = "ZIP Files (*.zip)|*.zip";
            ofDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            if (ofDialog.ShowDialog() != true)
            {
                return;
            }

            if (!IsValidSoundtrackBackup(ofDialog.FileName))
            {
                return;
            }

            gridMain.IsEnabled = false;

            await Task.Run(() => UploadBackupToXbox(ofDialog.FileName));

            gridMain.IsEnabled = true;
        }

        private void mnuPatchXBE_Click(object sender, RoutedEventArgs e)
        {
            XBEPatch XBEPatch = new XBEPatch();
            XBEPatch.Show();
        }

        private void mnuSettings_Click(object sender, RoutedEventArgs e)
        {
            UserSettings wndSettings = new UserSettings();
            wndSettings.Top = this.Top + 100;
            wndSettings.Left = this.Left + 100;
            if (wndSettings.ShowDialog() == true)
            {
                SetStatus("Settings saved");
                LoadSettings();
            }
        }

        private void mnuAbout_Click(object sender, RoutedEventArgs e)
        {
            using (AboutForm AboutForm = new AboutForm())
            {
                AboutForm.ShowDialog();
            }
        }

        private void mnuExit_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void AddTrackFiles(object paths, int TrackTotal)
        {
            Soundtrack sTrack = null;
            Dispatcher.Invoke(new Action(() =>
            {
                sTrack = (Soundtrack)listSoundtracks.SelectedItem;
            }));

            int soundtrackId = sTrack.id;

            string[] realPaths = (string[])paths;

            Dispatcher.Invoke(new Action(() =>
            {
                progressBar.Maximum = realPaths.Length;
            }));

            int CurrentTrack = 0;

            try
            {
                foreach (string path in realPaths)
                {
                    IWMPMedia mediainfo = wmp.newMedia(path);
                    string title = mediainfo.name.Trim();

                    if (title.Length > 31)
                    {
                        bool NewTitle = Dispatcher.Invoke(new Func<bool>(() =>
                        {
                            TitleInput TitleInput = new TitleInput("Song name \"" + title + "\" is too long. Please enter a new one.", "Edit Song Title", 31);
                            if (TitleInput.ShowDialog() != true)
                            {
                                return false;
                            }

                            title = TitleInput.TrackTitle;
                            return true;
                        }));

                        if (!NewTitle)
                        {
                            TrackTotal--;
                            continue;
                        }
                    }

                    CurrentTrack++;
                    
                    string TrackFormat = Path.GetExtension(path).Replace(".", "").ToUpper();

                    if (TrackFormat == "WMA")
                    {
                        ftpLocalPaths.Add(path);
                    }
                    else
                    {
                        SetStatus($"Converting {TrackFormat} track... ({CurrentTrack} of {TrackTotal})");

                        string wmaOutputPath = OutputFolder + "\\" + nextSongId.ToString("X4") + ".wma";
                        using (MediaFoundationReader reader = new MediaFoundationReader(path))
                        {
                            MediaFoundationEncoder.EncodeToWma(reader, wmaOutputPath, bitrate);
                        }

                        ftpLocalPaths.Add(wmaOutputPath);
                    }

                    SetStatus($"Adding {TrackFormat} track... ({CurrentTrack} of {TrackTotal})");

                    AddSong(soundtrackId, path, title);
                    Dispatcher.Invoke(new Action(() =>
                    {
                        progressBar.Value++;
                    }));
                }

                if (TrackTotal > 0)
                {
                    SetStatus(TrackTotal + " track" + (TrackTotal > 1 ? "s" : "") + " added");
                    SoundtracksEdited = true;
                }
            }
            catch
            {
                SetStatus("Error adding track" + (TrackTotal > 1 ? "s" : ""));
            }

            Dispatcher.Invoke(new Action(() =>
            {
                progressBar.Value = 0;
            }));
        }

        private void AddSong(int soundtrackId, string path, string title)
        {
            char[] songTitle = new char[32];
            title.CopyTo(0, songTitle, 0, title.Length);

            int songMs = GetSongLengthInMs(path);

            for (int b = 0; b < soundtracks.Count; b++)
            {
                if (soundtracks[b].id == soundtrackId)
                {
                    for (int i = 0; i < soundtracks[b].songGroups.Count; i++)
                    {
                        for (int a = 0; a < 6; a++)
                        {
                            if (soundtracks[b].songGroups[i].songTimeMilliseconds[a] == 0)
                            {
                                soundtracks[b].songGroups[i].songId[a] = nextSongId;
                                soundtracks[b].songGroups[i].songNames[a] = songTitle;
                                soundtracks[b].songGroups[i].songTimeMilliseconds[a] = songMs;
                                soundtracks[b].numSongs++;
                                Dispatcher.Invoke(new Action(() =>
                                {
                                    soundtracks[b].allSongs.Add(new Song { 
                                        isRemote = false,
                                        Name = new string(songTitle).Trim(),
                                        TimeMs = songMs,
                                        songGroupId = soundtracks[b].songGroups[i].id,
                                        soundtrackId = soundtracks[b].id,
                                        id = nextSongId 
                                    });
                                }));

                                soundtracks[b].CalculateTotalTimeMs();

                                ftpSoundtrackIds.Add(soundtrackId.ToString("X4"));
                                ftpDestPaths.Add(soundtrackId.ToString("X4") + nextSongId.ToString("X4") + @".wma");

                                FindNextSongId();
                                return;
                            }
                        }
                    }
                    // no available song groups so create one
                    SongGroup sGroup = new SongGroup();
                    sGroup.magic = 0x00031073;
                    sGroup.soundtrackId = soundtrackId;
                    sGroup.id = soundtracks[b].songGroups.Count;
                    sGroup.padding = 0x00000001;
                    sGroup.songId[0] = nextSongId;
                    sGroup.songNames[0] = songTitle;
                    sGroup.songTimeMilliseconds[0] = songMs;
                    soundtracks[b].songGroups.Add(sGroup);
                    soundtracks[b].numSongs++;
                    Dispatcher.Invoke(new Action(() =>
                    {
                        soundtracks[b].allSongs.Add(new Song {
                            isRemote = false,
                            Name = new string(songTitle).Trim(),
                            TimeMs = songMs,
                            songGroupId = sGroup.id,
                            soundtrackId = soundtracks[b].id,
                            id = nextSongId 
                        });
                    }));
                    soundtracks[b].CalculateTotalTimeMs();

                    ftpSoundtrackIds.Add(soundtrackId.ToString("X4"));
                    ftpDestPaths.Add(soundtrackId.ToString("X4") + nextSongId.ToString("X4") + @".wma");

                    FindNextSongId();
                    return;
                }
            }
        }

        private int GetSongLengthInMs(string path)
        {
            IWMPMedia mediainfo = wmp.newMedia(path);
            return (int)(mediainfo.duration * 1000);
        }

        private void btnAddSoundtrack_Click(object sender, RoutedEventArgs e)
        {
            if (soundtracks.Count == 100)
            {
                MessageBox.Show("The maximum amount of soundtracks has been reached.", "Maximum Soundtracks", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            TitleInput TitleInput = new TitleInput("Enter a soundtrack title.", "Soundtrack Title", 31);
            if (TitleInput.ShowDialog() != true)
            {
                return;
            }

            string title = TitleInput.TrackTitle;

            // create soundtrack
            Soundtrack sTrack = new Soundtrack();
            sTrack.magic = 0x00021371;
            sTrack.id = nextSoundtrackId;
            title.CopyTo(0, sTrack.name, 0, title.Length);
            sTrack.padding = new byte[32];

            soundtracks.Add(sTrack);

            numSoundtracks++;

            FindNextSoundtrackId();

            listSoundtracks.SelectedItem = listSoundtracks.Items[soundtracks.Count - 1];
            listSoundtracks.Focus();

            SetStatus("Added soundtrack " + title);

            blankSoundtrackAdded = true;
            SoundtracksEdited = true;
        }

        private void btnDeleteSoundtrack_Click(object sender, RoutedEventArgs e)
        {
            Soundtrack temp = (Soundtrack)listSoundtracks.SelectedItem;

            MessageBoxResult DialogResult = MessageBox.Show("Are you sure you want to delete soundtrack " + temp.Name + "?", "Delete Soundtrack", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (DialogResult == MessageBoxResult.Yes)
            {
                soundtracks.Remove((Soundtrack)listSoundtracks.SelectedItem);
                FindNextSongId();
                FindNextSoundtrackId();

                SoundtracksEdited = true;
            }
        }

        private void btnRenameSoundtrack_Click(object sender, RoutedEventArgs e)
        {
            Soundtrack sTrack = (Soundtrack)listSoundtracks.SelectedItem;

            TitleInput TitleInput = new TitleInput("Enter a new soundtrack title.", "Edit Soundtrack Title", 31);
            if (TitleInput.ShowDialog() != true)
            {
                return;
            }

            string title = TitleInput.TrackTitle;

            sTrack.name = new char[64];
            title.CopyTo(0, sTrack.name, 0, title.Length);

            listSoundtracks.Items.Refresh();

            SoundtracksEdited = true;
        }

        private void listSoundtracks_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (blankSoundtrackAdded)
            {
                foreach (Soundtrack sound in e.RemovedItems)
                {
                    soundtracks.Remove(sound);
                }

                FindNextSoundtrackId();
                blankSoundtrackAdded = false;
            }

            if (listSoundtracks.SelectedItem == null)
            {
                listSongs.ItemsSource = null;
                btnDeleteSoundtrack.IsEnabled = false;
                btnAddTracks.IsEnabled = false;
                btnRenameSoundtrack.IsEnabled = false;
                return;
            }
            else
            {
                btnAddTracks.IsEnabled = true;
                btnRenameSoundtrack.IsEnabled = true;
            }

            ListBox listBox = (ListBox)sender;
            Soundtrack soundtrack = (Soundtrack)listBox.SelectedItem;
            soundtrack.RefreshAllSongNames();
            listSongs.ItemsSource = soundtrack.allSongs;
            btnDeleteSoundtrack.IsEnabled = true;
        }

        private async void btnAddTracks_Click(object sender, RoutedEventArgs e)
        {
            if (!Directory.Exists(OutputFolder))
            {
                MessageBox.Show("Invalid output folder configured in Settings.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            OpenFileDialog oDialog = new OpenFileDialog();
            oDialog.Title = "Choose track files to add";
            oDialog.Filter = "Track Files|*.wma; *.mp3; *.wav; *.flac; *.m4a|" + 
                             "WMA Files (*.wma)|*.wma|" +
                             "MP3 Files (*.mp3)|*.mp3|" +
                             "WAV Files (*.wav)|*.wav|" +
                             "FLAC Files (*.flac)|*.flac|" +
                             "M4A Files (*.m4a)|*.m4a";
            oDialog.Multiselect = true;

            if (oDialog.ShowDialog() != true)
            {
                return;
            }

            gridMain.IsEnabled = false;

            await Task.Run(() => AddTrackFiles(oDialog.FileNames, oDialog.FileNames.Length));

            gridMain.IsEnabled = true;
        }

        private void btnDeleteSongs_Click(object sender, RoutedEventArgs e)
        {
            Soundtrack tempSoundtrack = (Soundtrack)listSoundtracks.SelectedItem;

            MessageBoxResult DialogResult = MessageBox.Show("Are you sure you want to delete the selected songs from soundtrack " + tempSoundtrack.Name + "?", "Delete Songs", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (DialogResult != MessageBoxResult.Yes)
            {
                return;
            }

            for (int i = 0; i < listSongs.SelectedItems.Count; i++)
            {
                Song tempSong = (Song)listSongs.SelectedItems[i];

                bool foundInArray = false;
                for (int z = ftpDestPaths.Count - 1; z >= 0; z--)
                {
                    if (ftpDestPaths[z] == tempSong.soundtrackId.ToString("X4") + tempSong.id.ToString("X4") + @".wma")
                    {
                        foundInArray = true;
                        ftpDestPaths.RemoveAt(z);
                        ftpLocalPaths.RemoveAt(z);
                        ftpSoundtrackIds.RemoveAt(z);
                    }
                }

                foreach (SongGroup sGroup in tempSoundtrack.songGroups)
                {
                    for (int p = 0; p < 6; p++)
                    {
                        if (sGroup.songId[p] == tempSong.id)
                        {
                            sGroup.songId[p] = 0;
                            sGroup.songNames[p] = new char[32];
                            sGroup.songTimeMilliseconds[p] = 0;
                        }
                    }
                }

                if (!foundInArray)
                {
                    if (!ConnectToXbox())
                    {
                        return;
                    }

                    if (!GetMusicWorkingDirectory())
                    {
                        return;
                    }

                    FTP.SetWorkingDirectory(XboxMusicDirectory + tempSong.soundtrackId.ToString("X4"));

                    FTP.DeleteFile(tempSong.soundtrackId.ToString("X4") + tempSong.id.ToString("X4") + @".wma");

                    FTP.Disconnect();
                }

                tempSoundtrack.numSongs--;
            }

            SetStatus("Songs deleted");

            tempSoundtrack.RefreshAllSongNames();
            listSongs.ItemsSource = tempSoundtrack.allSongs;
            ReorderSongsInGroups(tempSoundtrack);
            FindNextSongId();

            SoundtracksEdited = true;
        }

        private void btnRenameSong_Click(object sender, RoutedEventArgs e)
        {
            Song song = (Song)listSongs.SelectedItem;

            TitleInput TitleInput = new TitleInput("Enter a new song title.", "Edit Song Title", 31);
            if (TitleInput.ShowDialog() != true)
            {
                return;
            }

            string title = TitleInput.TrackTitle;

            foreach (Soundtrack sTrack in soundtracks)
            {
                if (sTrack.id == song.soundtrackId)
                {
                    foreach (SongGroup sGroup in sTrack.songGroups)
                    {
                        for (int i = 0; i < 6; i++)
                        {
                            if (song.id == sGroup.songId[i] && sGroup.songTimeMilliseconds[i] > 0)
                            {
                                sGroup.songNames[i] = new char[32];
                                title.CopyTo(0, sGroup.songNames[i], 0, title.Length);
                                sTrack.RefreshAllSongNames();
                            }
                        }
                    }
                }
            }

            listSongs.Items.Refresh();

            SoundtracksEdited = true;
        }

        private void listSongs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (listSongs.SelectedItem == null)
            {
                btnDeleteSongs.IsEnabled = false;
                btnRenameSong.IsEnabled = false;
            }
            else if (listSongs.SelectedItems.Count > 1)
            {
                btnRenameSong.IsEnabled = false;
                btnDeleteSongs.IsEnabled = true;
            }
            else
            {
                btnRenameSong.IsEnabled = true;
                btnDeleteSongs.IsEnabled = true;
            }
        }

        private async void Window_Closing(object sender, CancelEventArgs e)
        {
            if (SoundtracksEdited)
            {
                MessageBoxResult DialogResult = MessageBox.Show("Do you want to upload your changes to your Xbox?", "Upload Changes?", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                if (DialogResult == MessageBoxResult.Yes)
                {
                    e.Cancel = true;

                    gridMain.IsEnabled = false;

                    await Task.Run(() => UploadDbToXbox());

                    //If this is still true then there was an error on upload, so we'll cancel closing
                    if (SoundtracksEdited)
                    {
                        gridMain.IsEnabled = true;
                    } 
                    else
                    {
                        this.Close();
                    }
                } 
                else if (DialogResult == MessageBoxResult.Cancel)
                {
                    e.Cancel = true;
                }
            }
        }
    }
}
