﻿using FluentFTP.Exceptions;
using Microsoft.VisualBasic;
using Microsoft.Win32;
using FluentFTP;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using WMPLib;
using static System.Net.WebRequestMethods;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Window;
using System.Reflection.Emit;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace OGXboxSoundtrackEditor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        List<FtpLogEntry> logLines = new List<FtpLogEntry>();

        FtpClient FTP;

        bool blankSoundtrackAdded = false;
        int blankSoundtrackId = 0;

        // wma stuff
        WindowsMediaPlayer wmp = new WindowsMediaPlayer();

        // saved settings variables
        string outputFolder;
        string ftpIpAddress;
        string ftpUsername;
        string ftpPassword;
        int bitrate;

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
            outputFolder = Properties.Settings.Default.outputFolder;
            ftpIpAddress = Properties.Settings.Default.ftpIpAddress;
            ftpUsername = Properties.Settings.Default.ftpUsername;
            ftpPassword = Properties.Settings.Default.ftpPassword;
            bitrate = Properties.Settings.Default.bitrate;
        }

        private bool ConnectToXbox()
        {
            //If the user hasn't set an IP or output directory, prompt to set one
            if (string.IsNullOrEmpty(ftpIpAddress))
            {
                MessageBoxResult DialogResult = MessageBox.Show("No FTP IP Address set in settings. Do you want to configure an IP Address?", "No FTP IP Set", MessageBoxButton.YesNo, MessageBoxImage.Information);
                if (DialogResult == MessageBoxResult.Yes)
                {
                    UserSettings Settings = new UserSettings();
                    if (Settings.ShowDialog() != true)
                    {
                        return false;
                    }
                }
            }

            FTP = new FtpClient();
            FTP.Host = ftpIpAddress;
            FTP.Credentials = new NetworkCredential(ftpUsername, ftpPassword);
            FTP.Port = 21; //TODO: Allow this to be user configurable

            //Connect to the Xbox
            SetStatus("Connecting to Xbox " + ftpIpAddress + "...");

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

            return false;
        }

        private async void mnuNew_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult result = MessageBox.Show("This will delete your entire soundtrack database from the Xbox.  Are you sure?", "Delete Soundtrack Database", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            // create header
            magic = 0x00000001;
            numSoundtracks = 0;
            nextSoundtrackId = 0;
            soundtrackIds = new int[100];
            nextSongId = 0;
            padding = new byte[96];

            songGroupCount = 0;
            paddingBetween = 0;

            soundtracks.Clear();
            listSoundtracks.ItemsSource = soundtracks;
            btnAddSoundtrack.IsEnabled = true;

            gridMain.IsEnabled = false;

            await Task.Run(() => DeleteAllFromFtp());
            
            gridMain.IsEnabled = true;
        }

        private void OpenDbFromStream()
        {
            if (!ConnectToXbox())
            {
                return;
            }

            SetStatus("Downloading soundtrack database...");

            try
            {
                //Check if music directory is on the Xbox
                if (!FTP.DirectoryExists("/E/TDATA/fffe0000/music"))
                {
                    if (!FTP.CreateDirectory("/E/TDATA/fffe0000/music"))
                    {
                        SetStatus("Failed to create directory on FTP server.");
                        return;
                    }
                }

                FTP.SetWorkingDirectory("/E/TDATA/fffe0000/music");

                if (!FTP.FileExists("ST.DB"))
                {
                    SetStatus("No soundtracks found on Xbox");
                    return;
                }

                if (!FTP.DownloadBytes(out byte[] DownloadedBytes, "/E/TDATA/fffe0000/music/ST.DB"))
                {
                    SetStatus("Couldn't download soundtrack database");
                    return;
                }

                if (DownloadedBytes.Length == 0)
                {
                    SetStatus("No soundtracks in database");
                    return;
                }

                BinaryReader bReader = new BinaryReader(new MemoryStream(DownloadedBytes), Encoding.Unicode);
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

                Dispatcher.Invoke(new Action(() =>
                {
                    btnAddSoundtrack.IsEnabled = true;
                    listSoundtracks.ItemsSource = soundtracks;
                }));

                SetStatus("Soundtrack database loaded");
            }
            catch (FtpException ex)
            {
                SetStatus("Couldn't retrieve soundtracks");
                MessageBox.Show("Couldn't retrieve soundtracks.\n" + ex.Message, "Couldn't Retrieve Soundtracks", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            catch
            {
                SetStatus("Unknown error");
                return;
            }
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

        private void mnuExit_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
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
                btnAddMp3.IsEnabled = false;
                btnAddWma.IsEnabled = false;
                btnRenameSoundtrack.IsEnabled = false;
                return;
            }
            else
            {
                btnAddMp3.IsEnabled = true;
                btnAddWma.IsEnabled = true;
                btnRenameSoundtrack.IsEnabled = true;
            }

            ListBox listBox = (ListBox)sender;
            Soundtrack soundtrack = (Soundtrack)listBox.SelectedItem;
            soundtrack.RefreshAllSongNames();
            listSongs.ItemsSource = soundtrack.allSongs;
            btnDeleteSoundtrack.IsEnabled = true;
        }

        private void btnAddSoundtrack_Click(object sender, RoutedEventArgs e)
        {
            string title = Interaction.InputBox("Enter a new soundtrack title.", "Edit Soundtrack Title", "", -1, -1);

            if (string.IsNullOrEmpty(title))
            {
                MessageBox.Show("Title cannot be empty.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            else if (title.Length > 64)
            {
                MessageBox.Show("Title cannot be longer than 64 characters.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

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
        }

        private async void btnAddWma_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog oDialog = new OpenFileDialog();
            oDialog.Filter = "Windows Media Audio files (*.wma)|*.wma";
            oDialog.Multiselect = true;

            if (oDialog.ShowDialog() != true)
            {
                return;
            }

            gridMain.IsEnabled = false;

            await Task.Run(() => AddWmaFiles(oDialog.FileNames));

            gridMain.IsEnabled = true;

            if (FTP.IsConnected)
            {
                FTP.Disconnect();
            }
        }

        private void AddWmaFiles(object paths)
        {
            try
            {
                Soundtrack sTrack = null;
                Dispatcher.Invoke(new Action(() =>
                {
                    sTrack = (Soundtrack)listSoundtracks.SelectedItem;
                }));

                int soundtrackId = sTrack.id;

                string[] realPaths = (string[])paths;

                SetStatus("Adding WMA tracks...");

                Dispatcher.Invoke(new Action(() =>
                {
                    progFtpTransfer.Maximum = realPaths.Length;
                }));

                foreach (string path in realPaths)
                {
                    ftpLocalPaths.Add(path);
                    AddSongLoop(soundtrackId, path);
                }

                SetStatus("WMA tracks added");

                FtpSTDB();
            }
            catch
            {
                SetStatus("Unknown error");
            }
        }

        private void AddSongLoop(int soundtrackId, string path)
        {
            char[] songTitle = GetSongTitle(path);
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
                                soundtracks[b].songGroups[i].songTimeMilliseconds[a] = GetSongLengthInMs(path);
                                soundtracks[b].numSongs++;
                                Dispatcher.Invoke(new Action(() =>
                                {
                                    soundtracks[b].allSongs.Add(new Song { isRemote = false, Name = new string(songTitle).Trim(), TimeMs = GetSongLengthInMs(path), songGroupId = soundtracks[b].songGroups[i].id, soundtrackId = soundtracks[b].id, id = nextSongId });
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
                    sGroup.songTimeMilliseconds[0] = GetSongLengthInMs(path);
                    soundtracks[b].songGroups.Add(sGroup);
                    soundtracks[b].numSongs++;
                    Dispatcher.Invoke(new Action(() =>
                    {
                        soundtracks[b].allSongs.Add(new Song { isRemote = false, Name = new string(songTitle).Trim(), TimeMs = GetSongLengthInMs(path), songGroupId = sGroup.id, soundtrackId = soundtracks[b].id, id = nextSongId });
                    }));
                    soundtracks[b].CalculateTotalTimeMs();

                    ftpSoundtrackIds.Add(soundtrackId.ToString("X4"));
                    ftpDestPaths.Add(soundtrackId.ToString("X4") + nextSongId.ToString("X4") + @".wma");

                    FindNextSongId();
                    return;
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

        private int GetSongLengthInMs(string path)
        {
            IWMPMedia mediainfo = wmp.newMedia(path);
            return (int)(mediainfo.duration * 1000);
        }

        private char[] GetSongTitle(string path)
        {
            char[] titleChars = new char[32];
            IWMPMedia mediainfo = wmp.newMedia(path);
            string title = mediainfo.name.Trim();

            if (title.Length > 32)
            {
                bool validTitle = false;
                while (!validTitle)
                {
                    title = Interaction.InputBox("Song name " + mediainfo.name + " is too long. Please enter a new one.", "Song Title", "", -1, -1);
                    
                    if (string.IsNullOrEmpty(title))
                    {
                        MessageBox.Show("Title cannot be blank.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    else if (title.Length > 32)
                    {
                        MessageBox.Show("Title cannot be longer than 32 characters.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    else
                    {
                        validTitle = true;
                    }
                }
            }

            title.CopyTo(0, titleChars, 0, title.Length);
            return titleChars;
        }

        private string GetSongTitleString(string path)
        {
            IWMPMedia mediainfo = wmp.newMedia(path);
            return mediainfo.name;
        }

        private void btnDeleteSoundtrack_Click(object sender, RoutedEventArgs e)
        {
            if (listSoundtracks.SelectedItem == null)
            {
                MessageBox.Show("No soundtrack selected.  Please try again.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            Soundtrack temp = (Soundtrack)listSoundtracks.SelectedItem;

            if (MessageBox.Show("Are you sure you want to delete soundtrack " + temp.Name + "?", "Question", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                soundtracks.Remove((Soundtrack)listSoundtracks.SelectedItem);
                FindNextSongId();
                FindNextSoundtrackId();
            }

            FtpSTDB();
        }

        private void btnDeleteSongs_Click(object sender, RoutedEventArgs e)
        {
            if (listSongs.SelectedItems.Count == 0)
            {
                MessageBox.Show("No songs selected.  Please try again.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            Soundtrack tempSoundtrack = (Soundtrack)listSoundtracks.SelectedItem;

            if (MessageBox.Show("Are you sure you want to delete the selected songs from soundtrack " + tempSoundtrack.Name + "?", "Question", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
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

                    FTP.SetWorkingDirectory("/E/TDATA/fffe0000/music/" + tempSong.soundtrackId.ToString("X4"));

                    FTP.DeleteFile(tempSong.soundtrackId.ToString("X4") + tempSong.id.ToString("X4") + @".wma");
                }

                tempSoundtrack.numSongs--;
            }

            txtStatus.Text = "Songs Deleted Successfully";

            tempSoundtrack.RefreshAllSongNames();
            listSongs.ItemsSource = tempSoundtrack.allSongs;
            ReorderSongsInGroups(tempSoundtrack);
            FindNextSongId();

            FtpSTDB();
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

        private void FtpSTDB()
        {
            if (!ConnectToXbox())
            {
                return;
            }
            try
            {
                FTP.SetWorkingDirectory("/E/TDATA/fffe0000/music/");

                FTP.UploadBytes(GetDbBytes(), "ST.DB", FtpRemoteExists.Overwrite);

                Dispatcher.Invoke(new Action(() =>
                {
                    progFtpTransfer.Value = 0;
                    progFtpTransfer.Maximum = ftpDestPaths.Count;
                }));

                for (int i = 0; i < ftpDestPaths.Count; i++)
                {
                    FTP.CreateDirectory(ftpSoundtrackIds[i]);

                    FTP.SetWorkingDirectory("/E/TDATA/fffe0000/music/" + ftpSoundtrackIds[i]);

                    FTP.UploadFile(ftpLocalPaths[i], ftpDestPaths[i], FtpRemoteExists.Overwrite);

                    Dispatcher.Invoke(new Action(() =>
                    {
                        progFtpTransfer.Value++;
                    }));
                }

                Dispatcher.Invoke(new Action(() =>
                {
                    txtStatus.Text = "Uploading Success";
                    gridMain.IsEnabled = true;
                }));
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
                Dispatcher.Invoke(new Action(() =>
                {
                    txtStatus.Text = "Failed To Upload Changes";
                    gridMain.IsEnabled = true;
                }));
            }
            finally
            {
                Dispatcher.Invoke(new Action(() =>
                {
                    progFtpTransfer.Value = 0;
                }));
            }

            ftpDestPaths.Clear();
            ftpLocalPaths.Clear();
            ftpSoundtrackIds.Clear();
            return;
        }

        private void DeleteAllFromFtp()
        {
            if (!ConnectToXbox()) 
            { 
                return; 
            }
            
            SetStatus("Deleting soundtracks from Xbox...");

            try
            {
                if (FTP.DirectoryExists("/E/TDATA/fffe0000/music/"))
                {
                    FTP.SetWorkingDirectory("/E/TDATA/fffe0000/music/");

                    //TODO: Using EmptyDirectory and DeleteDirectory only seems to work on XBMC, get it working for other dashboards
                    FTP.EmptyDirectory("/E/TDATA/fffe0000/music/");
                }

                SetStatus("Deleted soundtracks from Xbox");
            }
            catch (Exception ex)
            {
                SetStatus("Critical Error");
            }
        }

        private async void mnuOpenFromFtp_Click(object sender, RoutedEventArgs e)
        {
            gridMain.IsEnabled = false;

            await Task.Run(() => OpenDbFromStream());

            gridMain.IsEnabled = true;

            if (FTP.IsConnected)
            {
                FTP.Disconnect();
            }
        }

        private void btnRenameSoundtrack_Click(object sender, RoutedEventArgs e)
        {
            Soundtrack sTrack = (Soundtrack)listSoundtracks.SelectedItem;

            string title = Interaction.InputBox("Enter a new soundtrack title.", "Edit Soundtrack Title", new string(sTrack.name), -1, -1);

            if (string.IsNullOrEmpty(title))
            {
                MessageBox.Show("Title cannot be empty.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            else if (title.Length > 64)
            {
                MessageBox.Show("Title cannot be longer than 64 characters.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            sTrack.name = new char[64];
            title.CopyTo(0, sTrack.name, 0, title.Length);

            FtpSTDB();

            listSoundtracks.Items.Refresh();
        }

        private bool ContainsSoundtracks()
        {
            if (soundtracks.Count > 0)
            {
                return true;
            }
            return false;
        }

        private void btnRenameSong_Click(object sender, RoutedEventArgs e)
        {
            Song song = (Song)listSongs.SelectedItem;

            string title = Interaction.InputBox("Enter a new song title.", "Edit Soundtrack Title", song.Name, -1, -1);

            if (string.IsNullOrEmpty(title))
            {
                MessageBox.Show("Title cannot be empty.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            else if (title.Length > 32)
            {
                MessageBox.Show("Title cannot be longer than 32 characters.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

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

            FtpSTDB();

            listSongs.Items.Refresh();
        }
        
        private void mnuBackupFromFtp_Click(object sender, RoutedEventArgs e)
        {
            /*
            SaveFileDialog sDialog = new SaveFileDialog();
            sDialog.Filter = "Zip files (*.zip)|*.zip";
            sDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (sDialog.ShowDialog() != true)
            {
                return;
            }

            gridMain.IsEnabled = false;
            txtStatus.Text = "Backing Up From FTP";
            thrFtpControl = new Thread(new ParameterizedThreadStart(BackupFromFtp));
            thrFtpControl.Start(sDialog.FileName);
            */
        }
        
        private void BackupFromFtp(object zipPath)
        {
            /*
            try
            {
                using (FileStream fStream = new FileStream((string)zipPath, FileMode.Create))
                {
                    using (ZipArchive zip = new ZipArchive(fStream, ZipArchiveMode.Create, true))
                    {
                        ftpClient = new FtpClient(ftpIpAddress, ftpUsername, ftpPassword);

                        if (!ftpClient.Connect())
                        {
                            SetStatus("Error: Failed To Connnect To Xbox");
                            return;
                        }
                        if (!ftpClient.Login())
                        {
                            SetStatus("Error: Wrong Username Or Password");
                            return;
                        }
                        if (!ftpClient.ChangeWorkingDirectory(@"/E/TDATA/fffe0000/music"))
                        {
                            SetStatus("Error: No Music To Backup");
                            ftpClient.Disconnect();
                            return;
                        }

                        ftpClient.List();
                        List<FtpDirectory> soundtrackFolders = ftpClient.GetDirectories();
                        List<FtpFile> soundtrackFiles = ftpClient.GetFiles();

                        foreach (FtpDirectory tempDir in soundtrackFolders)
                        {
                            if (!ftpClient.ChangeWorkingDirectory(tempDir.Name))
                            {
                                ftpClient.Disconnect();
                                return;
                            }

                            ftpClient.List();
                            List<FtpFile> subfolderFiles = ftpClient.GetFiles();

                            foreach (FtpFile tempFile in subfolderFiles)
                            {
                                if (!ftpClient.Retrieve(tempFile.name))
                                {
                                    ftpClient.Disconnect();
                                    return;
                                }
                                ZipArchiveEntry fileEntry = zip.CreateEntry(tempDir.Name + "/" + tempFile.name);
                                using (BinaryWriter writer = new BinaryWriter(fileEntry.Open()))
                                {
                                    writer.Write(ftpClient.downloadedBytes, 0, ftpClient.downloadedBytes.Length);
                                }
                            }
                            if (!ftpClient.ChangeWorkingDirectory(@"/E/TDATA/fffe0000/music"))
                            {
                                ftpClient.Disconnect();
                                return;
                            }
                        }

                        foreach (FtpFile tempFile in soundtrackFiles)
                        {
                            if (!ftpClient.Retrieve(tempFile.name))
                            {
                                ftpClient.Disconnect();
                                return;
                            }
                            ZipArchiveEntry fileEntry = zip.CreateEntry(tempFile.name);
                            using (BinaryWriter writer = new BinaryWriter(fileEntry.Open()))
                            {
                                writer.Write(ftpClient.downloadedBytes, 0, ftpClient.downloadedBytes.Length);
                            }
                        }

                        ftpClient.Disconnect();
                        Dispatcher.Invoke(new Action(() =>
                        {
                            txtStatus.Text = "DB Backed Up To Zip";
                        }));
                    }
                }
            }
            catch
            {
                Dispatcher.Invoke(new Action(() => {
                    txtStatus.Text = "Critical Error";
                }));
            }
            finally
            {
                Dispatcher.Invoke(new Action(() => {
                    gridMain.IsEnabled = true;
                }));
            }
            */
        }
        
        private void mnuUploadBackupToFtp_Click(object sender, RoutedEventArgs e)
        {
            /*
            OpenFileDialog ofDialog = new OpenFileDialog();
            ofDialog.Filter = "Zip files (*.zip)|*.zip";
            ofDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (ofDialog.ShowDialog() != true)
            {
                return;
            }

            gridMain.IsEnabled = false;
            txtStatus.Text = "Uploading Backup To FTP";
            thrFtpControl = new Thread(new ParameterizedThreadStart(UploadBackupToFtp));
            thrFtpControl.Start(ofDialog.FileName);
            */
        }

        private void SetStatus(string msg)
        {
            Dispatcher.Invoke(new Action(() => {
                txtStatus.Text = msg;
            }));
        }
        
        private void UploadBackupToFtp(object zipPath)
        {
            /*
            ftpClient = new FtpClient(ftpIpAddress, ftpUsername, ftpPassword);
            try
            {
                if (!ftpClient.Connect())
                {
                    SetStatus("Error: Failed To Connnect To Xbox");
                    return;
                }
                if (!ftpClient.Login())
                {
                    SetStatus("Error: Wrong Username Or Password");
                    return;
                }

                if (!ChangeToMusicDirectory())
                {
                    SetStatus("Error: Failed To Create Music Folder");
                    return;
                }

                using (FileStream fStream = new FileStream((string)zipPath, FileMode.Open))
                {
                    using (ZipArchive zip = new ZipArchive(fStream, ZipArchiveMode.Read, true))
                    {
                        Dispatcher.Invoke(new Action(() => {
                            progFtpTransfer.Maximum = zip.Entries.Count;
                            progFtpTransfer.Value = 0;
                        }));
                        
                        foreach (ZipArchiveEntry zArchive in zip.Entries)
                        {
                            using (BinaryReader bReader = new BinaryReader(zArchive.Open()))
                            {
                                long fileSize = zArchive.Length;
                                ftpClient.toUploadBytes = bReader.ReadBytes((int)fileSize);

                                if (!ftpClient.ChangeWorkingDirectory(@"/E/TDATA/fffe0000/music"))
                                {
                                    ftpClient.Disconnect();
                                    return;
                                }
                                //Debug.WriteLine(zArchive.FullName);
                                if (zArchive.FullName.Contains("/"))
                                {
                                    string[] split = zArchive.FullName.Split('/');
                                    ftpClient.MakeDirectory(split[0]);
                                    ftpClient.ChangeWorkingDirectory(split[0]);
                                    if (!ftpClient.Store(zArchive.Name))
                                    {
                                        return;
                                    }
                                    Dispatcher.Invoke(new Action(() => {
                                        progFtpTransfer.Value++;
                                    }));
                                }
                                else
                                {
                                    if (!ftpClient.Store(zArchive.Name))
                                    {
                                        return;
                                    }
                                    Dispatcher.Invoke(new Action(() => {
                                        progFtpTransfer.Value++;
                                    }));
                                }
                            }
                        }
                    }
                }
                ftpClient.Disconnect();
                Dispatcher.Invoke(new Action(() =>
                {
                    txtStatus.Text = "Uploaded DB Backup";
                }));
            }
            catch
            {
                Dispatcher.Invoke(new Action(() =>
                {
                    txtStatus.Text = "Critical Error";
                }));
            }
            finally
            {
                logLines = ftpClient.ftpLogEntries;
                Dispatcher.Invoke(new Action(() =>
                {
                    progFtpTransfer.Value = 0;
                    gridMain.IsEnabled = true;
                }));
            }
            */
        }
        

        private async void btnAddMp3_Click(object sender, RoutedEventArgs e)
        {
            if (!Directory.Exists(outputFolder))
            {
                MessageBox.Show("Invalid output folder configured in Settings.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            OpenFileDialog oDialog = new OpenFileDialog();
            oDialog.Filter = "MP3 files (*.mp3)|*.mp3";
            oDialog.Multiselect = true;

            if (oDialog.ShowDialog() != true)
            {
                return;
            }

            progFtpTransfer.Value = 0;
            gridMain.IsEnabled = false;

            await Task.Run(() => AddMp3Files(oDialog.FileNames));

            gridMain.IsEnabled = true;
        }

        private void AddMp3Files(object paths)
        {
            Soundtrack sTrack = null;
            Dispatcher.Invoke(new Action(() =>
            {
                sTrack = (Soundtrack)listSoundtracks.SelectedItem;
            }));
            
            int soundtrackId = sTrack.id;

            string[] realPaths = (string[])paths;

            SetStatus("Converting MP3 tracks...");
            
            Dispatcher.Invoke(new Action(() =>
            {
                progFtpTransfer.Maximum = realPaths.Length;
            }));

            try
            {
                foreach (string path in realPaths)
                {
                    string wmaOutputPath = outputFolder + "\\" + nextSongId.ToString("X4") + ".wma";
                    using (MediaFoundationReader reader = new MediaFoundationReader(path))
                    {
                        MediaFoundationEncoder.EncodeToWma(reader, wmaOutputPath, bitrate);
                    }

                    ftpLocalPaths.Add(wmaOutputPath);
                    AddSongLoop(soundtrackId, path);
                    Dispatcher.Invoke(new Action(() =>
                    {
                        progFtpTransfer.Value++;
                    }));
                }

                SetStatus("MP3 tracks added");
            }
            catch
            {
                SetStatus("Unknown error");
            }

            FtpSTDB();
        }
    }
}
