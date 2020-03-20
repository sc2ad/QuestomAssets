using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace QuestomAssets.AssetsChanger
{
    public sealed class Texture2DObject : TextureObject
    {
        public Texture2DObject(IObjectInfo<AssetsObject> objectInfo, AssetsReader reader) : base(objectInfo)
        {
            Parse(reader);
        }

        public Texture2DObject(AssetsFile assetsFile) : base(assetsFile, AssetsConstants.ClassID.Texture2DClassID)
        { }

    }

    
}
