using QuestomAssets.AssetsChanger;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace QuestomAssets.BeatSaber
{
    public class MiscUtils
    {
        private static HashSet<char> InvalidChars = new HashSet<char>(System.IO.Path.GetInvalidPathChars().Union(System.IO.Path.GetInvalidFileNameChars()).Union(new char[] { ' ' }).Union(new char[] { '-' }));
        public static string SanitizeName(string songName)
        {
            return new string(songName.Where(c => !InvalidChars.Contains(c)).ToArray());
        }

        public static string GetCharacteristicAssetName(string characteristic)
        {
            string name = null;
            switch (characteristic)
            {
                case "OneSaber":
                    name = "OneColorBeatmapCharacteristic";
                    break;
                case "NoArrows":
                    name = "NoArrowsBeatmapCharacteristic";
                    break;
                case "Standard":
                    name = "StandardBeatmapCharacteristic";
                    break;
                case "Lightshow":
                    name = "LightshowBeatmapCharacteristic";
                    break;
                case "Lawless":
                    name = "LawlessBeatmapCharacteristic";
                    break;
                case "360Degree":
                    name = "360DegreeBeatmapCharacteristic";
                    break;
                case "90Degree":
                    name = "90DegreeBeatmapCharacteristic";
                    break;
            }
            return name;
        }
        //string playlist = @"C:\Program Files (x86)\Steam\steamapps\common\Beat Saber\Playlists\SongBrowserPluginFavorites.json";
        //string customSongsFolder2 = @"C:\Program Files (x86)\Steam\steamapps\common\Beat Saber\CustomSongs";
        //string copyToFolder = @"C:\Users\VR\Desktop\platform-tools_r28.0.3-windows\dist\ToConvert";
        //dynamic favs = JObject.Parse(File.ReadAllText(playlist));

        //JArray songs = favs["songs"] as JArray;
        //var songsToCopy = (from x in songs select x["key"].Value<string>()).ToList();
        //foreach (var key in songsToCopy)
        //{
        //    if (string.IsNullOrWhiteSpace(key))
        //        continue;
        //    string songPath = Path.Combine(customSongsFolder2, key) +"\\";
        //    if (!Directory.Exists(songPath))
        //    {
        //        Log.LogErr($"Missing custom song: {songPath}");
        //        continue;
        //    }
        //    try
        //    {
        //        new Microsoft.VisualBasic.Devices.Computer().FileSystem.CopyDirectory(songPath, copyToFolder);
        //    } catch
        //    {
        //        Log.LogErr($"Error copying {songPath}");
        //    }
        //}
        //return;
    }
}
