using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using QuestomAssets.AssetsChanger;
using Newtonsoft.Json;

namespace QuestomAssets.BeatSaber
{
    public sealed class BeatmapLevelsModel : MonoBehaviourObject, INeedAssetsMetadata
    {
        public BeatmapLevelsModel(AssetsFile assetsFile) : base(assetsFile, assetsFile.Manager.GetScriptObject("BeatmapLevelsModel"))
        {
        }

        public BeatmapLevelsModel(IObjectInfo<AssetsObject> objectInfo, AssetsReader reader) : base(objectInfo)
        {
            Parse(reader);
        }

        public ISmartPtr<BeatmapLevelPackCollectionContainer> DlcLevelPackCollectionContainer { get; set; }
        public ISmartPtr<BeatmapLevelPackCollection> OstsAndExtrasPackCollection { get; set; }
        public ISmartPtr<BeatmapLevelDataLoader> BeatmapLevelDataLoader { get; set; }
        public int MaxCachedBeatmapLevels { get; set; }

        public override void Parse(AssetsReader reader)
        {
            base.ParseBase(reader);
            DlcLevelPackCollectionContainer = SmartPtr<BeatmapLevelPackCollectionContainer>.Read(ObjectInfo.ParentFile, this, reader);
            OstsAndExtrasPackCollection = SmartPtr<BeatmapLevelPackCollection>.Read(ObjectInfo.ParentFile, this, reader);
            BeatmapLevelDataLoader = SmartPtr<BeatmapLevelDataLoader>.Read(ObjectInfo.ParentFile, this, reader);
            MaxCachedBeatmapLevels = reader.ReadInt32();
        }

        protected override void WriteObject(AssetsWriter writer)
        {
            base.WriteBase(writer);
            DlcLevelPackCollectionContainer.Write(writer);
            OstsAndExtrasPackCollection.Write(writer);
            BeatmapLevelDataLoader.Write(writer);
            writer.Write(MaxCachedBeatmapLevels);
        }

        [System.ComponentModel.Browsable(false)]
        [Newtonsoft.Json.JsonIgnore]
        public override byte[] ScriptParametersData
        {
            get
            {
                throw new InvalidOperationException("Cannot access parameters data from this object.");
            }
            set
            {
                throw new InvalidOperationException("Cannot access parameters data from this object.");
            }
        }


    }
}
