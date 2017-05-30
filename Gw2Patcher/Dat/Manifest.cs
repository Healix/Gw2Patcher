using System.IO;

namespace Gw2Patcher.Dat
{
    class Manifest
    {
        public class ManifestRecord
        {
            public int baseId, fileId, size;
        }

        private Manifest()
        {

        }

        private static Manifest ParseRoot(byte[] buffer)
        {
            var records = buffer[32] | buffer[33] << 8 | buffer[34] << 16 | buffer[35] << 24;
            var offset = 36 + buffer[36] | buffer[37] << 8 | buffer[38] << 16 | buffer[39] << 24;

            Manifest m = new Manifest()
            {
                records = new ManifestRecord[records]
            };

            for (var i = 0; i < records; i++)
            {
                var r = m.records[i] = new ManifestRecord();

                r.baseId = buffer[offset++] | buffer[offset++] << 8 | buffer[offset++] << 16 | buffer[offset++] << 24;
                r.fileId = buffer[offset++] | buffer[offset++] << 8 | buffer[offset++] << 16 | buffer[offset++] << 24;
                r.size = buffer[offset++] | buffer[offset++] << 8 | buffer[offset++] << 16 | buffer[offset++] << 24;

                offset += 8;
            }

            return m;
        }

        private static Manifest ParseAsset(byte[] buffer)
        {
            var records = buffer[40] | buffer[41] << 8 | buffer[42] << 16 | buffer[43] << 24;
            var offset = 44 + buffer[44] | buffer[45] << 8 | buffer[46] << 16 | buffer[47] << 24;

            Manifest m = new Manifest()
            {
                records = new ManifestRecord[records]
            };

            for (var i = 0; i < records; i++)
            {
                var r = m.records[i] = new ManifestRecord();

                r.baseId = buffer[offset++] | buffer[offset++] << 8 | buffer[offset++] << 16 | buffer[offset++] << 24;
                r.fileId = buffer[offset++] | buffer[offset++] << 8 | buffer[offset++] << 16 | buffer[offset++] << 24;
                r.size = buffer[offset++] | buffer[offset++] << 8 | buffer[offset++] << 16 | buffer[offset++] << 24;

                offset += 4;
            }

            return m;
        }

        public static Manifest Parse(byte[] buffer)
        {
            var chunkId = buffer[12] | buffer[13] << 8 | buffer[14] << 16 | buffer[15] << 24;
            var build = buffer[28] | buffer[29] << 8 | buffer[30] << 16 | buffer[31] << 24;

            string id = (char)buffer[0] + "" + (char)buffer[1];
            string cid = (char)buffer[12] + "" + (char)buffer[13] + (char)buffer[14] + (char)buffer[15];

            Manifest m;

            if (chunkId == 1414743629)
                m = ParseAsset(buffer);
            else if (chunkId == 1179472449)
                m = ParseRoot(buffer);
            else
                throw new IOException("Unknown chunk header");

            m.build = build;

            return m;
        }

        public int build;
        public ManifestRecord[] records;
    }
}
