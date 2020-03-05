using System;
using System.Collections.Generic;
using System.Text;

namespace QuestomAssets.AssetsChanger
{
    public class MinMaxAABB
    {

        public MinMaxAABB()
        { }
        public MinMaxAABB(AssetsReader reader)
        {
            Parse(reader);
        }

        public void Parse(AssetsReader reader)
        {
            Min = new Vector3F(reader);
            Max = new Vector3F(reader);
        }

        public void Write(AssetsWriter writer)
        {
            Min.Write(writer);
            Max.Write(writer);
        }
        public Vector3F Min { get; set; }
        public Vector3F Max { get; set; }
    }
}
