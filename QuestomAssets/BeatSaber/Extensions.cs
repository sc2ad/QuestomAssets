﻿using System;
using System.Collections.Generic;
using System.Text;
using QuestomAssets.AssetsChanger;
using System.IO;
using System.Linq;

namespace QuestomAssets.BeatSaber
{
    public static class Extensions
    {
        private static string FindFirstOfSplit(IAssetsFileProvider fp, string assetsFile)
        {
            int lastDot = assetsFile.LastIndexOf('.');
            if (lastDot == -1)
            {
                // Couldn't find the last dot, but this is still a split conversion
                // We should check to make sure that there might be some split files with a . added at the end
                // of the string
                // SUPER HACK! To make sure that globalgameassets still loads properly
                if (assetsFile.Contains("level") && fp.FileExists(assetsFile + ".split0"))
                {
                    return assetsFile + ".split0";
                }
            }
            if (lastDot > 0)
            {
                
                string afterDot = assetsFile.Substring(lastDot, assetsFile.Length - lastDot);
                string noSplit;
                if (afterDot.ToLower().StartsWith(".split"))
                {
                    noSplit = assetsFile.Substring(0, lastDot);
                    if (fp.FileExists(noSplit))
                        return noSplit;
                }
                else
                {
                    noSplit = assetsFile;
                }
                if (fp.FileExists(noSplit))
                    return noSplit;

                var split0 = noSplit + ".split0";
                if (fp.FileExists(split0))
                    return split0;
            }
            else if (fp.FileExists(assetsFile))
            {
                return assetsFile;
            }
            else if (fp.FileExists(assetsFile + ".split0"))
            {
                return assetsFile + ".split0";
            }

            return null;            
        }

        public static string CorrectAssetFilename(this IAssetsFileProvider fp, string assetsFile)
        {
            var correctName = FindFirstOfSplit(fp, assetsFile);
            if (correctName != null)
                return correctName;
            //some of the files in ExternalFiles have library/ on them, but they're actually in the root path
            var splitPath = assetsFile.Split('/').ToList();
            if (splitPath.Count() > 1)
            {
                splitPath.RemoveAt(splitPath.Count - 2);
                correctName = String.Join("/", splitPath);
                correctName = FindFirstOfSplit(fp, correctName);
                if (correctName != null)
                    return correctName;
            }

            throw new ArgumentException("The file doesn't exist in the APK with any name!");
        }
        public static void WriteCombinedAssets(this IAssetsFileProvider fp, AssetsFile assetsFile, string assetsFilePath)
        {
            if (assetsFilePath.EndsWith("split0"))
                throw new ArgumentException("Don't pass in filenames with split0, pass in the original.");
            fp.DeleteFiles(assetsFilePath + ".split*");
            using (var ms = new MemoryStream())
            {
                assetsFile.Write(ms);
                ms.Seek(0, SeekOrigin.Begin);
                fp.Write(assetsFilePath, ms.ToArray(), true, true);
            }
        }

        public static Stream ReadCombinedAssets(this IAssetsFileProvider fp, string assetsFilePath)
        {
            string actualName = fp.CorrectAssetFilename(assetsFilePath);

            List<string> assetFiles = new List<string>();
            if (actualName.ToLower().EndsWith("split0"))
            {
                assetFiles.AddRange(fp.FindFiles(actualName.Replace(".split0", ".split*"))
                    .OrderBy(x => Convert.ToInt32(x.Split(new string[] { ".split" }, StringSplitOptions.None).Last())));
            }
            else
            {
                return fp.GetReadStream(actualName);
            }
            MemoryStream msFullFile = new MemoryStream();
            foreach (string assetsFile in assetFiles)
            {
                byte[] fileBytes = fp.Read(assetsFile);
                msFullFile.Write(fileBytes, 0, fileBytes.Length);
            }

            return msFullFile;
        }

    }
}
