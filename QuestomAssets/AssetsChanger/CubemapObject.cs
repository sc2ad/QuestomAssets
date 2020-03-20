using System;
using System.Collections.Generic;
using System.Text;

namespace QuestomAssets.AssetsChanger
{
    public sealed class CubemapObject : TextureObject
    {
        public CubemapObject(IObjectInfo<AssetsObject> objectInfo, AssetsReader reader) : base(objectInfo)
        {
            Parse(reader);
        }

        public CubemapObject(AssetsFile assetsFile) : base(assetsFile, AssetsConstants.ClassID.CubemapClassID)
        { }
        public override void Parse(AssetsReader reader)
        {
            base.Parse(reader);
            SourceTextures = reader.ReadArrayOf(r => (ISmartPtr<Texture2DObject>)SmartPtr<Texture2DObject>.Read(ObjectInfo.ParentFile, this, r));
        }

        protected override void WriteObject(AssetsWriter writer)
        {
            base.WriteObject(writer);
            writer.WriteArrayOf(SourceTextures, (o, w) => o.Write(w));
        }

        public List<ISmartPtr<Texture2DObject>> SourceTextures { get; set; }

    }
}
