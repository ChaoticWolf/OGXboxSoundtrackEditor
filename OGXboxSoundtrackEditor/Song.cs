namespace OGXboxSoundtrackEditor
{
    public class Song
    {
        public int Identity
        {
            get { return id; }
        }
        public int id;

        public int SongGroupId
        {
            get { return songGroupId; }
        }
        public int songGroupId;

        public int soundtrackId;

        public bool isRemote;

        private string name;
        public string Name
        {
            get { return name; }
            set { name = value; }
        }
        private int timeMs;
        public int TimeMs
        {
            get { return timeMs; }
            set { timeMs = value; }
        }
    }
}
