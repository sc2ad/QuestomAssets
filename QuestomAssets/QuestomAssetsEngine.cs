using System;
using System.IO;
using QuestomAssets.AssetsChanger;
using QuestomAssets.BeatSaber;
using System.Collections.Generic;
using System.Linq;
using QuestomAssets.Utils;
using Newtonsoft.Json;

namespace QuestomAssets
{

    public class QuestomAssetsEngine
    {
        private bool _readOnly;
        private string _pemData;
        private List<string> _assetsLoadOrder = new List<string>();
        private AssetsManager _manager;

        public IAssetsFileProvider FileProvider { get; private set; }

        public string AssetsRootPath { get; private set; }

        public bool HideOriginalPlaylists { get; private set; } = true;

        /// <summary>
        /// Create a new instance of the class and open the apk file
        /// </summary>
        /// <param name="apkFilename">The path to the Beat Saber APK file</param>
        /// <param name="readOnly">True to open the APK read only</param>
        /// <param name="pemCertificateData">The contents of the PEM certificate that will be used to sign the APK.  If omitted, a new self signed cert will be generated.</param>
        public QuestomAssetsEngine(IAssetsFileProvider fileProvider, string assetsRootPath, bool readOnly = false, string pemCertificateData = BSConst.DebugCertificatePEM)
        {
            FileProvider = fileProvider;
            _readOnly = readOnly;
            _pemData = pemCertificateData;
            _assetsLoadOrder = GetAssetsLoadOrderFile();
            AssetsRootPath = assetsRootPath;
            if (_assetsLoadOrder == null)
            {
                _assetsLoadOrder = new List<string>()
                {
                    "globalgamemanagers",
                    "globalgamemanagers.assets",
                    "sharedassets1.assets",
                    "231368cb9c1d5dd43988f2a85226e7d7",
                    "sharedassets11.assets",
                    "sharedassets18.assets",
                    "sharedassets20.assets"
                };
            }
            _manager = new AssetsManager(FileProvider, assetsRootPath, BSConst.GetAssetTypeMap());
        }

        public BeatSaberQuestomConfig GetCurrentConfig(bool suppressImages = false)
        {
            PreloadFiles();
            var config = GetConfig(suppressImages);

            //clear out any of the internal refs that were used so the GC can clean things up
            foreach (var p in config.Playlists)
            {
                p.CoverArtSprite = null;
                p.LevelPackObject = null;
                foreach (var song in p.SongList)
                {
                    song.LevelData = null;
                    song.SourceOgg = null;
                }
            }
            //config.Saber = new SaberModel()
            //{
            //    SaberID = GetCurrentSaberID(manager)
            //};
            return config;
        }

        public void UpdateConfig(BeatSaberQuestomConfig config)
        {
            //todo: basic validation of the config
            if (_readOnly)
                throw new InvalidOperationException("Cannot update in read only mode.");


            PreloadFiles();

            //get existing playlists and their songs
            //compare with new ones
            //generate a diff
            //etc.

            UpdateColorConfig(config.Colors);

            //TODO: something broke
            //UpdateTextConfig(manager, config.TextChanges);

            //if (!UpdateSaberConfig(manager, config.Saber))
            //{
            //    Log.LogErr("Saber failed to update.  Aborting all changes.");
            //}

            if (config.Playlists != null)
            {
                UpdateMusicConfig(config);
            }
            else
            {
                Log.LogMsg("Playlists is null, song configuration will not be changed.");
            }

            Log.LogMsg("Serializing all assets...");
            _manager.WriteAllOpenAssets();

            FileProvider.Save();

        }

        public bool ApplyPatchSettingsFile()
        {
            string filename = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "patchsettings.json");
            if (!File.Exists(filename))
            {
                Log.LogErr($"Can't apply patch settings from file, {filename} does not exist!");
                return false;
            }
            List<FilePatch> patches = null;
            using (StreamReader sr = new StreamReader(filename))
            using (JsonReader jr = new JsonTextReader(sr))
                patches = new JsonSerializer().Deserialize<List<FilePatch>>(jr);
            Log.LogMsg($"Found {patches.Count} files to patch in {filename}");
            foreach (var patch in patches)
            {
                if (!ApplyPatch(patch))
                {
                    Log.LogErr($"Failed to apply patch... aborting any further patching...");
                    return false;
                }
            }
            return true;
        }

        public bool ApplyPatch(FilePatch patch)
        {
            if (!Patcher.Patch(FileProvider, patch))
            {
                Log.LogErr($"File {patch.Filename} failed to patch!");
                return false;
            }
            return true;
        }
        public void SignAPK()
        {
            ApkSigner signer = new ApkSigner(_pemData);
            signer.Sign(FileProvider);
        }

        private MainLevelPackCollectionObject GetMainLevelPack()
        {
            var mainLevelPack = _manager.MassFirstOrDefaultAsset<MainLevelPackCollectionObject>(x => true)?.Object;
            if (mainLevelPack == null)
                throw new Exception("Unable to find the main level pack collection object!");
            return mainLevelPack;
        }

        private AssetsFile GetSongsAssetsFile()
        {
            var extrasPack = _manager.MassFirstOrDefaultAsset<BeatmapLevelPackObject>(x => x.Object.Name == "ExtrasLevelPack", true);
            if (extrasPack == null)
                throw new Exception("Unable to find the file that ExtrasLevelPack is in!");
            return extrasPack.ParentFile;
        }

        private AlwaysOwnedContentModel GetAlwaysOwnedModel()
        {
            var aoModel = _manager.MassFirstOrDefaultAsset<AlwaysOwnedContentModel>(x => x.Object.Name == "DefaultAlwaysOwnedContentModel", true);
            if (aoModel == null)
                throw new Exception("Unable to find AlwaysOwnedContentModel!");
            return aoModel.Object;
        }

        private NoteCutSoundEffectManager GetNoteCutSoundEffectManager(AssetsManager manager)
        {
            var ao = manager.MassFirstAsset<NoteCutSoundEffectManager>(x => x.Object.GameObject.Object.Name == "NoteCutSoundEffectManager");
            if (ao == null)
                throw new Exception("Unable to find NoteCutSoundEffectManager!");
            return ao.Object;
        }

        private void UpdatePlaylistConfig(AssetsFile songsAssetFile, BeatSaberPlaylist playlist)
        {
            Log.LogMsg($"Processing playlist ID {playlist.PlaylistID}...");
            CustomLevelLoader loader = new CustomLevelLoader(songsAssetFile);
            BeatmapLevelPackObject levelPack = songsAssetFile.FindAsset<BeatmapLevelPackObject>(x => x.Object.PackID == playlist.PlaylistID)?.Object;
            //create a new level pack if one waasn't found
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
            if (playlist.CoverArtBytes != null)
            {
                Log.LogMsg($"Loading cover art for playlist ID '{playlist.PlaylistID}'");

                playlist.CoverArtSprite = loader.LoadPackCover(playlist.PlaylistID, playlist.CoverArtBytes);
                playlist.LevelPackObject.CoverImage = playlist.CoverArtSprite.PtrFrom(playlist.LevelPackObject);
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
            levelCollection.BeatmapLevels.ForEach(x => x.Dispose());
            levelCollection.BeatmapLevels.Clear();
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
                        Log.LogErr($"Playlist ID '{playlist.PlaylistID}' already contains song ID '{song.SongID}' once, removing the second link");
                    }
                    else
                    {
                        levelCollection.BeatmapLevels.Add(song.LevelData.PtrFrom(levelCollection));
                        continue;
                    }
                }

                playlist.SongList.Remove(song);
            }
            Console.WriteLine($"Proccessed {totalSongs} for playlist ID {playlist.PlaylistID}");
        }

        private bool UpdateSongConfig(AssetsFile songsAssetFile, BeatSaberSong song, CustomLevelLoader loader)
        {
            BeatmapLevelDataObject level = null;
            if (!string.IsNullOrWhiteSpace(song.SongID))
            {
                var levels = songsAssetFile.FindAssets<BeatmapLevelDataObject>(x => x.Object.LevelID == song.SongID).Select(x => x.Object).ToList();
                if (levels.Count() > 0)
                {
                    if (levels.Count() > 1)
                        Log.LogErr($"Song ID {song.SongID} already has more than one entry in the assets, this may cause problems!");
                    else
                        Log.LogMsg($"Song ID {song.SongID} exists already and won't be loaded");
                    level = levels.First();
                    song.LevelData = level;
                    return true;
                }
                else
                {
                    Log.LogMsg($"Song ID '{song.SongID}' does not exist and will be created");
                }
            }
            if (level != null && !string.IsNullOrWhiteSpace(song.CustomSongFolder))
            {
                Log.LogErr("SongID and CustomSongsFolder are both set and the level already exists.  The existing one will be used and CustomSongsFolder won'tbe imported again.");
                return false;
            }

            //load new song
            if (!string.IsNullOrWhiteSpace(song.CustomSongFolder))
            {
                try
                {
                    string oggPath;
                    var deser = loader.DeserializeFromJson(song.CustomSongFolder, song.SongID);
                    var found = songsAssetFile.FindAssets<BeatmapLevelDataObject>(x => x.Object.LevelID == deser.LevelID).Select(x => x.Object).FirstOrDefault();
                    if (found != null)
                    {
                        Log.LogErr($"No song id was specified, but the level {found.LevelID} is already in the assets, skipping it.");
                        song.LevelData = found;
                        return true;
                    }
                    level = loader.LoadSongToAsset(deser, song.CustomSongFolder, out oggPath, true);
                    song.SourceOgg = oggPath;
                }
                catch (Exception ex)
                {
                    Log.LogErr($"Exception loading custom song folder '{song.CustomSongFolder}', skipping it", ex);
                    return false;
                }

                if (level == null)
                {
                    Log.LogErr($"Song at folder '{song.CustomSongFolder}' failed to load, skipping it");
                    return false;
                }

                song.LevelData = level;
                return true;
            }
            //level == null && string.IsNullOrWhiteSpace(song.CustomSongFolder)

            Log.LogErr($"Song ID '{song.SongID}' either was not specified or could not be found and no CustomSongFolder was specified, skipping it.");
            return false;

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
            var texture = sprite.Texture.Object;
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
            if (string.IsNullOrEmpty(saberInfo?.ID))
                throw new ArgumentNullException("saberInfo.ID must not be null or empty!");
            if (saberInfo.AssetsFiles == null)
                throw new ArgumentNullException("saberInfo.AssetsFiles must not be null for loading .sabers!");

            var file11 = manager.GetAssetsFile(BSConst.KnownFiles.File11);

            //lots of double checking things in this function, first time I've done object manipulation this detailed

            if (saberManager == null || leftSaber == null || rightSaber == null)
                throw new ArgumentNullException("saberManager, leftsaber, and rightsaber must all not be null!");

            // Find parent transform
            var vrgamecore = (saberManager.Object.Components[0].Object as Transform).Father.Object as Transform;

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

            // Now we need to add the new sabers as children to the vrgamecore
            saberInfo.AssetsFiles.ForEach(x =>
            {
                if (!x.Contains("."))
                {
                    try
                    {
                        // Gets the new sabers
                        var f = manager.GetAssetsFile(x);
                        var newLeft = f.FindAsset<GameObject>(go => go.Object.Name == "LeftSaber");
                        var newRight = f.FindAsset<GameObject>(go => go.Object.Name == "RightSaber");
                        // I don't know what we need to do in order to make sure we add everything properly, but for now just YOLO it?
                        // We almost definitely need to clone all of the objects from the saber, remove all of the custom scripts, etc.
                        // Start by adding the sabers
                        manager.GetAssetsFile("level11").AddObject(newLeft.DeepClone(exclusions: new List<CloneExclusion>()
                        {
                            new CloneExclusion(ExclusionMode.LeaveRef)
                            {
                                Filter = (ptr, y) =>
                                {
                                    if (ptr != null && ptr.Target.Object is MonoBehaviourObject)
                                    {
                                        return false;
                                    }
                                    return true;
                                }
                            }
                        }));
                        //f.AddObject(newRight.Clone());
                        var leftChildPointer = (newLeft.Object.Components[0].Object as Transform).PtrFrom(vrgamecore);
                        vrgamecore.Children.Add(leftChildPointer);
                        var rightChildPointer = (newRight.Object.Components[0].Object as Transform).PtrFrom(vrgamecore);
                        vrgamecore.Children.Add(rightChildPointer);

                        // Remove custom scripts, and possible null scripts
                        // Actually don't remove null scripts for now, but we need to figure out what they should be so that we can
                        // reassign the PathIDs to the proper monoscripts, and also properly assign typeIDs

                    }
                    catch (Exception ex)
                    {
                        Log.LogErr($"Failed to load file '{x}' from bundle", ex);
                    }
                }
            });
        }

        //TODO: this whole section is a lot of copy/paste that needs to be cleaned up after I make sure it works at all

        private GameObject GetBasicSaber(AssetsManager manager)
        {
            var saber = manager.MassFirstAsset<GameObject>(x => x.Object.Name == "BasicSaber", true);
            if (saber == null)
                throw new Exception("Unable to find BasicSaber!");
            return saber.Object;
        }

            //clone the MeshFilters, set their Mesh pointers to the new parts above.
            var newSaberBladeMeshFilter = saberBladeMeshFilter.ObjectInfo.Clone().Object as MeshFilterObject;
            newSaberBladeMeshFilter.Mesh = newSaberBladeMesh.PtrFrom(newSaberBladeMeshFilter);
            file11.AddObject(newSaberBladeMeshFilter);

        private bool UpdateSaberConfig(AssetsManager manager, SaberModel saberCfg)
        {
            try
            {
                var basicSaber = GetBasicSaber(manager);
                var saberFile = basicSaber.ObjectInfo.ParentFile;
                if (saberCfg != null && !string.IsNullOrWhiteSpace(saberCfg.CustomSaberFolder))
                {
                    SaberInfo newSaber = SaberInfo.FromFolderOrZip(saberCfg.CustomSaberFolder);
                    if (SaberExists(basicSaber.ObjectInfo.ParentFile, newSaber.ID))
                    {
                        Log.LogErr($"Saber ID {newSaber.ID} that was loaded already exists.  Cannot load another saber with the same name.");
                        return false;
                    }
                    var saberFatherTarget = (basicSaber?.Components?.FirstOrDefault()?.Object as Transform)?.Father?.Target?.Object;
                    if (saberFatherTarget == null)
                        throw new Exception("Unable to find the saber's transform father!");

                    //clone the basicsaber, but make sure to exclude the father target
                    var clonedSaber = basicSaber.ObjectInfo.DeepClone(exclusions: new List<CloneExclusion>() {
                        new CloneExclusion(ExclusionMode.Remove)
                        {
                             PointerTarget = saberFatherTarget
                        },
                        new CloneExclusion(ExclusionMode.LeaveRef)
                        {
                            Filter = (x,y) =>
                            {
                                if (x != null && typeof(MonoBehaviourObject).IsAssignableFrom(x.Target.Object.GetType()))
                                    return true;

                                if (x != null && x.Target.Object is MonoScriptObject)
                                    return true;

                                if (x != null && x.Target.Type.ClassID == AssetsConstants.ClassID.MaterialClassID)
                                    return true;

                                return false;
                            }
                        }
                    }) as GameObject;
                    if (clonedSaber == null)
                        throw new Exception("Failed to clone BasicSaber!");
                    clonedSaber.Name = $"{newSaber.ID}Saber";
                    var transform = clonedSaber.Components.FirstOrDefault(x => x.Object is Transform)?.Object as Transform;
                    if (transform == null)
                        throw new Exception("Unable to find the transform on the cloned BasicSaber!");

                    //i'm not null checking all this, but if it crashes, i'll be general about why instead of leaving it as a null ref exception
                    try
                    {
                        Func<Transform, string, MeshObject> findPart = (trans, name) => trans.Children.Where(x => x.Object?.GameObject?.Object?.Name == name)
                                .SelectMany(x => x.Object.GameObject.Object.Components)
                                .Where(z => z.Object is MeshFilterObject && (z.Object as MeshFilterObject)?.Mesh?.Object?.Name == name)
                                .Select(y => (y.Object as MeshFilterObject).Mesh.Object).FirstOrDefault();
                        var saberBladeMesh = findPart(transform, "SaberBlade");
                        var saberEdgesMesh = findPart(transform, "SaberGlowingEdges");
                        var saberHandleMesh = findPart(transform, "SaberHandle");
                        saberBladeMesh.MeshData = newSaber.DatFiles.SaberBladeBytes;
                        saberEdgesMesh.MeshData = newSaber.DatFiles.SaberGlowingEdgesBytes;
                        saberHandleMesh.MeshData = newSaber.DatFiles.SaberHandleBytes;
                        saberBladeMesh.Name = $"{newSaber.ID}SaberBlade";
                        saberEdgesMesh.Name = $"{newSaber.ID}SaberGlowingEdges";
                        saberHandleMesh.Name = $"{newSaber.ID}SaberHandle";
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("Failed to find some internal component of the BasicSaber!", ex);
                    }

                    SwapToSaberID(manager, newSaber.ID);
                    return true;

                }
                else if (saberCfg != null && saberCfg.SaberID != null)
                {
                    if (SaberExists(saberFile, saberCfg.SaberID))
                    {
                        if (GetCurrentSaberID(manager) == saberCfg.SaberID)
                        {
                            Log.LogMsg($"The saber ID {saberCfg.SaberID} is already set, don't need to make any changes.");
                            return true;
                        }
                        else
                        {
                            SwapToSaberID(manager, saberCfg.SaberID);
                            return true;
                        }
                    }
                    else
                    {
                        Log.LogErr($"The saber ID {saberCfg.SaberID} does not exist!");
                    }
                }
                else
                {
                    Log.LogMsg("Saber config is null, saber configuration will not be changed.");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Log.LogErr("Failed to update saber configuration.", ex);
                return false;
            }
            return false;
        }

        private bool SaberExists(AssetsFile saberFile, string saberID)
        {
            return saberFile.FindAsset<GameObject>(x => x.Object.Name == $"{saberID}Saber") != null;
        }

        private Transform GetSaberParentTransform(AssetsManager manager)
        {
            var basicSaberModel = GetBasicSaberModel(manager);
            var transform = basicSaberModel.Components.FirstOrDefault(x => x.Object is Transform)?.Object as Transform;
            if (transform == null)
                throw new Exception("Couldn't find Transform on BasicSaberModel!");
            return transform;
            //var saberParent = (transform.Children.FirstOrDefault(x => x.Object is Transform
            //        && ((x.Object as Transform).GameObject?.Object?.Name?.EndsWith("Saber") ?? false)).Object as Transform);
            //if (saberParent == null)
            //    throw new Exception("Could not find child transform of BasicSaberModel!");
            //return saberParent;
        }

        private string GetCurrentSaberID(AssetsManager manager)
        {
            var saberParent = GetSaberParentTransform(manager);
            var saberChild = saberParent?.GameObject?.Object;
            if (saberChild == null)
                throw new Exception("Couldn't find child saber game object of transform.");
            return saberChild.Name.Substring(0, saberChild.Name.Length - 5);
        }

        private void SwapToSaberID(AssetsManager manager, string saberID)
        {
            var saberParentTrans = GetSaberParentTransform(manager);

            var newSaber = manager.MassFirstOrDefaultAsset<GameObject>(x => x.Object.Name == $"{saberID}Saber")?.Object;
            if (newSaber == null)
                throw new Exception($"Saber with ID {saberID} does not exist!");

            var newSaberTransform = newSaber.Components.FirstOrDefault(x => x.Object is Transform).Object as Transform;
            if (newSaberTransform == null)
                throw new Exception($"Saber with ID {saberID} is missing its parent transform!");

            
            var saberChild = saberParentTrans.Children.FirstOrDefault(x => x.Object.GameObject?.Object?.Name?.EndsWith("Saber") ?? false);
            if (saberChild == null)
                throw new Exception("Couldn't find a game object on the BasicSaberModel Transform that ended with -Saber!");
            int saberIndex = saberParentTrans.Children.IndexOf(saberChild);
            saberChild.Object.Father = null;
            saberParentTrans.Children[saberIndex] = newSaberTransform.PtrFrom(saberParentTrans) as ISmartPtr<Transform>;
            newSaberTransform.Father = saberParentTrans.PtrFrom(newSaberTransform);
        }
        
        #endregion


        private BeatSaberQuestomConfig GetConfig(bool suppressImages)
        {
            BeatSaberQuestomConfig config = new BeatSaberQuestomConfig();
            var mainPack = GetMainLevelPack();
            foreach (var packPtr in mainPack.BeatmapLevelPacks)
            {
                var pack = packPtr.Target.Object;
                if (HideOriginalPlaylists && BSConst.KnownLevelPackIDs.Contains(pack.PackID))
                    continue;

                var packModel = new BeatSaberPlaylist() { PlaylistName = pack.PackName, PlaylistID = pack.PackID, LevelPackObject = pack };
                var collection = pack.BeatmapLevelCollection.Object;

                //get cover art for playlist
                if (!suppressImages)
                {
                    try
                    {
                        var coverSprite = pack.CoverImage.Object;
                        var coverTex = coverSprite.Texture.Object;
                        packModel.CoverArtBytes = coverTex.ToPngBytes();
                        packModel.CoverArtBase64PNG = Convert.ToBase64String(packModel.CoverArtBytes);
                    }
                    catch (Exception ex)
                    {
                        Log.LogErr($"Unable to convert texture for playlist ID '{pack.PackID}' cover art", ex);
                    }
                }
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
                    if (!suppressImages)
                    {
                        try
                        {
                            var songCover = songObj.CoverImageTexture2D.Object;
                            try
                            {
                                songModel.CoverArtBytes = songCover.ToPngBytes();
                                songModel.CoverArtBase64PNG = Convert.ToBase64String(songModel.CoverArtBytes);
                            }
                            catch (Exception ex)
                            {
                                Log.LogErr($"Unable to convert texture for song ID '{songModel.SongID}' cover", ex);
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.LogErr($"Exception loading/converting the cover image for song id '{songObj.LevelID}'", ex);
                        }
                    }
                    packModel.SongList.Add(songModel);
                }
                config.Playlists.Add(packModel);
            }
            return config;
        }

        private void PreloadFiles()
        {
            _assetsLoadOrder.ForEach(x => _manager.GetAssetsFile(x));
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

        private void UpdateMusicConfig(BeatSaberQuestomConfig config)
        {
            //get the old config before we start on this
            var originalConfig = GetConfig(false);
            var songsAssetFile = GetSongsAssetsFile();
            var aoModel = GetAlwaysOwnedModel();
            foreach (var playlist in config.Playlists)
            {
                UpdatePlaylistConfig(songsAssetFile, playlist);
            }

            //open the assets with the main levels collection, find the file index of sharedassets17.assets, and add the playlists to it

            var mainLevelPack = GetMainLevelPack();


            var packsToUnlink = mainLevelPack.BeatmapLevelPacks.Where(x => !HideOriginalPlaylists || !BSConst.KnownLevelPackIDs.Contains(x.Object.PackID)).ToList();
            var packsToRemove = mainLevelPack.BeatmapLevelPacks.Where(x => !BSConst.KnownLevelPackIDs.Contains(x.Object.PackID) && !config.Playlists.Any(y => y.PlaylistID == x.Object.PackID)).Select(x => x.Object).ToList();
            foreach (var unlink in packsToUnlink)
            {
                mainLevelPack.BeatmapLevelPacks.Remove(unlink);
                unlink.Dispose();
            }

            var oldSongs = originalConfig.Playlists.SelectMany(x => x.SongList).Select(x => x.LevelData).Distinct();
            var newSongs = config.Playlists.SelectMany(x => x.SongList).Select(x => x.LevelData).Distinct();

            //don't allow removal of the actual tracks or level packs that are built in, although you can unlink them from the main list
            var removeSongs = oldSongs.Where(x => !newSongs.Contains(x) && !BSConst.KnownLevelIDs.Contains(x.LevelID)).Distinct().ToList();

            var addedSongs = newSongs.Where(x => !oldSongs.Contains(x));

            var removedPlaylistCount = originalConfig.Playlists.Where(x => !config.Playlists.Any(y => y.PlaylistID == x.PlaylistID)).Count();
            var newPlaylistCount = config.Playlists.Where(x => !originalConfig.Playlists.Any(y => y.PlaylistID == x.PlaylistID)).Count();
            //
            //
            //TODO: clean up cover art, it's leaking!
            //
            //
            List<string> audioFilesToDelete = new List<string>();
            removeSongs.ForEach(x => RemoveLevelAssets(x, audioFilesToDelete));

            packsToRemove.ForEach(x => RemoveLevelPackAssets(x));

            //relink all the level packs in order
            var addPacks = config.Playlists.Select(x => x.LevelPackObject.PtrFrom(mainLevelPack));
            mainLevelPack.BeatmapLevelPacks.AddRange(addPacks);

            //link them to the always owned ones
            var addPacksOwned = config.Playlists.Select(x => x.LevelPackObject.PtrFrom(aoModel));
            aoModel.AlwaysOwnedPacks.AddRange(addPacksOwned);

            ////do a first loop to guess at the file size
            //Int64 originalApkSize = new FileInfo(_apkFilename).Length;
            //Int64 sizeGuess = originalApkSize;
            //foreach (var pl in config.Playlists)
            //{
            //    foreach (var sng in pl.SongList)
            //    {
            //        if (sng.SourceOgg != null)
            //        {
            //            var clip = sng.LevelData.AudioClip.Object;
            //            sizeGuess += new FileInfo(sng.SourceOgg).Length;
            //        }
            //    }
            //}
            //foreach (var toDelete in audioFilesToDelete)
            //{
            //    sizeGuess -= apkFileProvider.GetFileSize(BSConst.KnownFiles.AssetsRootPath + toDelete);
            //}

            Log.LogMsg("");
            Log.LogMsg("Playlists:");
            Log.LogMsg($"  Added:   {newPlaylistCount}");
            Log.LogMsg($"  Removed: {removedPlaylistCount}");
            Log.LogMsg("");
            Log.LogMsg("Songs:");
            Log.LogMsg($"  Added:   {addedSongs.Count()}");
            Log.LogMsg($"  Removed: {removeSongs.Count()}");
            Log.LogMsg("");
            //Log.LogMsg($"Original APK size:     {originalApkSize:n0}");
            //Log.LogMsg($"Guesstimated new size: {sizeGuess:n0}");
            //Log.LogMsg("");

            //if (sizeGuess > Int32.MaxValue)
            //{
            //    Log.LogErr("***************ERROR*****************");
            //    Log.LogErr($"Guesstimating a file size around {sizeGuess / (Int64)1000000}MB , this will crash immediately upon launch.");
            //    Log.LogErr($"The file size MUST be less than {Int32.MaxValue / (int)1000000}MB");
            //    Log.LogErr("***************ERROR*****************");
            //    throw new OverflowException("File might exceed 2.1GB, aborting.");
            //}

            ////////START WRITING DATA



            //todo: save here?


            foreach (var pl in config.Playlists)
            {
                foreach (var sng in pl.SongList)
                {
                    if (sng.SourceOgg != null)
                    {
                        var clip = sng.LevelData.AudioClip.Object;

                        FileProvider.WriteFile(sng.SourceOgg, AssetsRootPath + clip.Resource.Source, true, false);
                        //saftey check to make sure we aren't removing a file we just put here
                        if (audioFilesToDelete.Contains(clip.Resource.Source))
                        {
                            Log.LogErr($"Level id '{sng.LevelData.LevelID}' wrote file '{clip.Resource.Source}' that was on the delete list...");
                            audioFilesToDelete.Remove(clip.Resource.Source);
                        }
                    }

                    //todo: save on some interval to save ram?
                }
            }

            if (audioFilesToDelete.Count > 0)
            {
                Log.LogMsg($"Deleting {audioFilesToDelete.ToString()} audio files");
                foreach (var toDelete in audioFilesToDelete)
                {
                    //Log.LogMsg($"Deleting audio file {toDelete}");
                    FileProvider.Delete(BSConst.KnownFiles.AssetsRootPath + toDelete);
                }
            }
        }

        private void UpdateColorConfig(SimpleColorSO[] colors)
        {
            var manager = GetColorManager();

            var colorA = colors[0];
            var colorB = colors[1];

            if (colorA != null)
            {
                (manager.ColorA.Object as SimpleColorSO).color = colorA.color;
            }
            if (colorB != null)
                (manager.ColorB.Object as SimpleColorSO).color = colorB.color;
            // Reset
            if (colorA == null && colorB == null)
            {
                (manager.ColorA.Object as SimpleColorSO).color = BSConst.Colors.DefaultColorA;
                (manager.ColorB.Object as SimpleColorSO).color = BSConst.Colors.DefaultColorB;
            }
        }

        private void UpdateTextConfig(List<(string, string)> texts)
        {
            var textAsset = GetBeatSaberTextAsset();
            var textKeyPairs = Utils.TextUtils.ReadLocaleText(textAsset.Script, new List<char>() { ',', ',', '\n' });
            Utils.TextUtils.ApplyWatermark(textKeyPairs);
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


    }
}
