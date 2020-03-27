using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using QuestomAssets.AssetsChanger;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace QuestomAssets.Mods.Assets
{
    public class DynamicLocatorHelper
    {
        private const string FileName = "locator-assets.json";
        private const string DownloadPath = "https://raw.githubusercontent.com/BMBF/resources/master/assets/locator-assets.json";
        private static string path;
        public static Dictionary<string, Dictionary<string, string>> Versions { get; set; } = new Dictionary<string, Dictionary<string, string>>();

        /// <summary>
        /// Gets the Assets filename of the given string and Beat Saber Version.
        /// </summary>
        /// <param name="e"></param>
        /// <param name="bsV"></param>
        /// <returns></returns>
        private static string InternalGetFile(string e, string bsV)
        {
            if (string.IsNullOrWhiteSpace(e))
                return null;
            if (Versions.TryGetValue(bsV, out var dict))
            {
                if (dict.TryGetValue(e, out string val))
                    return val;
                else
                    Log.LogErr($"Could not find matching asset for version: {bsV} and string: {e}");
            }
            else
            {
                Log.LogErr($"Could not find matching version: {bsV}");
                Log.LogMsg($"Falling back to most recent version, which may cause issues!");
                var lastKey = Versions.Keys.LastOrDefault();
                if (Versions[lastKey].TryGetValue(e, out string temp))
                    return temp;
                else
                    Log.LogErr($"Could not find matching asset for version: {lastKey} and string: {e}");
            }
            return null;
        }

        public static void ParseLatest(string rootPath)
        {
            // Try to download the file
            var filePath = rootPath.CombineFwdSlash(FileName);
            path = filePath;
            try
            {
                using (var client = new WebClient())
                {
                    Log.LogMsg($"Attempting to download from: {DownloadPath}");
                    client.DownloadFile(DownloadPath, filePath);
                }
            }
            catch (Exception e)
            {
                Log.LogErr($"Could not download file!", e);
            }
            if (File.Exists(filePath))
            {
                try
                {
                    Versions = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(File.ReadAllText(filePath));
                    Log.LogMsg($"Deserialized {filePath} for LocatorEnumHelper!");
                }
                catch (JsonException e)
                {
                    Log.LogErr($"Failed to deserialize the file: {filePath}!", e);
                }
            }
        }

        public static string GetFile(string e, string bsV)
        {
            if (Versions.Count == 0)
            {
                // Attempt to redownload
                ParseLatest(path);
            }
            return InternalGetFile(e, bsV);
        }
    }
}