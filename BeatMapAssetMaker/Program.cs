﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Diagnostics;
using Emulamer.Utils;
using QuestomAssets;
using QuestomAssets.AssetsChanger;
using QuestomAssets.BeatSaber;
using System.Drawing;
using CommandLine;
using Newtonsoft.Json;

namespace BeatmapAssetMaker
{
    [Verb("getconfig", HelpText = "Get the current configuration")]
    public class OutputConfig
    {
        [Option('a', "apk-file", Required =true, HelpText = "The Beat Saber APK file to load configuration from")]
        public string ApkFile { get; set; }

        [Option('o', "output-to-file", Required = false, HelpText = "The name of the file to output.  If not specified, writes to stdout (and logs will be suppressed).")]
        public string OutputFile { get; set; }

        [Option('l', "force-log", Required = false, HelpText = "When outputting to stdout, forces log output enabled.  JSON will appear between the tokens /* JSON START */ and /* JSON END */")]
        public bool ForceLog { get; set; }

        [Option("no-images", Required = false, HelpText ="Do not include image data in outupt.")]
        public bool NoImages { get; set; }
        //[Option("nocovers", Required = false, Default = false, HelpText = "Do not export image base64 data for cover art.")]
        //public bool NoCovers { get; set; }  
    }

    [Verb("updateconfig", HelpText = "Updates the configuration.")]
    public class UpdateConfig
    {
        [Option('a', "apk-file", Required = true, HelpText = "The Beat Saber APK file to load configuration from.")]
        public string ApkFile { get; set; }

        [Option('i', "input-file", Required = false, HelpText = "The name of the file to input.  If not specified, reads from stdin.")]
        public string InputFile { get; set; }

        [Option("nopatch", Required = false, Default = false, HelpText = "Skip patching the executable.")]
        public bool NoPatch { get; set; }

        //[Option("nocovers", Required = false, Default = false, HelpText = "Skip importing cover art")]
        //public bool NoCovers { get; set; }        
    }

    [Verb("loadfolder", HelpText = "Loads songs from a folder to a 'Custom Songs' playlist.")]
    public class FolderMode
    {
        [Option('a', "apk-file", Required = true, HelpText = "The Beat Saber APK file to load configuration from.")]
        public string ApkFile { get; set; }

        [Option('i', "input-folder", Required = true, HelpText = "The folder containing the custom songs to load.")]
        public string CustomSongsFolder { get; set; }

        [Option("nopatch", Required = false, Default = false, HelpText = "Skip patching the executable.")]
        public bool NoPatch { get; set; }

        [Option('d', "delete-songs", Required = false, HelpText = "Deletes existing songs that aren't in the custom songs folders.")]
        public bool DeleteSongs { get; set; }
        //[Option("nocovers", Required = false, Default = false, HelpText = "Skip importing cover art")]
        //public bool NoCovers { get; set; }
    }


    [Verb("loadsaber", HelpText = "Loads a custom saber from a folder and replaces the current saber with it.")]
    public class SaberMode
    {
        [Option('a', "apk-file", Required = true, HelpText = "The Beat Saber APK file to load configuration from.")]
        public string ApkFile { get; set; }

        [Option('i', "input-folder", Required = true, HelpText = "The folder containing the custom saber to load.")]
        public string CustomSaberFolder { get; set; }

        [Option("nopatch", Required = false, Default = false, HelpText = "Skip patching the executable.")]
        public bool NoPatch { get; set; }

    }

    class Program
    {

        static int Main(string[] args)
        {
            return CommandLine.Parser.Default.ParseArguments<OutputConfig, UpdateConfig, FolderMode>(args)
              .MapResult(
                (OutputConfig opts) => OutputConfig(opts),
                (UpdateConfig opts) => UpdateConfig(opts),
                (FolderMode opts) => FolderMode(opts),
                errs => 1);
        }

        static int UpdateConfig(UpdateConfig args)
        {
            QuestomAssets.Log.SetLogSink(new ConsoleSink());

            try
            {
                Log.LogMsg($"Opening APK at '{args.ApkFile}'");
                using (QuestomAssetsEngine q = new QuestomAssetsEngine(args.ApkFile))
                {
                    BeatSaberQuestomConfig config = null;
                    TextReader inReader = null;
                    string from = string.IsNullOrWhiteSpace(args.InputFile) ? "stdin" : args.InputFile;
                    Log.LogMsg($"Reading configuration from {from}...");
                    try
                    {
                        if (string.IsNullOrWhiteSpace(args.InputFile))
                        {
                            inReader = Console.In;
                        }
                        else
                        {
                            inReader = new StreamReader(args.InputFile);
                        }
                        using (var jReader = new JsonTextReader(inReader))
                            config = new JsonSerializer().Deserialize<BeatSaberQuestomConfig>(jReader);
                    }
                    finally
                    {
                        if (inReader != null)
                        {
                            inReader.Dispose();
                            inReader = null;
                        }
                    }
                    
                    Log.LogMsg($"Config parsed");

                    Log.LogMsg("Applying new configuration...");
                    q.UpdateConfig(config);
                    Log.LogMsg("Configuration updated");

                    if (!args.NoPatch)
                        q.ApplyBeatmapSignaturePatch();

                    Log.LogMsg("Signing and saving APK...");
                }
                Log.LogMsg("APK saved");
                return 0;
            }
            catch (Exception ex)
            {
                Log.LogErr("Something went horribly wrong", ex);
                return -1;
            }
        }

        static int OutputConfig(OutputConfig args)
        {
            if (!string.IsNullOrWhiteSpace(args.OutputFile) || args.ForceLog)
            {
                Log.SetLogSink(new ConsoleSink());
            }

            try
            {
                Log.LogMsg($"Opening APK at '{args.ApkFile}'");
                using (QuestomAssetsEngine q = new QuestomAssetsEngine(args.ApkFile, true))
                {
                    Log.LogMsg($"Loading configuration...");
                    var cfg = q.GetCurrentConfig(args.NoImages);
                    Log.LogMsg($"Configuration loaded");

                    TextWriter outWriter = null;
                    try
                    {
                        string toPlace = string.IsNullOrWhiteSpace(args.OutputFile) ? "stdout" : args.OutputFile;
                        Log.LogMsg($"Writing configuration to {toPlace}...");
                        if (string.IsNullOrWhiteSpace(args.OutputFile))
                        {
                            //write to stdout
                            outWriter = Console.Out;
                            outWriter.WriteLine("/* JSON START */");
                        }
                        else
                        {
                            //write to file
                            outWriter = new StreamWriter(new FileStream(args.OutputFile, FileMode.Create));
                        }
                        var ser = new JsonSerializer();
                        ser.Formatting = Formatting.Indented;
                        ser.Serialize(outWriter, cfg);

                        if (string.IsNullOrWhiteSpace(args.OutputFile))
                        {
                            outWriter.WriteLine("");
                            outWriter.WriteLine("/* JSON END */");
                        }
                    }
                    finally
                    {
                        if (outWriter != null)
                        {
                            outWriter.Dispose();
                            outWriter = null;
                        }
                    }
                }
                return 0;
            }
            catch (Exception ex)
            {
                Log.LogErr("Something went horribly wrong", ex);
                return -1;
            }
        }

        static int FolderMode(FolderMode args)
        {
            QuestomAssets.Log.SetLogSink(new ConsoleSink());

            var customSongsFolders = GetCustomSongsFromPath(args.CustomSongsFolder);
            if (customSongsFolders.Count < 1)
            {
                Log.LogErr("No custom songs found!");
                return -1;
            }
            try
            {
                Log.LogMsg($"Opening APK at '{args.ApkFile}'");
                using (QuestomAssetsEngine q = new QuestomAssetsEngine(args.ApkFile))
                {
                    Log.LogMsg($"Loading configuration...");
                    var cfg = q.GetCurrentConfig();
                    Log.LogMsg($"Configuration loaded");
                    
                    BeatSaberPlaylist playlist = cfg.Playlists.FirstOrDefault(x => x.PlaylistID == "CustomSongs");
                    if (playlist == null)
                    {
                        Log.LogMsg("Playlist doesn't already exist, creating it");
                        playlist = new BeatSaberPlaylist()
                        {
                            PlaylistID = "CustomSongs",
                            PlaylistName = "Custom Songs"
                        };
                        cfg.Playlists.Add(playlist);
                    }
                    else if (args.DeleteSongs)
                    {
                        Log.LogMsg("Deleting current songs from playlist before reloading");
                        playlist.SongList.Clear();
                    }

                    Log.LogMsg($"Attempting to load {customSongsFolders.Count} custom songs...");
                    foreach (var cs in customSongsFolders)
                    {
                        playlist.SongList.Add(new BeatSaberSong()
                        {
                            CustomSongFolder = cs
                        });
                    }
                    if (!args.NoPatch)
                        q.ApplyBeatmapSignaturePatch();

                    Log.LogMsg("Applying new configuration...");
                    q.UpdateConfig(cfg);
                    Log.LogMsg("Configuration updated");

                    Log.LogMsg("Signing and saving APK...");
                }
                Log.LogMsg("APK saved");
                return 0;
            }
            catch (Exception ex)
            {
                Log.LogErr("Something went horribly wrong", ex);
                return -1;
            }            
        }

        static List<string> GetCustomSongsFromPath(string path)
        {
            List<string> customSongsFolders = new List<string>();

            if (File.Exists(Path.Combine(path, "Info.dat")))
            {
                //do one
                Log.LogErr("Found Info.dat in customSongsFolder, injecting one custom song.");
                customSongsFolders.Add(path);
            }
            else
            {
                //do many
                List<string> foundSongs = Directory.EnumerateDirectories(path).Where(y => File.Exists(Path.Combine(y, "Info.dat"))).ToList();
                Log.LogErr($"Found {foundSongs.Count()} custom songs to inject");
                customSongsFolders.AddRange(foundSongs);
            }
            return customSongsFolders;
        }

        static void Usage()
        {
            Console.WriteLine("Usage: BeatmapAssetMaker <mode> [-nocovers] [-nopatch] <apkFile> <customSongsFolder>");
            Console.WriteLine("\tModes:");
            Console.WriteLine("\t\toutputconfig");
            Console.WriteLine("\t\tupdateconfig");
            Console.WriteLine("\t\toldmode");

            Console.WriteLine("\tapkFile: the path to the APK.");
            Console.WriteLine("\tcustomSongsFolder: the folder that contains folders of custom songs in the new native beatsaber format, or a folder with a single song that contains an Info.dat file");
            Console.WriteLine("\tIf you want to skip importing covers then add \"nocovers\" at the end.");
            Console.WriteLine("\tIf you want to skip patching the binary to allow custom songs, add \"nopatch\" at the end.");
            return;
        }
    }
}
