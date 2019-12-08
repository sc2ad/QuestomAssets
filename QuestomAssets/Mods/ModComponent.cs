using Newtonsoft.Json;
using QuestomAssets.AssetOps;
using QuestomAssets.AssetsChanger;
using System;
using System.Collections.Generic;
using System.Text;

namespace QuestomAssets.Mods
{
    [JsonConverter(typeof(ModComponentTypeConverter))]
    public abstract class ModComponent
    {
        public List<ModDependency> Dependencies { get; set; }
        /// <summary>
        /// The type of modification this component will perform
        /// </summary>
        public abstract ModComponentType Type { get; }
        
        /// <summary>
        /// Checks the dependencies of the ModComponent, if they exist.
        /// If any dependencies fail to validate, a ModDependencies.ModDependencyException will be thrown.
        /// </summary>
        /// <param name="context"></param>
        public void CheckDependencies(ModContext context)
        {
            if (Dependencies != null)
                Dependencies.ForEach(md => md.Check(context)); // This will throw a ModDependency.ModDependencyException if it fails!
        }

        public abstract List<AssetOp> GetInstallOps(ModContext context);

        public abstract List<AssetOp> GetUninstallOps(ModContext context);
    }
}
