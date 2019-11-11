using QuestomAssets.AssetsChanger;
using System;
using System.Collections.Generic;
using System.Text;

namespace QuestomAssets.Mods
{
    public class ModContext
    {
        public ModContext(string modPath, QaeConfig config, Func<QuestomAssetsEngine> getEngine)
        {
            ModPath = modPath;
            Config = config;
            GetEngine = getEngine;
        }
        public string ModPath { get; }
        public QaeConfig Config { get; }
        public Func<QuestomAssetsEngine> GetEngine { get; }

        /// <summary>
        /// Used for uninstalls, this provides a read-only QAE opened against the unmodified beat saber APK
        /// </summary>
        public QuestomAssetsEngine BackupEngine { get; set; }
    }
}
