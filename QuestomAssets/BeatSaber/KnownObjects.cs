﻿using QuestomAssets.AssetsChanger;
using System;
using System.Collections.Generic;
using System.Text;

namespace QuestomAssets.BeatSaber
{
    //I really need a file to pointer resolver
    public class KnownObjects
    {
        public static class File17
        {
            //public static PPtr DefaultEnvironment { get; set; } = new PPtr(20, 1);
            //public static PPtr BigMirrorEnvironment { get; set; } = new PPtr(0, 249);
            //public static PPtr CrabRaveEnvironment { get; set; } = new PPtr(0, 250);
            //public static PPtr KDAEnvironment { get; set; } = new PPtr(0, 251);

            ////in sharedassets19
            //public static PPtr MonstercatEnvironment { get; set; } = new PPtr(-1, 52);

            ////in sharedassets14
            //public static PPtr NiceEnvironment { get; set; } = new PPtr(-1, 3);
            //public static PPtr TriangleEnvironment { get; set; } = new PPtr(0, 252);
            //public static PPtr BeatSaberCoverArt { get; set; } = new PPtr(0, 19);
            //public static PPtr OneSaberCharacteristic { get; set; } = new PPtr(19, 1);
            //public static PPtr NoArrowsCharacteristic { get; set; } = new PPtr(6, 1);
            //public static PPtr StandardCharacteristic { get; set; } = new PPtr(22, 1);

            //public static PPtr ExtrasCoverArtSprite { get; set; } = new PPtr(0, 45);



            public static RawPtr BeatmapLevelDataScriptPtr { get; set; } = new RawPtr(1, 644);
            public static RawPtr BeatmapDataScriptPtr { get; set; } = new RawPtr(1, 1552);
            public static RawPtr BeatmapLevelCollectionScriptPtr { get; set; } = new RawPtr(1, 762);
            public static RawPtr BeatmapLevelPackScriptPtr { get; set; } = new RawPtr(1, 1480);
            public static RawPtr MainLevelsCollectionScriptPtr { get; set; } = new RawPtr(1, 1530);


        }


    }
}
