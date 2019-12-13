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
    public sealed class BeatmapLevelDataLoader : MonoBehaviourObject, INeedAssetsMetadata
    {
        public BeatmapLevelDataLoader(AssetsFile assetsFile) : base(assetsFile, assetsFile.Manager.GetScriptObject("BeatmapLevelDataLoaderSO"))
        {
        }

        public BeatmapLevelDataLoader(IObjectInfo<AssetsObject> objectInfo, AssetsReader reader) : base(objectInfo)
        {
            Parse(reader);
        }

        public ISmartPtr<BeatmapCharacteristicCollectionObject> AllBeatmapCharacteristicCollection { get; set; }

        public override void Parse(AssetsReader reader)
        {
            base.ParseBase(reader);
            AllBeatmapCharacteristicCollection = SmartPtr<BeatmapCharacteristicCollectionObject>.Read(ObjectInfo.ParentFile, this, reader);
        }

        protected override void WriteObject(AssetsWriter writer)
        {
            base.WriteBase(writer);
            AllBeatmapCharacteristicCollection.Write(writer);
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
