using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace QuestomAssets.AssetsChanger
{ 
    public class AssetsFileHeader
    {
        /// <summary>
        /// Return the size of the header which is a constant value
        /// </summary>
        public Int32 HeaderSize {
            get
            { 
                return 4 + 4 + 4 + 4 + 1 + 3 ;
            }
        }

        public int MetadataSize { get; set; }

        public long FileSize { get; set; }

        public int Version { get; set; }

        public long ObjectDataOffset { get; set; }

        public bool IsBigEndian { get; set; }
        // Version >= 22
        public byte[] unknown { get; set; }

        public AssetsFileHeader(AssetsReader reader)
        {
            Parse(reader);
        }

        private void Parse(AssetsReader reader)
        {
            MetadataSize = reader.ReadBEInt32();
            FileSize = reader.ReadBEInt32();
            Version = reader.ReadBEInt32();
            ObjectDataOffset = reader.ReadBEInt32();
            if (Version >= 9)
            {
                IsBigEndian = reader.ReadBoolean();
                reader.ReadBytes(3);
            }
            if (Version >= 22)
            {
                MetadataSize = reader.ReadBEInt32();
                FileSize = reader.ReadBEInt64();
                ObjectDataOffset = reader.ReadBEInt64();
                unknown = reader.ReadBytes(4);
            }
        }

        public void Write(AssetsWriter writer)
        {
            writer.WriteBEInt32(MetadataSize);
            writer.WriteBEInt32((int)FileSize);
            writer.WriteBEInt32(Version);
            writer.WriteBEInt32((int)ObjectDataOffset);
            if (Version >= 9)
            {
                writer.Write(IsBigEndian);
                writer.Write(new byte[3]);
            }
            if (Version >= 22)
            {
                writer.WriteBEInt32(MetadataSize);
                writer.WriteBEInt64(FileSize);
                writer.WriteBEInt64(ObjectDataOffset);
                writer.Write(unknown);
            }
        }

    }
}
