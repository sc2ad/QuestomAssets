using QuestomAssets.AssetsChanger;
using QuestomAssets.Download;
using QuestomAssets.Mods.Assets;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;

namespace QuestomAssets
{
    public class QaeConfig
    {
        public IFileProvider RootFileProvider { get; set; }
        public IFileProvider SongFileProvider { get; set; }
        public IFileProvider EmbeddedResourcesFileProvider { get; set; }
        public HttpClient HttpClient { get; set; }
        public IDownloadService DownloadService { get; set; }
        public IDynamicAssetsProvider DynamicAssetsProvider { get; set; }

        public string AssetsPath { get; set; }

        public string SongsPath { get; set; }

        public string PlaylistArtPath { get; set; }

        public string ModsSourcePath { get; set; }

        public string ModsStatusFile { get; set; }

        public string BackupApkFileAbsolutePath { get; set; }

        //this should only ever be used in ugly scenarios where there's no backup, and one has to be created out of the modded apk.
        public string ModdedFallbackBackupPath { get; set; }

        public IFileProvider ModLibsFileProvider { get; set; }

        public string PlaylistsPath { get; set; }
    }
}