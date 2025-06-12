namespace OGXboxSoundtrackEditor
{
    public class SongGroup
    {
        public int magic;
        public int soundtrackId;
        public int id;
        public int padding;
        public int[] songId = new int[6];
        public int[] songTimeMilliseconds = new int[6];
        public char[][] songNames = new char[6][];
        public byte[] paddingChar = new byte[64];
    }
}
