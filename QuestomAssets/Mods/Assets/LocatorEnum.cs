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
    /// <summary>
    /// The predefined locators that can be used.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum LocatorEnum
    {
        Saber,
        Note,
        MenuTitle,
        Trail,
        Platform
    }
    public class LocatorEnumHelper
    {
        private const string FileName = "locator-assets.json";
        private const string DownloadPath = "https://raw.githubusercontent.com/BMBF/resources/master/assets/locator-assets.json";
        private static string path;
        public static Dictionary<string, Dictionary<LocatorEnum, string>> Versions { get; set; } = new Dictionary<string, Dictionary<LocatorEnum, string>>();
        /// <summary>
        /// Gets the Assets filename of the given LocatorEnum and Beat Saber Version.
        /// TODO: Make this perform a web request/local file read as opposed to hardcoded.
        /// </summary>
        /// <param name="e"></param>
        /// <param name="bsV"></param>
        /// <returns></returns>
        private static string InternalGetFile(LocatorEnum? e, string bsV)
        {
            if (!e.HasValue)
                return null;
            if (Versions.ContainsKey(bsV))
            {
                if (Versions[bsV].ContainsKey(e.Value))
                    return Versions[bsV][e.Value];
                else
                    Log.LogErr($"Could not find matching asset for version: {bsV} and enum: {e.Value}");
            } else
            {
                Log.LogErr($"Could not find matching version: {bsV}");
                Log.LogMsg($"Falling back to most recent version, which may cause issues!");
                var lastKey = Versions.Keys.LastOrDefault();
                if (Versions[lastKey].ContainsKey(e.Value))
                    return Versions[lastKey][e.Value];
                else
                    Log.LogErr($"Could not find matching asset for version: {lastKey} and enum: {e.Value}");
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
            } catch (Exception e)
            {
                Log.LogErr($"Could not download file!", e);
            }
            if (File.Exists(filePath))
            {
                try
                {
                    Versions = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<LocatorEnum, string>>>(File.ReadAllText(filePath));
                    Log.LogMsg($"Deserialized {filePath} for LocatorEnumHelper!");
                } catch (JsonException e)
                {
                    Log.LogErr($"Failed to deserialize the file: {filePath}!", e);
                }
            }
        }
        public static string GetFile(LocatorEnum? e, string bsV)
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
