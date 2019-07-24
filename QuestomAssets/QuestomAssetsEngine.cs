﻿using System;
using System.IO;
using QuestomAssets.AssetsChanger;
using QuestomAssets.BeatSaber;
using System.Collections.Generic;
using System.Linq;
using QuestomAssets.Utils;
using Newtonsoft.Json;
using QuestomAssets.Models;
using System.Diagnostics;
using System.Threading;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using QuestomAssets.AssetOps;
using QuestomAssets.Mods;

namespace QuestomAssets
{

    public class QuestomAssetsEngine : IDisposable
    {
        public bool HasChanges
        {
            get
            {
                return Manager.HasChanges || ModManager.HasChanges || QueuedFileOperations.Count > 0;
            }
        }
        private CustomLevelLoader _loader;
        private List<string> _assetsLoadOrder = new List<string>();
        private AssetsManager _manager;
        internal AssetsManager Manager { get => _manager;  }
        internal MusicConfigCache MusicCache { get => _musicCache; }
        public ModManager ModManager { get; private set; }
        private MusicConfigCache _musicCache;
        internal List<ModDefinition> _modCache;
        private AssetOpManager _opManager;
        public AssetOpManager OpManager { get => _opManager; }
        public IFileProvider FileProvider
        {
            get
            {
                return _config.RootFileProvider;
            }
        }

        //public string AssetsRootPath { get; private set; }

        public bool HideOriginalPlaylists { get; private set; } = false;
        private QaeConfig _config;
        internal QaeConfig Config { get => _config; }

        internal List<QueuedFileOp> QueuedFileOperations { get; } = new List<QueuedFileOp>();
        
        /// <summary>
        /// Create a new instance of the class and open the apk file
        /// </summary>
        public QuestomAssetsEngine(QaeConfig config)
        {
            _config = config;
            _assetsLoadOrder = GetAssetsLoadOrderFile();
            if (_assetsLoadOrder == null)
            {
                _assetsLoadOrder = new List<string>()
                {
                    "globalgamemanagers",
                    "globalgamemanagers.assets",
                    "sharedassets1.assets",
                    "231368cb9c1d5dd43988f2a85226e7d7",
                    "17c37b4ad5b2b5046be37e2524b67216",
                    "92d85d84be1369e4ab3b35188d1ea8b6",
                    "b79ca5d157a731a45a945697ad0820c8",
                    "8e37fa14e51257645b7a2df8aecb19e1",
                    "sharedassets11.assets",
                    "sharedassets18.assets",
                    "sharedassets20.assets"
                };
            }
            Stopwatch sw = new Stopwatch();
            _manager = new AssetsManager(_config.RootFileProvider, _config.AssetsPath, BSConst.GetAssetTypeMap());
            Log.LogMsg("Preloading files...");
            sw.Start();
            PreloadFiles();
            sw.Stop();
            Log.LogMsg($"Preload files took {sw.ElapsedMilliseconds}ms");
            _musicCache = new MusicConfigCache(GetMainLevelPack());
            _opManager = new AssetOpManager(new OpContext(this));
            ModManager = new ModManager(_config, () => this);
        }

        public BeatSaberQuestomConfig GetCurrentConfig()
        {
            var config = GetConfig();

            return config;
        }

        //for some reason, I think this is a bad idea.
        public bool IsManagerLocked()
        {
            bool entered = Monitor.TryEnter(_manager);
            if (entered)
                Monitor.Exit(_manager);
            return !entered;
        }

        private List<AssetOp> DiffModConfig(BeatSaberQuestomConfig config)
        {
            ModManager.ResetCache();
            var toAddIDs = config.Mods.Where(x => x.Status == ModStatusType.Installed && !ModManager.Mods.Any(y => y.ID == x.ID && y.Status == ModStatusType.Installed)).Select(x => x.ID);
            var toRemoveIDs = ModManager.Mods.Where(x => x.Status == ModStatusType.Installed && !config.Mods.Any(y => y.Status == ModStatusType.Installed && y.ID == x.ID)).Select(x=> x.ID);
            List<ModDefinition> toAdd = new List<ModDefinition>();
            List<ModDefinition> toRemove = new List<ModDefinition>();
            var fullMods = ModManager.Mods;
            foreach (var addID in toAddIDs)
            {
                var fullMod = fullMods.FirstOrDefault(x => x.ID == addID);
                if (fullMod == null)
                {
                    Log.LogErr($"Added mod ID {addID} was not found, which probably means it isn't in the Mods/ folder.");
                    throw new Exception($"Added mod ID {addID} was not found, which probably means it isn't in the Mods/ folder.");
                }
                toAdd.Add(fullMod);
            }
            foreach (var removeID in toRemoveIDs)
            {
                var fullMod = fullMods.FirstOrDefault(x => x.ID == removeID);
                if (fullMod == null)
                {
                    Log.LogErr($"Removed mod ID {removeID} was not found, which probably means it isn't in the Mods/ folder.");
                    throw new Exception($"Removed mod ID {removeID} was not found, which probably means it isn't in the Mods/ folder.");
                }
                toRemove.Add(fullMod);
            }

            List<AssetOp> ops = new List<AssetOp>();

            toRemove.ForEach(x => ops.AddRange(ModManager.GetUninstallModOps(x)));
            toAdd.ForEach(x => ops.AddRange(ModManager.GetInstallModOps(x)));
            return ops;
        }

        public void UpdateConfig(BeatSaberQuestomConfig config)
        {
            Stopwatch sw = new Stopwatch();
            lock (this)
            {
                //todo: basic validation of the config

                //TODO; fix
                //UpdateColorConfig(config.Colors);

                //TODO: something broke
                //UpdateTextConfig(manager, config.TextChanges);
                List<AssetOp> ops = new List<AssetOp>();
                if (config.Mods != null)
                {
                    using (new LogTiming("Diffing mod config"))
                    {
                        ops.AddRange(DiffModConfig(config));
                    }
                }
                else
                {
                    Log.LogMsg("Mods config is null, not changing mods");
                }

                if (config.Playlists != null)
                {
                    Log.LogMsg("Updating music config...");

                    using (new LogTiming("Diffing music config"))
                    {
                        ops.AddRange(DiffMusicConfig(config));
                    }                    
                }
                else
                {
                    Log.LogMsg("Playlists is null, song configuration will not be changed.");
                }

                if (ops.Count < 1)
                {
                    Log.LogMsg("No changes needed to config.");
                }
                else
                {
                    List<AssetOp> failed = new List<AssetOp>();
                    Log.LogMsg($"{ops.Count} Ops to update music config");
                    OpManager.OpStatusChanged += (a, e) =>
                    {
                        if (e.Status == OpStatus.Failed)
                        {
                            failed.Add(e);
                        }
                    };

                    ops.ForEach(x => OpManager.QueueOp(x));
                    Log.LogMsg($"Ops queued, waiting for all to complete...");
                    using (new LogTiming("waiting for ops to complete"))
                    {
                        ops.WaitForFinish();
                    }

                    if (failed.Count > 0)
                    {
                        Log.LogErr($"{failed.Count} ops failed while updating config.");
                        failed.ForEach(x => Log.LogErr($"{x.GetType().Name} failed: {x.Exception.Message}"));
                        throw new AssetOpsException("At least one internal operation failed while updating the config", failed);
                    }
                }
            }
        }

        public void Save()
        {
            lock (this)
            {
                if (!Monitor.TryEnter(_manager))
                    throw new Exception("Other operations are in progress, cannot save now.");
                
                    Stopwatch sw = new Stopwatch();
                    try
                    {
                        Log.LogMsg("Serializing all assets...");
                        sw.Restart();
                        _manager.WriteAllOpenAssets();
                        sw.Stop();
                        Log.LogMsg($"Serialization of assets took {sw.ElapsedMilliseconds}ms");

                        Log.LogMsg("Making sure everything is saved...");
                        FileProvider.Save();


                        if (ModManager.HasChanges)
                        {
                            using (new LogTiming("Saving mod status"))
                            {
                                ModManager.Save();
                            }
                        }
                        else
                        {
                            Log.LogMsg("ModManager has no changes, not saving it.");
                        }

                        if (QueuedFileOperations.Count > 0)
                        {
                            using (new LogTiming("Performing queued file operations"))
                            {
                                foreach (var qfo in QueuedFileOperations.ToList())
                                {
                                    try
                                    {
                                        qfo.PerformFileOperation(_config);
                                        QueuedFileOperations.Remove(qfo);
                                    }
                                    catch (Exception ex)
                                    {
                                        Log.LogErr($"Queued file operation of type {qfo.Type} for target {qfo.TargetPath} failed!", ex);
                                        throw new Exception($"Queued file operation of type {qfo.Type} for target {qfo.TargetPath} failed!", ex);
                                    }
                                }
                            }
                        }
                        else
                        {
                            Log.LogMsg("No queued file operations to perform");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.LogErr("Exception saving assets!", ex);
                        throw new Exception("Failed to save assets!", ex);
                    }
                finally
                {
                    Monitor.Exit(_manager);
                }
            }
        }

        private MainLevelPackCollectionObject _mainLevelPackCache;
        internal MainLevelPackCollectionObject GetMainLevelPack()
        {
            if (_mainLevelPackCache == null)
            {
                var mainLevelPack = _manager.MassFirstOrDefaultAsset<MainLevelPackCollectionObject>(x => true, false)?.Object;
                if (mainLevelPack == null)
                    throw new Exception("Unable to find the main level pack collection object!");
                _mainLevelPackCache = mainLevelPack;
            }
            return _mainLevelPackCache;
        }

        private AssetsFile _songsAssetsFileCache;
        internal AssetsFile GetSongsAssetsFile()
        {
            if (_songsAssetsFileCache == null)
            {
                var extrasPack = _manager.MassFirstOrDefaultAsset<BeatmapLevelPackObject>(x => x.Object.Name == "ExtrasLevelPack", false);
                if (extrasPack == null)
                    throw new Exception("Unable to find the file that ExtrasLevelPack is in!");
                _songsAssetsFileCache = extrasPack.ParentFile;
            }
            return _songsAssetsFileCache;
        }

        private AlwaysOwnedContentModel _aoModelCache;
        internal AlwaysOwnedContentModel GetAlwaysOwnedModel()
        {
            if (_aoModelCache == null)
            {
                var aoModel = _manager.MassFirstOrDefaultAsset<AlwaysOwnedContentModel>(x => x.Object.Name == "DefaultAlwaysOwnedContentModel", false);
                if (aoModel == null)
                    throw new Exception("Unable to find AlwaysOwnedContentModel!");
                _aoModelCache = aoModel.Object;
            }

            return _aoModelCache;
        }

        private void UpdatePlaylistConfig(AssetsFile songsAssetFile, BeatSaberPlaylist playlist)
        {
            Log.LogMsg($"Processing playlist ID {playlist.PlaylistID}...");
            CustomLevelLoader loader = new CustomLevelLoader(songsAssetFile, _config);
            BeatmapLevelPackObject levelPack = songsAssetFile.FindAsset<BeatmapLevelPackObject>(x => x.Object.PackID == playlist.PlaylistID)?.Object;
            //create a new level pack if one wasn't found
            if (levelPack == null)
            {
                Log.LogMsg($"Level pack for playlist '{playlist.PlaylistID}' was not found and will be created");
                levelPack = new BeatmapLevelPackObject(songsAssetFile)
                {
                    Enabled = 1,
                    GameObject = null,
                    IsPackAlwaysOwned = true,
                    PackID = playlist.PlaylistID,
                    Name = playlist.PlaylistID + BSConst.NameSuffixes.LevelPack,
                    PackName = playlist.PlaylistName
                };
                songsAssetFile.AddObject(levelPack, true);
                var col = new BeatmapLevelCollectionObject(songsAssetFile)
                { Name = playlist.PlaylistID + BSConst.NameSuffixes.LevelCollection };
                songsAssetFile.AddObject(col, true);
                levelPack.BeatmapLevelCollection = col.PtrFrom(levelPack);
            }

            playlist.LevelPackObject = levelPack;


            levelPack.PackName = playlist.PlaylistName ?? levelPack.PackName;
            //todo: allow for editing cover art
            if (playlist.CoverImageBytes != null && playlist.CoverImageBytes.Length > 0)
            {
                Log.LogMsg($"Loading cover art for playlist ID '{playlist.PlaylistID}'");

                var oldCoverImage = playlist?.LevelPackObject?.CoverImage;
                var oldTex = playlist?.LevelPackObject?.CoverImage?.Object?.RenderData?.Texture;

                //todo: verify this is a good place to delete stuff                
                playlist.CoverArtSprite = loader.LoadPackCover(playlist.PlaylistID, playlist.CoverImageBytes);
                playlist.LevelPackObject.CoverImage = playlist.CoverArtSprite.PtrFrom(playlist.LevelPackObject);
                if (oldCoverImage != null)
                {
                    if (oldCoverImage.Object != null)
                        songsAssetFile.DeleteObject(oldCoverImage.Object);

                    oldCoverImage.Dispose();
                }
                if (oldTex != null)
                {
                    if (oldTex?.Object != null)
                        songsAssetFile.DeleteObject(oldTex.Object);

                    oldTex.Dispose();
                }
            }
            else
            {
                if (playlist.LevelPackObject.CoverImage != null)
                {
                    playlist.CoverArtSprite = playlist.LevelPackObject.CoverImage.Object;
                }
                else
                {
                    playlist.CoverArtSprite = loader.LoadPackCover(playlist.PlaylistID, null);
                }
                playlist.LevelPackObject.CoverImage = playlist.CoverArtSprite.PtrFrom(playlist.LevelPackObject);
            }

            //clear out any levels, we'll add them back
            var levelCollection = levelPack.BeatmapLevelCollection.Object;
            //levelCollection.BeatmapLevels.ForEach(x => x.Dispose());
            //levelCollection.BeatmapLevels.Clear();
            int songCount = 0;
            Log.LogMsg($"Processing songs for playlist ID {playlist.PlaylistID}...");
            var totalSongs = playlist.SongList.Count();
            var songMod = Math.Ceiling((double)totalSongs / (double)10);
            if (songMod < 1)
                songMod = 1;
            foreach (var song in playlist.SongList.ToList())
            {
                songCount++;
                if (songCount % songMod == 0)
                    Console.WriteLine($"{songCount.ToString().PadLeft(5)} of {totalSongs}...");

                if (UpdateSongConfig(songsAssetFile, song, loader))
                {
                    if (levelCollection.BeatmapLevels.Any(x => x.Object.LevelID == song.LevelData.LevelID))
                    {
                        Log.LogErr($"Playlist ID '{playlist.PlaylistID}' already contains song ID '{song.SongID}'");
                    }
                    else
                    {
                        levelCollection.BeatmapLevels.Add(song.LevelData.PtrFrom(levelCollection));
                        continue;
                    }
                }
                else
                {
                    Log.LogErr($"Song {song.SongID} failed to load!");
                }
            }
            foreach (var bml in playlist.LevelPackObject.BeatmapLevelCollection.Object.BeatmapLevels.ToList())
            {
                if (!playlist.SongList.Any(x => x.SongID == bml.Object.LevelID))
                {
                    Log.LogMsg($"Song ID {bml.Object.LevelID} is not in the new configuration but is in the existing assets.  Removing the link.");
                    playlist.LevelPackObject.BeatmapLevelCollection.Object.BeatmapLevels.Remove(bml);
                    bml.Dispose();
                }
            }
            Console.WriteLine($"Proccessed {totalSongs} for playlist ID {playlist.PlaylistID}");
        }

        private bool UpdateSongConfig(AssetsFile songsAssetFile, BeatSaberSong song, CustomLevelLoader loader)
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();
            try
            {
                BeatmapLevelDataObject level = null;
                if (!string.IsNullOrWhiteSpace(song.SongID))
                {
                    var levels = _manager.MassFindAssets<BeatmapLevelDataObject>(x => x.Object.LevelID == song.SongID, false).Select(x => x.Object).ToList();
                    if (levels.Count() > 0)
                    {
                        if (levels.Count() > 1)
                            Log.LogErr($"Song ID {song.SongID} already has more than one entry in the assets, this may cause problems!");
                        else
                            Log.LogMsg($"Song ID {song.SongID} exists already and won't be loaded");
                        level = levels.First();
                        song.LevelData = level;
                        song.LevelAuthorName = level.LevelAuthorName;
                        song.SongAuthorName = level.SongAuthorName;
                        song.SongName = level.SongName;
                        song.SongSubName = level.SongSubName;
                        return true;
                    }
                    else
                    {
                        Log.LogMsg($"Song ID '{song.SongID}' does not exist and will be created");
                    }
                }
                if (level != null && !string.IsNullOrWhiteSpace(song.CustomSongPath))
                {
                    Log.LogErr("SongID and CustomSongsFolder are both set and the level already exists.  The existing one will be used and CustomSongsFolder won'tbe imported again.");
                    return false;
                }

                //load new song
                if (!string.IsNullOrWhiteSpace(song.CustomSongPath))
                {
                    try
                    {
                        var deser = loader.DeserializeFromJson(song.CustomSongPath, song.SongID);
                        var found = songsAssetFile.FindAssets<BeatmapLevelDataObject>(x => x.Object.LevelID == deser.LevelID).Select(x => x.Object).FirstOrDefault();
                        if (found != null)
                        {
                            Log.LogErr($"No song id was specified, but the level {found.LevelID} is already in the assets, skipping it.");
                            song.LevelData = found;
                            song.LevelAuthorName = found.LevelAuthorName;
                            song.SongAuthorName = found.SongAuthorName;
                            song.SongName = found.SongName;
                            song.SongSubName = found.SongSubName;
                            return true;
                        }
                        level = loader.LoadSongToAsset(deser, song.CustomSongPath, true);
                    }
                    catch (Exception ex)
                    {
                        Log.LogErr($"Exception loading custom song folder '{song.CustomSongPath}', skipping it", ex);
                        return false;
                    }

                    if (level == null)
                    {
                        Log.LogErr($"Song at folder '{song.CustomSongPath}' failed to load, skipping it");
                        return false;
                    }

                    song.LevelData = level;
                    song.LevelAuthorName = level.LevelAuthorName;
                    song.SongAuthorName = level.SongAuthorName;
                    song.SongName = level.SongName;
                    song.SongSubName = level.SongSubName;
                    return true;
                }
                //level == null && string.IsNullOrWhiteSpace(song.CustomSongFolder)

                Log.LogErr($"Song ID '{song.SongID}' either was not specified or could not be found and no CustomSongFolder was specified, skipping it.");
                return false;
            }
            finally
            {
                sw.Stop();
                Log.LogMsg($"Updating song config for {song?.SongID} (path {song?.CustomSongPath}) took {sw.ElapsedMilliseconds}ms");
            }
        }

        private void RemoveLevelAssets(BeatmapLevelDataObject level, List<string> audioFilesToDelete)
        {
            Log.LogMsg($"Removing assets for song id '{level.LevelID}'");
            var songsAssetFile = GetSongsAssetsFile();
            songsAssetFile.DeleteObject(level);
            var cover = level.CoverImageTexture2D.Object;
            if (cover == null)
            {
                Log.LogErr($"Could not find cover for song id '{level.LevelID}' to remove it");
            }
            else
            {
                songsAssetFile.DeleteObject(cover);
            }
            foreach (var diff in level.DifficultyBeatmapSets)
            {
                foreach (var diffbm in diff.DifficultyBeatmaps)
                {
                    songsAssetFile.DeleteObject(diffbm.BeatmapDataPtr.Object);
                }
            }
            var audioClip = level.AudioClip.Object;
            if (audioClip == null)
            {
                Log.LogErr($"Could not find audio clip asset for song id '{level.LevelID}' to remove it");
            }
            else
            {
                audioFilesToDelete.Add(audioClip.Resource.Source);
                songsAssetFile.DeleteObject(audioClip);
            }

        }

        private void RemoveLevelPackAssets(BeatmapLevelPackObject levelPack)
        {
            var songsAssetFile = GetSongsAssetsFile();

            Log.LogMsg($"Removing assets for playlist ID '{ levelPack.PackID}'");
            var collection = levelPack.BeatmapLevelCollection.Object;
            var sprite = levelPack.CoverImage.Object;
            var texture = sprite.RenderData.Texture.Object;
            songsAssetFile.DeleteObject(levelPack);
            songsAssetFile.DeleteObject(collection);
            songsAssetFile.DeleteObject(texture);
            songsAssetFile.DeleteObject(sprite);
        }

        #region Custom Saber

        //TODO: this whole section is a lot of copy/paste that needs to be cleaned up after I make sure it works at all

        //private void LoadSaberMesh(AssetsManager manager, SaberInfo saberInfo)
        //{
        //    if (string.IsNullOrEmpty(saberInfo?.ID))
        //        throw new ArgumentNullException("saberInfo.ID must not be null or empty!");

        //    var file11 = manager.GetAssetsFile(BSConst.KnownFiles.File11);
        //    file11.HasChanges = true;
        //    //lots of double checking things in this function, first time I've done object manipulation this detailed

        //    var newSaber = file11.FindAsset<GameObject>(x => x.Object.Name == $"{saberInfo.ID}Saber")?.Object;
        //    if (newSaber != null)
        //        throw new Exception($"Saber with ID {saberInfo.ID} already exists!");

        //    //find the "basic saber" game object, we're going to copy it
        //    var basicSaber = file11.FindAsset<GameObject>(x => x.Object.Name == "BasicSaber").Object;

        //    //do some detailed checking to make sure the objects are in the places we expect and get the object we're going to clone
        //    var transform = basicSaber.Components.FirstOrDefault(x => x.Object is Transform)?.Object as Transform;
        //    if (transform == null)
        //        throw new Exception("Unable to find Transform on Saber!");

        //    var saberBladeGOTransform = transform.Children.FirstOrDefault(x => x.Object.GameObject?.Object.Name == "SaberBlade")?.Object;
        //    var saberGlowingEdgesGOTransform = transform.Children.FirstOrDefault(x => x.Object.GameObject?.Object.Name == "SaberGlowingEdges")?.Object;
        //    var saberHandleGOTransform = transform.Children.FirstOrDefault(x => x.Object.GameObject?.Object.Name == "SaberHandle")?.Object;
        //    if (saberBladeGOTransform == null)
        //        throw new Exception("Unable to find parent transform of SaberBlade on Transform!");
        //    if (saberGlowingEdgesGOTransform == null)
        //        throw new Exception("Unable to find parent transform of SaberGlowingEdges on Transform!");
        //    if (saberHandleGOTransform == null)
        //        throw new Exception("Unable to find parent transform of SaberHandle on Transform!");

        //    var saberBladeGO = saberBladeGOTransform.GameObject.Object;
        //    var saberGlowingEdgesGO = saberGlowingEdgesGOTransform.GameObject.Object;
        //    var saberHandleGO = saberHandleGOTransform.GameObject.Object;
        //    if (saberBladeGO == null)
        //        throw new Exception("Unable to find SaberBlade on Transform!");
        //    if (saberGlowingEdgesGO == null)
        //        throw new Exception("Unable to find SaberGlowingEdges on Transform!");
        //    if (saberHandleGO == null)
        //        throw new Exception("Unable to find SaberHandle on Transform!");
        //    var saberBladeMeshFilter = saberBladeGO.Components.FirstOrDefault(x => x.Object is MeshFilterObject)?.Object as MeshFilterObject;
        //    var saberGlowingEdgesMeshFilter = saberGlowingEdgesGO.Components.FirstOrDefault(x => x.Object is MeshFilterObject)?.Object as MeshFilterObject;
        //    var saberHandleMeshFilter = saberHandleGO.Components.FirstOrDefault(x => x.Object is MeshFilterObject)?.Object as MeshFilterObject;
        //    if (saberBladeMeshFilter == null)
        //        throw new Exception("Unable to find SaberBlade MeshFilter on Transform!");
        //    if (saberGlowingEdgesMeshFilter == null)
        //        throw new Exception("Unable to find SaberGlowingEdges MeshFilter on Transform!");
        //    if (saberHandleMeshFilter == null)
        //        throw new Exception("Unable to find SaberHandle MeshFilter on Transform!");
        //    if (saberBladeMeshFilter?.Mesh?.Object?.Name != "SaberBlade")
        //        throw new Exception($"Should be named SaberBlade but is named {saberBladeMeshFilter?.Mesh?.Object?.Name}!");
        //    if (saberGlowingEdgesMeshFilter?.Mesh?.Object?.Name != "SaberGlowingEdges")
        //        throw new Exception($"Should be named SaberGlowingEdges but is named {saberGlowingEdgesMeshFilter?.Mesh?.Object?.Name}!");
        //    if (saberHandleMeshFilter?.Mesh?.Object?.Name != "SaberHandle")
        //        throw new Exception($"Should be named SaberHandle but is named {saberHandleMeshFilter?.Mesh?.Object?.Name}!");
        //    saberBladeMeshFilter.Mesh.Object.MeshData = saberInfo.DatFiles.SaberBladeBytes;
        //    saberGlowingEdgesMeshFilter.Mesh.Object.MeshData = saberInfo.DatFiles.SaberGlowingEdgesBytes;
        //    saberHandleMeshFilter.Mesh.Object.MeshData = saberInfo.DatFiles.SaberHandleBytes;


        //}


        //this doesn't work yet.
        private Transform MakeSaber(SaberInfo saberInfo)
        {
            /*
            if (string.IsNullOrEmpty(saberInfo?.ID))
                throw new ArgumentNullException("saberInfo.ID must not be null or empty!");

            var file11 = manager.GetAssetsFile(BSConst.KnownFiles.File11);

            //lots of double checking things in this function, first time I've done object manipulation this detailed

            var newSaber = file11.FindAsset<GameObject>(x => x.Object.Name == $"{saberInfo.ID}Saber")?.Object;
            if (newSaber != null)
                throw new Exception($"Saber with ID {saberInfo.ID} already exists!");

            //find the "basic saber" game object, we're going to copy it
            var basicSaber = file11.FindAsset<GameObject>(x => x.Object.Name == "BasicSaber").Object;

            //do some detailed checking to make sure the objects are in the places we expect and get the object we're going to clone
            var transform = basicSaber.Components.FirstOrDefault(x => x.Object is Transform)?.Object as Transform;
            if (transform == null)
                throw new Exception("Unable to find Transform on Saber!");

            var saberBladeGOTransform = transform.Children.FirstOrDefault(x => x.Object.GameObject?.Object.Name == "SaberBlade")?.Object;
            var saberGlowingEdgesGOTransform = transform.Children.FirstOrDefault(x => x.Object.GameObject?.Object.Name == "SaberGlowingEdges")?.Object;
            var saberHandleGOTransform = transform.Children.FirstOrDefault(x => x.Object.GameObject?.Object.Name == "SaberHandle")?.Object;
            if (saberBladeGOTransform == null)
                throw new Exception("Unable to find parent transform of SaberBlade on Transform!");
            if (saberGlowingEdgesGOTransform == null)
                throw new Exception("Unable to find parent transform of SaberGlowingEdges on Transform!");
            if (saberHandleGOTransform == null)
                throw new Exception("Unable to find parent transform of SaberHandle on Transform!");

            var saberBladeGO = saberBladeGOTransform.GameObject.Object;
            var saberGlowingEdgesGO = saberGlowingEdgesGOTransform.GameObject.Object;
            var saberHandleGO = saberHandleGOTransform.GameObject.Object;
            if (saberBladeGO == null)
                throw new Exception("Unable to find SaberBlade on Transform!");
            if (saberGlowingEdgesGO == null)
                throw new Exception("Unable to find SaberGlowingEdges on Transform!");
            if (saberHandleGO == null)
                throw new Exception("Unable to find SaberHandle on Transform!");
            var saberBladeMeshFilter = saberBladeGO.Components.FirstOrDefault(x => x.Object is MeshFilterObject)?.Object as MeshFilterObject;
            var saberGlowingEdgesMeshFilter = saberGlowingEdgesGO.Components.FirstOrDefault(x => x.Object is MeshFilterObject)?.Object as MeshFilterObject;
            var saberHandleMeshFilter = saberHandleGO.Components.FirstOrDefault(x => x.Object is MeshFilterObject)?.Object as MeshFilterObject;
            if (saberBladeMeshFilter == null)
                throw new Exception("Unable to find SaberBlade MeshFilter on Transform!");
            if (saberGlowingEdgesMeshFilter == null)
                throw new Exception("Unable to find SaberGlowingEdges MeshFilter on Transform!");
            if (saberHandleMeshFilter == null)
                throw new Exception("Unable to find SaberHandle MeshFilter on Transform!");
            if (saberBladeMeshFilter?.Mesh?.Object?.Name != "SaberBlade")
                throw new Exception($"Should be named SaberBlade but is named {saberBladeMeshFilter?.Mesh?.Object?.Name}!");
            if (saberGlowingEdgesMeshFilter?.Mesh?.Object?.Name != "SaberGlowingEdges")
                throw new Exception($"Should be named SaberGlowingEdges but is named {saberGlowingEdgesMeshFilter?.Mesh?.Object?.Name}!");
            if (saberHandleMeshFilter?.Mesh?.Object?.Name != "SaberHandle")
                throw new Exception($"Should be named SaberHandle but is named {saberHandleMeshFilter?.Mesh?.Object?.Name}!");

            //there's a bunch of other pointers we can leave in place, but we have to make 
            //copies of all objects in the tree that we're changing:
            //  BasicSaber -> Transform -> another Transform for each of the 3, SaberBlade/edges/handle (GO) (which contains the previous transform) -> MeshFilter -> SaberBlade/edges/handle (Mesh)

            //clone the SaberBlade/edges/handle meshes, give them new names, and set the new dat file data for the meshes
            var newSaberBladeMesh = saberBladeMeshFilter.Mesh.Object.ObjectInfo.Clone().Object as MeshObject;
            newSaberBladeMesh.Name = $"{saberInfo.ID}SaberBlade";
            newSaberBladeMesh.MeshData = saberInfo.DatFiles.SaberBladeBytes;
            file11.AddObject(newSaberBladeMesh);

            var newSaberGlowingEdgesMesh = saberGlowingEdgesMeshFilter.Mesh.Object.ObjectInfo.Clone().Object as MeshObject;
            newSaberGlowingEdgesMesh.Name = $"{saberInfo.ID}SaberGlowingEdges";
            newSaberGlowingEdgesMesh.MeshData = saberInfo.DatFiles.SaberGlowingEdgesBytes;
            file11.AddObject(newSaberGlowingEdgesMesh);

            var newSaberHandleMesh = saberGlowingEdgesMeshFilter.Mesh.Object.ObjectInfo.Clone().Object as MeshObject;
            newSaberHandleMesh.Name = $"{saberInfo.ID}SaberHandle";
            newSaberHandleMesh.MeshData = saberInfo.DatFiles.SaberHandleBytes;
            file11.AddObject(newSaberHandleMesh);

            //clone the MeshFilters, set their Mesh pointers to the new parts above.
            var newSaberBladeMeshFilter = saberBladeMeshFilter.ObjectInfo.Clone().Object as MeshFilterObject;
            newSaberBladeMeshFilter.Mesh = newSaberBladeMesh.PtrFrom(newSaberBladeMeshFilter);
            file11.AddObject(newSaberBladeMeshFilter);

            var newSaberGlowingEdgesMeshFilter = saberBladeMeshFilter.ObjectInfo.Clone().Object as MeshFilterObject;
            newSaberGlowingEdgesMeshFilter.Mesh = newSaberGlowingEdgesMesh.PtrFrom(newSaberGlowingEdgesMeshFilter);
            file11.AddObject(newSaberGlowingEdgesMeshFilter);

            var newSaberHandleMeshFilter = saberHandleMeshFilter.ObjectInfo.Clone().Object as MeshFilterObject;
            newSaberHandleMeshFilter.Mesh = newSaberHandleMesh.PtrFrom(newSaberHandleMeshFilter);
            file11.AddObject(newSaberHandleMeshFilter);


            //clone those weird transforms in the middle... this goes into the components of the GO, and into the parent transform
            // and gets its parent pointer set to the parent transform
            var newSaberBladeGOTransform = saberBladeGOTransform.ObjectInfo.Clone().Object as Transform;
            file11.AddObject(newSaberBladeGOTransform);
            var newSaberGlowingEdgesGOTransform = saberGlowingEdgesGOTransform.ObjectInfo.Clone().Object as Transform;
            file11.AddObject(newSaberGlowingEdgesGOTransform);
            var newSaberHandleGOTransform = saberHandleGOTransform.ObjectInfo.Clone().Object as Transform;
            file11.AddObject(newSaberHandleGOTransform);


            //clone the saberblade/edges/handle game objects and name them
            //remove the old mesh filter, and add the new one
            //not sure how careful to be, so we'll get it at the same index in each
            var newSaberBladeGO = saberBladeGO.ObjectInfo.Clone().Object as GameObject;
            newSaberBladeGO.Name = $"{saberInfo.ID}SaberBlade";
            var bladeIndexGO = newSaberBladeGO.Components.IndexOf(newSaberBladeGO.Components.First(x => x.Object is MeshFilterObject));
            var bladeIndexTrans = newSaberBladeGO.Components.IndexOf(newSaberBladeGO.Components.First(x => x.Object is Transform));
            newSaberBladeGO.Components[bladeIndexGO] = newSaberBladeMeshFilter.PtrFrom(newSaberBladeGO);
            newSaberBladeGO.Components[bladeIndexTrans] = newSaberBladeGOTransform.PtrFrom(newSaberBladeGO);
            file11.AddObject(newSaberBladeGO);

            var newSaberGlowingEdgesGO = saberGlowingEdgesGO.ObjectInfo.Clone().Object as GameObject;
            newSaberGlowingEdgesGO.Name = $"{saberInfo.ID}SaberGlowingEdges";
            var geIndexGO = newSaberGlowingEdgesGO.Components.IndexOf(newSaberGlowingEdgesGO.Components.First(x => x.Object is MeshFilterObject));
            var geIndexTrans = newSaberGlowingEdgesGO.Components.IndexOf(newSaberGlowingEdgesGO.Components.First(x => x.Object is Transform));
            newSaberGlowingEdgesGO.Components[geIndexGO] = newSaberGlowingEdgesMeshFilter.PtrFrom(newSaberGlowingEdgesGO);
            newSaberGlowingEdgesGO.Components[geIndexTrans] = newSaberGlowingEdgesGOTransform.PtrFrom(newSaberGlowingEdgesGO);
            file11.AddObject(newSaberGlowingEdgesGO);

            var newSaberHandleGO = saberHandleGO.ObjectInfo.Clone().Object as GameObject;
            newSaberHandleGO.Name = $"{saberInfo.ID}SaberHandle";
            var handleIndexGO = newSaberHandleGO.Components.IndexOf(newSaberHandleGO.Components.First(x => x.Object is MeshFilterObject));
            var handleIndexTrans = newSaberHandleGO.Components.IndexOf(newSaberHandleGO.Components.First(x => x.Object is Transform));
            newSaberHandleGO.Components[handleIndexGO] = newSaberHandleMeshFilter.PtrFrom(newSaberHandleGO);
            newSaberHandleGO.Components[handleIndexTrans] = newSaberHandleGOTransform.PtrFrom(newSaberHandleGO);
            file11.AddObject(newSaberHandleGO);

            //clone the Transform
            var newTransform = transform.ObjectInfo.Clone().Object as Transform;

            //get the new game objects in the right spots in the transform.  not sure how much index matters, but we'll be careful
            int bladeIndex = newTransform.Children.IndexOf(newTransform.Children.First(x => x.Object.GameObject.Object.Name == "SaberBlade"));
            int geIndex = newTransform.Children.IndexOf(newTransform.Children.First(x => x.Object.GameObject.Object.Name == "SaberGlowingEdges"));
            int handleIndex = newTransform.Children.IndexOf(newTransform.Children.First(x => x.Object.GameObject.Object.Name == "SaberHandle"));

            newTransform.Children[bladeIndex] = newSaberBladeGOTransform.PtrFrom(newTransform);
            newSaberBladeGOTransform.Father = newTransform.PtrFrom(newSaberBladeGOTransform);
            newTransform.Children[geIndex] = newSaberGlowingEdgesGOTransform.PtrFrom(newTransform);
            newSaberGlowingEdgesGOTransform.Father = newTransform.PtrFrom(newSaberGlowingEdgesGOTransform);
            newTransform.Children[handleIndex] = newSaberHandleGOTransform.PtrFrom(newTransform);
            newSaberHandleGOTransform.Father = newTransform.PtrFrom(newSaberHandleGOTransform);

            file11.AddObject(newTransform);

            ////////////////////////////////////TODO////////////////////////////
            ///I have to copy all of the monobehaviours too because they have game object links
            //clone the BasicSaber and give it a new name
            newSaber = basicSaber.ObjectInfo.Clone().Object as GameObject;
            newSaber.Name = $"{saberInfo.ID}Saber";

            //assign the transform
            newSaber.Components[0] = newTransform.PtrFrom(newSaber);

            file11.AddObject(newSaber);

            //holy shit, is there any chance all of this verbosity and double checking things will work?
            return newTransform;*/
            return null;

        }

        //private bool SaberExists(AssetsManager manager, string saberID)
        //{
        //    var file11 = manager.GetAssetsFile(BSConst.KnownFiles.File11);
        //    return file11.FindAsset<GameObject>(x => x.Object.Name == $"{saberID}Saber") != null;
        //}

        //private string GetCurrentSaberID(AssetsManager manager)
        //{
        //    var saberChild = GetSaberObjectParentTransform(manager)?.GameObject?.Object;
        //    if (saberChild == null)
        //        throw new Exception("Couldn't find child saber game object of transform.");
        //    return saberChild.Name.Substring(0, saberChild.Name.Length - 5);
        //}

        //private Transform GetSaberObjectParentTransform(AssetsManager manager)
        //{
        //    var file11 = manager.GetAssetsFile(BSConst.KnownFiles.File11);
        //    var basicSaberModel = file11.FindAsset<GameObject>(x => x.Object.Name == "BasicSaberModel");

        //    if (basicSaberModel == null)
        //        throw new Exception("Couldn't find BasicSaberModel!");

        //    var transform = basicSaberModel.Object.Components.FirstOrDefault(x => x.Object is Transform)?.Object as Transform;
        //    if (transform == null)
        //        throw new Exception("Couldn't find Transform on BasicSaberModel!");

        //    var saberParent = (transform.Children.FirstOrDefault(x => x.Object is Transform
        //            && ((x.Object as Transform).GameObject?.Object?.Name?.EndsWith("Saber") ?? false)).Object as Transform);
        //    if (saberParent == null)
        //        throw new Exception("Could not find child transform of BasicSaberModel!");
        //    return saberParent;
        //}

        //private void SwapToSaberID(AssetsManager manager, string saberID)
        //{
        //    var file11 = manager.GetAssetsFile(BSConst.KnownFiles.File11);

        //    var newSaber = file11.FindAsset<GameObject>(x => x.Object.Name == $"{saberID}Saber")?.Object;
        //    if (newSaber == null)
        //        throw new Exception($"Saber with ID {saberID} does not exist!");

        //    var newSaberTransform = newSaber.Components.FirstOrDefault(x => x.Object is Transform).Object as Transform;
        //    if (newSaberTransform == null)
        //        throw new Exception($"Saber with ID {saberID} is missing its parent transform!");

        //    var basicSaberModel = file11.FindAsset<GameObject>(x => x.Object.Name == "BasicSaberModel");

        //    if (basicSaberModel == null)
        //        throw new Exception("Couldn't find BasicSaberModel!");

        //    var transform = basicSaberModel.Object.Components.FirstOrDefault(x => x.Object is Transform)?.Object as Transform;
        //    if (transform == null)
        //        throw new Exception("Couldn't find Transform on BasicSaberModel!");

        //    var saberChild = transform.Children.FirstOrDefault(x => x.Object.GameObject?.Object?.Name?.EndsWith("Saber")??false);
        //    if (saberChild == null)
        //        throw new Exception("Couldn't find a game object on the BasicSaberModel Transform that ended with -Saber!");
        //    int saberIndex = transform.Children.IndexOf(saberChild);
        //    saberChild.Object.Father = null;
        //    transform.Children[saberIndex] = newSaberTransform.PtrFrom(transform) as ISmartPtr<Transform>;
        //    newSaberTransform.Father = transform.PtrFrom(newSaberTransform);
        //}
        #endregion

        private List<AssetOp> DiffMusicConfig(BeatSaberQuestomConfig newConfig)
        {
            List<AssetOp> ops = new List<AssetOp>();
            Func<string, bool> IgnoreSongID = (songId) => {
                if (HideOriginalPlaylists)
                {
                    return BSConst.KnownLevelIDs.Contains(songId);
                } else
                {
                    return false;
                }
            };

            Func<string, bool> IgnorePackID = (packId) => {
                if (HideOriginalPlaylists)
                {
                    return BSConst.KnownLevelPackIDs.Contains(packId);
                }
                else
                {
                    return false;
                }
            };
            //NOTE: this code is pretty inefficient in that it loops over collections many multiple times in all of the LINQ statements and ToList()s
            //      If it becomes a performance bottleneck, it can probably all be compressed into 1 or 2 loops rather than a bunch of individual linq queries
            //      but for now it's left inefficient because it's easier to follow what's happening
            using (new LogTiming(nameof(DiffMusicConfig)))
            {                
                var lt = new LogTiming("find new playlists");

                var newOrUpdatedPlaylists = newConfig.Playlists.Where(x => !MusicCache.PlaylistCache.ContainsKey(x.PlaylistID)      //playlist id doesn't exist
                            || (x.PlaylistName != null && MusicCache.PlaylistCache[x.PlaylistID].Playlist.Name != x.PlaylistName)   //name is different
                            || (x.CoverImageBytes != null && x.CoverImageBytes.Length > 0)).ToList();                               //cover image bytes is provided
                                                                                                                                 //create an op for each added or changed playlist
                newOrUpdatedPlaylists.ForEach(x => ops.Add(new AddOrUpdatePlaylistOp(x)));
                lt.Dispose();
                Log.LogMsg("Adding or updating playlist IDs: " + string.Join(", ", newOrUpdatedPlaylists.Select(x => x.PlaylistID).ToArray()));

                lt = new LogTiming("build all config songs");
                //get an anon SongID, PlaylistID pair of what's in the config passed in
                var allConfigSongs = newConfig.Playlists.SelectMany(y => y.SongList.Select(x => new { SongID = x.SongID, PlaylistID = y.PlaylistID, Song = x })).ToList();
                lt.Dispose();

                lt = new LogTiming("make delete song ops");
                //find any songs that have been removed from everywhere and make a song delete op for each of them.  song delete op will handle removing it from the playlist
                //  check whether it should be ignored (i.e. leave OST stuff alone)
                var removedSongIDs = MusicCache.SongCache.Keys.Where(x => !allConfigSongs.Any(y => y.SongID == x) && !IgnoreSongID(x)).ToList();
                removedSongIDs.ForEach(x => ops.Add(new DeleteSongOp(x)));
                lt.Dispose();
                Log.LogMsg("Removing song IDs: " + string.Join(", ", removedSongIDs.ToArray()));

                lt = new LogTiming("make move song ops");
                //find any song/playlist tuples whose playlist doesn't match what's in the cache for that song (i.e. a song was moved to a different playlist)
                var movedSongs = allConfigSongs.Where(x => MusicCache.SongCache.ContainsKey(x.SongID) && MusicCache.SongCache[x.SongID].Playlist.PackID != x.PlaylistID).ToList();
                movedSongs.ForEach(x => ops.Add(new MoveSongToPlaylistOp(x.SongID, x.PlaylistID, null)));
                lt.Dispose();
                Log.LogMsg("Moving song IDs to playlists: " + string.Join(", ", movedSongs.Select(x => x.SongID + " -> " + x.PlaylistID).ToArray()));

                lt = new LogTiming("make new song ops");
                //find any new songs, add an op to add them
                var newSongs = allConfigSongs.Where(x => !MusicCache.SongCache.ContainsKey(x.SongID)).ToList();
                newSongs.ForEach(x => ops.Add(new AddNewSongToPlaylistOp(x.Song, x.PlaylistID)));
                lt.Dispose();
                Log.LogMsg("Adding new songs to playlists: " + string.Join(", ", newSongs.Select(x => x.SongID + " -> " + x.PlaylistID)));

                //find any playlists that should be removed.  tell the DeletePlaylistOp not to delete all songs in the playlist since we should have already cleaned them all up
                //  check whether to ignore the playlist (i.e. don't delete OST stuff)
                lt = new LogTiming("make remove playlist ops");
                var removedPlaylists = MusicCache.PlaylistCache.Keys.Where(x => !newConfig.Playlists.Any(y => x == y.PlaylistID) && !IgnorePackID(x)).ToList();
                removedPlaylists.ForEach(x => ops.Add(new DeletePlaylistOp(x, false)));
                lt.Dispose();
                Log.LogMsg("Removing playlist IDs: " + string.Join(", ", removedPlaylists.ToArray()));
            }
            return ops;
        }

        private BeatSaberQuestomConfig GetConfig()
        {
            Stopwatch sw = new Stopwatch();
            try
            {
                sw.Start();
                lock (this)
                {
                    BeatSaberQuestomConfig config = new BeatSaberQuestomConfig();
                    
                    var mainPack = GetMainLevelPack();
                    CustomLevelLoader loader = new CustomLevelLoader(GetSongsAssetsFile(), _config);
                    foreach (var packDef in MusicCache.PlaylistCache.Values.OrderBy(x=> x.Order))
                    {
                        var pack = packDef.Playlist;
                        if (HideOriginalPlaylists && BSConst.KnownLevelPackIDs.Contains(pack.PackID))
                            continue;

                        var packModel = new BeatSaberPlaylist() { PlaylistName = pack.PackName, PlaylistID = pack.PackID, LevelPackObject = pack };
                        var collection = pack.BeatmapLevelCollection.Object;

                        foreach (var songPtr in collection.BeatmapLevels)
                        {
                            var songObj = songPtr.Object;
                            var songModel = new BeatSaberSong()
                            {
                                LevelAuthorName = songObj.LevelAuthorName,
                                SongID = songObj.LevelID,
                                SongAuthorName = songObj.SongAuthorName,
                                SongName = songObj.SongName,
                                SongSubName = songObj.SongSubName,
                                LevelData = songObj
                            };
                            //songModel.CoverArtFilename = loader.GetCoverImageFilename(songObj);
                            packModel.SongList.Add(songModel);
                        }
                        config.Playlists.Add(packModel);
                    }
                    var cm = GetColorManager();
                    var rc = cm.ColorB.Object.Color; 
                    var lc = cm.ColorA.Object.Color;
                    config.RightColor = new BeatSaberColor() { A = (byte)(255*rc.A), R = (byte)(255*rc.R), G = (byte)(rc.G*255), B = (byte)(rc.B*255) };
                    config.LeftColor = new BeatSaberColor() { A = (byte)(255 * lc.A), R = (byte)(255 * lc.R), G = (byte)(lc.G * 255), B = (byte)(lc.B * 255) };
                    ModManager.Mods.ForEach(x => config.Mods.Add(x));
                    return config;
                }
            }
            finally
            {
                sw.Stop();
                Log.LogMsg($"Loading config took {sw.ElapsedMilliseconds}ms");
            }
        }

        private void VerifyCharacteristics()
        {

            using (new LogTiming("Checking that custom characteristics exist"))
            {
                var lightshowName = MiscUtils.GetCharacteristicAssetName(Characteristic.Lightshow);
                var lawlessName = MiscUtils.GetCharacteristicAssetName(Characteristic.Lawless);

                var lightshowAsset = Manager.MassFirstAsset<BeatmapCharacteristicObject>(x => x.Object.Name == lightshowName, false)?.Object;
                var lawlessAsset = Manager.MassFirstAsset<BeatmapCharacteristicObject>(x => x.Object.Name == lawlessName, false)?.Object;

                if (lightshowAsset == null || lawlessAsset == null)
                {
                    var standardName = MiscUtils.GetCharacteristicAssetName(Characteristic.Standard);
                    BeatmapCharacteristicObject standardCharacteristic = Manager.MassFirstAsset<BeatmapCharacteristicObject>(x => x.Object.Name == standardName, false)?.Object;
                    int count = Manager.MassFindAssets<BeatmapCharacteristicObject>(x => true, false).Count();
                    if (standardCharacteristic == null)
                    {
                        Log.LogErr($"Unable to locate the standard beatmap characteristic while verifying characteristics!");
                        return;
                    }
                    if (lightshowAsset == null)
                    {
                        Log.LogMsg("Lightshow characteristic wasn't found, creating it.");
                        CreateCharacteristic(Characteristic.Lightshow, standardCharacteristic, count);
                        count++;
                    }
                    if (lawlessAsset == null)
                    {
                        Log.LogMsg("Lawless characteristic wasn't found, creating it.");
                        CreateCharacteristic(Characteristic.Lawless, standardCharacteristic, count);
                        count++;
                    }
                }
            }
        }

        private void CreateCharacteristic(Characteristic characteristic, BeatmapCharacteristicObject baseToClone, int sort)
        {
            try
            {
                string characteristicName = $"LEVEL_{characteristic.ToString().ToUpper()}";
                string hintText = $"{characteristicName}_HINT";
                string assetName = MiscUtils.GetCharacteristicAssetName(Characteristic.Lightshow);
                var lightshowAsset = (BeatmapCharacteristicObject)baseToClone.ObjectInfo.DeepClone(baseToClone.ObjectInfo.ParentFile);
                lightshowAsset.Name = assetName;
                lightshowAsset.SerializedName = characteristic.ToString();
                lightshowAsset.SortingOrder = sort;
                //todo: text translation stuff
                //lightshowAsset.CharacteristicName = characteristicName;
                //lightshowAsset.HintText = hintText;
                try
                {
                    byte[] lightshowIcon = _config.EmbeddedResourcesFileProvider.Read("Lightshow.png");
                    if (lightshowIcon == null || lightshowIcon.Length < 1)
                        throw new Exception("Lightshow.png read was null or empty!");
                    ImageUtils.Instance.AssignImageToTexture(lightshowIcon, lightshowAsset.Icon.Object.RenderData.Texture.Object, 256, 256, Int32.MaxValue, TextureConversionFormat.RGB24);
                }
                catch (Exception ex)
                {
                    Log.LogErr("Failed to load lightshow's png icon!", ex);
                    throw;
                }
            }
            catch (Exception ex)
            {
                Log.LogErr("Exception trying to create lightshow characteristic!", ex);
            }
        }

        private void PreloadFiles()
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();
            _assetsLoadOrder.ForEach(x => _manager.GetAssetsFile(x));
            sw.Stop();
            Log.LogMsg($"Preloading files took {sw.ElapsedMilliseconds}ms");
        }

        //private bool UpdateSaberConfig(AssetsManager manager, SaberModel saberCfg)
        //{
        //    try
        //    {
        //        if (saberCfg != null && !string.IsNullOrWhiteSpace(saberCfg.CustomSaberFolder))
        //        {

        //            SaberInfo newSaber = SaberInfo.FromFolderOrZip(saberCfg.CustomSaberFolder);
        //            if (SaberExists(manager, newSaber.ID))
        //            {
        //                Log.LogErr($"Saber ID {newSaber.ID} that was loaded already exists.  Cannot load another saber with the same name.");
        //                return false;
        //            }
        //            LoadSaberMesh(manager, newSaber);
        //            return true;

        //        }
        //        else
        //        {
        //            Log.LogMsg("Saber config is null, saber configuration will not be changed.");
        //            return true;
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Log.LogErr("Failed to update saber configuration.", ex);
        //        return false;
        //    }
        //}

        //not currently working
        //private bool UpdateSaberConfig(AssetsManager manager, SaberModel saberCfg)
        //{
        //    try
        //    {
        //        if (saberCfg != null && (!string.IsNullOrWhiteSpace(saberCfg.CustomSaberFolder) || !string.IsNullOrWhiteSpace(saberCfg.SaberID)))
        //        {
        //            var currentSaber = GetCurrentSaberID(manager);
        //            if (!string.IsNullOrWhiteSpace(saberCfg.SaberID) && SaberExists(manager, saberCfg.SaberID))
        //            {
        //                if (currentSaber == saberCfg.SaberID)
        //                {
        //                    Log.LogMsg($"Current saber is already set to {currentSaber}, no changes needed.");
        //                    return true;
        //                }
        //                Log.LogMsg($"SaberID {saberCfg.SaberID} was found already in the assets, using it.");
        //                SwapToSaberID(manager, saberCfg.SaberID);
        //                return true;
        //            }
        //            else
        //            {
        //                SaberInfo newSaber = SaberInfo.FromFolderOrZip(saberCfg.CustomSaberFolder);
        //                if (SaberExists(manager, newSaber.ID))
        //                {
        //                    Log.LogErr($"Saber ID {newSaber.ID} that was loaded already exists.  Cannot load another saber with the same name.");
        //                    return false;
        //                }
        //                MakeSaber(manager, newSaber);
        //                SwapToSaberID(manager, newSaber.ID);
        //                return true;
        //            }
        //        }
        //        else
        //        {
        //            Log.LogMsg("Saber config is null, saber configuration will not be changed.");
        //            return true;
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Log.LogErr("Failed to update saber configuration.", ex);
        //        return false;
        //    }
        //}

        private void UpdateColorConfig(SimpleColorSO[] colors)
        {
            var manager = GetColorManager();

            var colorA = colors[0];
            var colorB = colors[1];

            if (colorA != null)
            {
                (manager.ColorA.Object as SimpleColorSO).Color = colorA.Color;
            }
            if (colorB != null)
                (manager.ColorB.Object as SimpleColorSO).Color = colorB.Color;
            // Reset
            if (colorA == null && colorB == null)
            {
                (manager.ColorA.Object as SimpleColorSO).Color = BSConst.Colors.DefaultColorA;
                (manager.ColorB.Object as SimpleColorSO).Color = BSConst.Colors.DefaultColorB;
            }
        }

        private void UpdateTextConfig(List<(string, string)> texts)
        {
            var textAsset = GetBeatSaberTextAsset();
            if (textAsset == null)
            {
                // Could not find text asset!
                return;
            }
            var textKeyPairs = TextUtils.ReadLocaleText(textAsset.Script);
            TextUtils.ApplyWatermark(textKeyPairs);
            foreach (var kp in texts)
            {
                textKeyPairs[kp.Item1]["ENGLISH"] = kp.Item2;
            }
            textAsset.Script = TextUtils.WriteLocaleText(textKeyPairs);
        }

        private ColorManager GetColorManager()
        {
            var colorManager = _manager.MassFirstOrDefaultAsset<ColorManager>(x => true)?.Object;
            if (colorManager == null)
                throw new Exception("Unable to find the color manager asset!");
            return colorManager;
        }

        private TextAsset GetBeatSaberTextAsset()
        {

            var textAssets = _manager.MassFirstOrDefaultAsset<TextAsset>(x => x.Object.Name == "BeatSaber"); ;
            if (textAssets == null)
                throw new Exception("Unable to find any TextAssets! Perhaps the ClassID/ScriptHash are invalid?");
            // Literally the only object in the TextAssetFile is "BeatSaber" at PathID=1
            return textAssets.Object;
        }
               
        private List<string> GetAssetsLoadOrderFile()
        {
            string filename = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "assetsLoadOrder.json");
            if (!File.Exists(filename))
            {
                Log.LogErr($"Can't find {filename}!  Default assets load order will be used.");
                return null;
            }
            List<string> loadOrder = new List<string>();
            try
            {
                using (var jr = new JsonTextReader(new StreamReader(filename)))
                    loadOrder = new JsonSerializer().Deserialize<List<string>>(jr);
            }
            catch (Exception ex)
            {
                Log.LogErr($"Error loading {filename}!  Default assets load order will be used.", ex);
                return null;
            }
            if (loadOrder == null || loadOrder.Count < 1)
                return null;

            return loadOrder;
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _manager.Dispose();
                    _manager = null;
                    _opManager.Dispose();
                    _opManager = null;
                }
                disposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);

        }
        #endregion
    }
}