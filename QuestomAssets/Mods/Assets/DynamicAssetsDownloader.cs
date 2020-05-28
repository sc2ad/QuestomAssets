using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using QuestomAssets.AssetsChanger;
using QuestomAssets.Download;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace QuestomAssets.Mods.Assets
{
    public class DynamicAssetsDownloader : IDynamicAssetsProvider
    {
        private const string FileName = "locator-assets.json";
        private static readonly Uri DownloadPath = new Uri("https://raw.githubusercontent.com/BMBF/resources/master/assets/locator-assets.json");
        private readonly IDownloadService _client;

        private static string path;
        public static Dictionary<string, Dictionary<string, string>> Versions { get; set; } = new Dictionary<string, Dictionary<string, string>>();

        public DynamicAssetsDownloader(IDownloadService client, string rootPath)
        {
            _client = client;
            ParseLatest(rootPath);
        }

        /// <summary>
        /// Gets the Assets filename of the given string and Beat Saber Version.
        /// </summary>
        /// <param name="e"></param>
        /// <param name="bsV"></param>
        /// <returns></returns>
        private string InternalGetFile(string e, string bsV)
        {
            if (string.IsNullOrWhiteSpace(e))
                return null;
            if (Versions == null)
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
                // TODO: Change this to actually get the latest version, instead of the last one in the JSON
                var lastKey = Versions.Keys.LastOrDefault();
                if (Versions[lastKey].TryGetValue(e, out string temp))
                    return temp;
                else
                    Log.LogErr($"Could not find matching asset for version: {lastKey} and string: {e}");
            }
            return null;
        }

        public void ParseLatest(string rootPath)
        {
            // Try to download the file
            var filePath = rootPath.CombineFwdSlash(FileName);
            path = filePath;
            try
            {
                Log.LogMsg("Attempting to download file!");
                _client.DownloadFile(DownloadPath, filePath);
            }
            catch (Exception e)
            {
                Log.LogErr("Could not download file!", e);
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

        public string GetFile(string assetName, string version)
        {
            if (Versions.Count == 0)
            {
                // Attempt to redownload
                ParseLatest(path);
            }
            return InternalGetFile(assetName, version);
        }
    }
}