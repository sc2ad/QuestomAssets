using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Text;

namespace QuestomAssets.Mods
{
    // TODO: ADD VERSION CHECKS
    public class ModDependencies
    {
        public ModDependency[] Dependencies { get; set; }
        /// <summary>
        /// Checks if all dependencies are valid in the provided context.
        /// Will throw ModDependencyException if any dependency is not valid.
        /// </summary>
        /// <param name="context"></param>
        public void Check(ModContext context)
        {
            if (Dependencies == null)
                return;
            foreach (var d in Dependencies)
            {
                d.Check(context);
            }
        }
        /// <summary>
        /// Acts as a dependency of a mod on a: <see cref="RequiredType"/>
        /// </summary>
        public class ModDependency
        {
            public DependencyDefinition RequiredItem { get; set; }
            public RequiredType Type { get; set; }
            /// <summary>
            /// Checks if this dependency is valid in the provided context.
            /// Will throw ModDependencyException if it is not valid.
            /// </summary>
            /// <param name="context"></param>
            public void Check(ModContext context)
            {
                if (RequiredItem == null)
                    throw new InvalidOperationException($"{nameof(RequiredItem)} parameter for a ModDependency must not be null!");
                switch (Type)
                {
                    case RequiredType.AssetMod:
                    case RequiredType.HookMod:
                        CheckMod(context);
                        break;
                    case RequiredType.AssetModFromFile:
                    case RequiredType.HookModFromFile:
                        CheckMod(context, true);
                        break;
                    default:
                    case RequiredType.File:
                        CheckFile(context);
                        break;
                }
            }
            private void CheckFile(ModContext context)
            {
                if (string.IsNullOrWhiteSpace(RequiredItem.FileName))
                    throw new InvalidOperationException($"DependencyDefinition property: {nameof(RequiredItem.FileName)} must not be null or empty!");
                if (!context.Config.RootFileProvider.FileExists(RequiredItem.FileName))
                    throw new ModDependencyException($"Dependency requiring {RequiredItem.FileName} failed! Required file not found!");
            }
            private void CheckMod(ModContext context, bool fromFile = false)
            {
                if (!fromFile)
                {
                    // Check DependencyDefinition
                    if (string.IsNullOrWhiteSpace(RequiredItem.ID))
                        throw new InvalidOperationException($"DependencyDefinition property: {nameof(RequiredItem.ID)} must not be null or empty!");
                    if (string.IsNullOrWhiteSpace(RequiredItem.Version))
                        throw new InvalidOperationException($"DependencyDefinition property: {nameof(RequiredItem.Version)} must not be null or empty!");
                    var found = context.GetEngine().ModManager.Mods.FindAll((md) =>
                    {
                        return md.ID == RequiredItem.ID && md.Version == RequiredItem.Version 
                        && (string.IsNullOrWhiteSpace(RequiredItem.GameVersion) || md.TargetBeatSaberVersion == RequiredItem.GameVersion);
                    });
                    if (found == null || found.Count == 0)
                        throw new ModDependencyException($"Mod Dependency requiring {RequiredItem.ID} ID failed! Required mod not found!");
                } else
                {
                    // Check DependencyDefinition.FileName
                    if (string.IsNullOrWhiteSpace(RequiredItem.FileName))
                        throw new InvalidOperationException($"DependencyDefinition property: {nameof(RequiredItem.FileName)} must not be null or empty!");
                    if (!context.Config.RootFileProvider.FileExists(RequiredItem.FileName))
                        throw new InvalidOperationException($"Mod dependency requiring {RequiredItem.FileName} failed! Required dependency file not found!");
                    try
                    {
                        var mdc = JsonConvert.DeserializeObject<ModDefinition>(context.Config.RootFileProvider.ReadToString(RequiredItem.FileName));
                        var found = context.GetEngine().ModManager.Mods.FindAll((md) => md.Equals(mdc));
                        if (found == null || found.Count == 0)
                            throw new ModDependencyException($"Mod Dependency requiring {RequiredItem.FileName} dependency file failed! Required mod not found!");
                    } catch (JsonException e)
                    {
                        throw new ModDependencyException($"{RequiredItem.FileName} is not a valid ModDefinition JSON file!" + e);
                    }
                }
            }
            [JsonConverter(typeof(StringEnumConverter))]
            public enum RequiredType
            {
                File,
                AssetMod,
                HookMod,
                AssetModFromFile,
                HookModFromFile
            }
        }
        public class DependencyDefinition
        {
            public string FileName { get; set; }
            // For ModDefinitions
            public string ID { get; set; }
            public string Version { get; set; }
            public string GameVersion { get; set; }
        }
        public class ModDependencyException : Exception
        {
            public ModDependencyException(string message) : base(message)
            {
            }
        }
    }
}
