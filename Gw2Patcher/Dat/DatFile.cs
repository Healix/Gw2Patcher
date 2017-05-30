using System.IO;

namespace Gw2Patcher.Dat
{
    static class DatFile
    {
        public class MftEntry
        {
            public int baseId, fileId;
        }

        public static MftEntry[] Read(string path)
        {
            MftEntry[] entries;

            using (var r = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                int length = 1024000;
                byte[] buffer = new byte[length];
                int read, id, size, compression;
                long offset;

                read = r.Read(buffer, 0, 35);

                if (read < 35)
                    throw new IOException();

                byte version = buffer[0];
                id = buffer[1] | buffer[2] << 8 | buffer[3] << 16;
                offset = (uint)(buffer[24] | buffer[25] << 8 | buffer[26] << 16 | buffer[27] << 24) | (long)(buffer[28] | buffer[29] << 8 | buffer[30] << 16 | buffer[31] << 24) << 32;
                size = buffer[32] | buffer[33] << 8 | buffer[34] << 16 | buffer[35] << 24;

                //earch entry is 24 bytes - skipping to the 3rd entry, which points to the ids
                r.Position = offset + 48;

                entries = new MftEntry[size / 24];

                read = r.Read(buffer, 0, 24);

                if (read < 24)
                    throw new IOException();

                offset = (uint)(buffer[0] | buffer[1] << 8 | buffer[2] << 16 | buffer[3] << 24) | (long)(buffer[4] | buffer[5] << 8 | buffer[6] << 16 | buffer[7] << 24) << 32;
                size = buffer[8] | buffer[9] << 8 | buffer[10] << 16 | buffer[11] << 24;
                compression = buffer[12] << 8 | buffer[13];

                if (compression != 0)
                    throw new IOException("Unable to compress file table");

                //each entry is 8 bytes - file id + index
                do
                {
                    r.Position = offset;

                    int l;
                    if (size < length)
                        l = size;
                    else
                        l = length;

                    read = r.Read(buffer, 0, l);

                    if (read == 0)
                        throw new IOException("Unable to read data");

                    if ((l = read % 8) != 0)
                        read -= l;

                    offset += read;
                    size -= read;

                    for (l = read / 8, read = 0; l > 0; l--)
                    {
                        id = buffer[read++] | buffer[read++] << 8 | buffer[read++] << 16 | buffer[read++] << 24;
                        int i = buffer[read++] | buffer[read++] << 8 | buffer[read++] << 16 | buffer[read++] << 24;

                        MftEntry entry = entries[i];
                        if (entry == null)
                        {
                            entry = entries[i] = new MftEntry()
                            {
                                baseId = id,
                                fileId = id
                            };
                        }
                        else
                        {
                            if (id < entry.baseId)
                                entry.baseId = id;
                            if (id > entry.fileId)
                                entry.fileId = id;
                        }
                    }
                }
                while (size > 0);
            }

            return entries;
        }
    }
}
