using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using QuestomAssets.AssetsChanger;
using System.IO;

namespace QuestomAssets.BeatSaber
{
    public sealed class BeatmapLevelPackCollectionContainer : MonoBehaviourObject
    {
        public BeatmapLevelPackCollectionContainer(IObjectInfo<AssetsObject> objectInfo, AssetsReader reader) : base(objectInfo)
        {
            Parse(reader);
        }

        public BeatmapLevelPackCollectionContainer(AssetsFile assetsFile) : base(assetsFile, assetsFile.Manager.GetScriptObject("BeatmapLevelPackCollectionContainerSO"))
        { }

        public ISmartPtr<BeatmapLevelPackCollection> BeatmapLevelPackCollection { get; set; }

        public override void Parse(AssetsReader reader)
        {
            base.ParseBase(reader);
            BeatmapLevelPackCollection = SmartPtr<BeatmapLevelPackCollection>.Read(ObjectInfo.ParentFile, this, reader);
        }

        protected override void WriteObject(AssetsWriter writer)
        {
            base.WriteBase(writer);
            BeatmapLevelPackCollection.Write(writer);
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
