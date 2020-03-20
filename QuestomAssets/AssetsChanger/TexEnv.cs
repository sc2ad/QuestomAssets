using System;
using System.Collections.Generic;
using System.Text;

namespace QuestomAssets.AssetsChanger
{
    public class TexEnv
    {
        public TexEnv(AssetsFile file, AssetsObject owner, AssetsReader reader)
        {
            Parse(file, owner, reader);
        }

        public TexEnv()
        { }

        public void Parse(AssetsFile file, AssetsObject owner, AssetsReader reader)
        {
            Texture = SmartPtr<TextureObject>.Read(file, owner, reader);
            Scale = new Vector2F(reader);
            Offset = new Vector2F(reader);
        }

        public void Write(AssetsWriter writer)
        {
            Texture.Write(writer);
            Scale.Write(writer);
            Offset.Write(writer);
        }

        //this is a Texture, I think that means Texture2D?  is there another kind?
        public SmartPtr<TextureObject> Texture { get; set; }
        public Vector2F Scale { get; set; }
        public Vector2F Offset { get; set; }
    }
}
