﻿using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Linq;

namespace QuestomAssets.BeatSaber
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum SaberInfoFormat
    {
        Dat,
        Saber
    }

    public class SaberInfo
    {
        public SaberInfoFormat Format { get; set; }
        public string ID { get; set; }
        // Null if loading a .saber
        public DatFileInfo DatFiles { get; set; }
        // Null if loading .dat files
        public List<string> AssetsFiles { get; set; }
        public class DatFileInfo
        {
            public string SaberBlade { get; set; }
            public string SaberGlowingEdges { get; set; }
            public string SaberHandle { get; set; }

            public byte[] SaberBladeBytes { get; set; }
            public byte[] SaberGlowingEdgesBytes { get; set; }
            public byte[] SaberHandleBytes { get; set; }
        }

        //this could be done much better
        public static SaberInfo FromFolderOrZip(string folderOrZip)
        {
            if (folderOrZip.EndsWith(".saber"))
            {
                BundleFileProvider provider = new BundleFileProvider(folderOrZip);
                return new SaberInfo()
                {
                    Format = SaberInfoFormat.Saber,
                    ID = Path.GetFileNameWithoutExtension(folderOrZip),
                    DatFiles = null,
                    AssetsFiles = provider.FindFiles("*")
                };
            }
            try
            {
                if (File.Exists(folderOrZip))
                {
                    Log.LogMsg("Found a file, maybe a zip file.  Going to use that to load the saber from.");
                    //do zip file mode

                    using (Ionic.Zip.ZipFile zip = new Ionic.Zip.ZipFile(folderOrZip))
                    {
                        var saberInfoEntry = zip.Entries.FirstOrDefault(x => x.FileName == "saberinfo.dat");
                        if (saberInfoEntry == null)
                        {
                            Log.LogErr($"Unable to find saberinfo.dat in {folderOrZip}.");
                            return null;
                        }
                        string saberInfoString;
                        using (StreamReader sr = new StreamReader(saberInfoEntry.OpenReader()))
                            saberInfoString = sr.ReadToEnd();
                        var saberInfo = JsonConvert.DeserializeObject<SaberInfo>(saberInfoString);
                        if (saberInfo == null || saberInfo.DatFiles == null)
                        {
                            Log.LogErr($"Unable to deserialize saberinfo.dat in {folderOrZip}.");
                            return null;
                        }
                        var saberBladeEntry = zip.Entries.FirstOrDefault(x => x.FileName == saberInfo.DatFiles.SaberBlade);
                        var saberGlowingEdgesEntry = zip.Entries.FirstOrDefault(x => x.FileName == saberInfo.DatFiles.SaberGlowingEdges);
                        var saberHandleEntry = zip.Entries.FirstOrDefault(x => x.FileName == saberInfo.DatFiles.SaberHandle);
                        if (saberBladeEntry == null)
                        {
                            Log.LogErr($"Unable to load saber blade {saberInfo.DatFiles.SaberBlade} from {folderOrZip}");
                            return null;
                        }
                        if (saberGlowingEdgesEntry == null)
                        {
                            Log.LogErr($"Unable to load saber glowing edges {saberInfo.DatFiles.SaberGlowingEdges} from {folderOrZip}");
                            return null;
                        }
                        if (saberHandleEntry == null)
                        {
                            Log.LogErr($"Unable to load saber handle {saberInfo.DatFiles.SaberHandle} from {folderOrZip}");
                            return null;
                        }
                        using (var br = new BinaryReader(saberBladeEntry.OpenReader()))
                            saberInfo.DatFiles.SaberBladeBytes = br.ReadBytes((int)br.BaseStream.Length);
                        using (var br = new BinaryReader(saberGlowingEdgesEntry.OpenReader()))
                            saberInfo.DatFiles.SaberGlowingEdgesBytes = br.ReadBytes((int)br.BaseStream.Length);
                        using (var br = new BinaryReader(saberHandleEntry.OpenReader()))
                            saberInfo.DatFiles.SaberHandleBytes = br.ReadBytes((int)br.BaseStream.Length);
                        return saberInfo;
                    }
                }
                else if (Directory.Exists(folderOrZip))
                {
                    Log.LogMsg("Found a folder, maybe a saber folder.  Going to use that to load the saber from.");
                    //do file
                    string infofilename = Path.Combine(folderOrZip, "saberinfo.dat");
                    if (!File.Exists(infofilename))
                    {
                        Log.LogMsg($"Didn't find saberinfo.dat in {folderOrZip}.");
                        return null;
                    }
                    string infostring = File.ReadAllText(infofilename);
                    SaberInfo info = JsonConvert.DeserializeObject<SaberInfo>(infostring);
                    if (info == null || info.DatFiles == null)
                    {
                        Log.LogMsg("Unable to deserialize saberinfo.dat");
                        return null;
                    }
                    string saberBladeFile = Path.Combine(folderOrZip, info.DatFiles.SaberBlade);
                    string saberGlowingBladeFile = Path.Combine(folderOrZip, info.DatFiles.SaberGlowingEdges);
                    string saberHandleFile = Path.Combine(folderOrZip, info.DatFiles.SaberHandle);
                    if (!File.Exists(saberBladeFile))
                    {
                        Log.LogMsg($"Unable to locate {saberBladeFile}");
                        return null;
                    }
                    if (!File.Exists(saberGlowingBladeFile))
                    {
                        Log.LogMsg($"Unable to locate {saberGlowingBladeFile}");
                        return null;
                    }
                    if (!File.Exists(saberHandleFile))
                    {
                        Log.LogMsg($"Unable to locate {saberHandleFile}");
                        return null;
                    }
                    info.DatFiles.SaberBladeBytes = File.ReadAllBytes(saberBladeFile);
                    info.DatFiles.SaberGlowingEdgesBytes = File.ReadAllBytes(saberGlowingBladeFile);
                    info.DatFiles.SaberHandleBytes = File.ReadAllBytes(saberHandleFile);
                    return info;
                }
                else
                {
                    Log.LogErr($"No saber folder or zipfile was found at {folderOrZip}.");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Log.LogErr($"Exception loading saber info from {folderOrZip}.", ex);
                return null;
            }
        }
    }
}
