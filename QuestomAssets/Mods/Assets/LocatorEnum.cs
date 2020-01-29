using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Text;

namespace QuestomAssets.Mods.Assets
{
    /// <summary>
    /// The predefined locators that can be used.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum LocatorEnum
    {
        Saber,
        Note,
        MenuTitle,
        Trail,
        Platform
    }
    public class LocatorEnumHelper
    {
        /// <summary>
        /// Gets the Assets filename of the given LocatorEnum and Beat Saber Version.
        /// TODO: Make this perform a web request/local file read as opposed to hardcoded.
        /// </summary>
        /// <param name="e"></param>
        /// <param name="bsV"></param>
        /// <returns></returns>
        public static string GetFile(LocatorEnum? e, string bsV)
        {
            if (!e.HasValue)
                return null;
            if (bsV == "1.5.0")
            {
                switch (e.Value)
                {
                    case LocatorEnum.Saber:
                        return "sharedassets15.assets";
                    case LocatorEnum.Note:
                        return "sharedassets16.assets";
                    case LocatorEnum.MenuTitle:
                        return "sharedassets24.assets";
                    case LocatorEnum.Trail:
                        return "sharedassets15.assets";
                    case LocatorEnum.Platform:
                        return "sharedassets3.assets";
                }
            } else if (bsV == "1.6.0" || bsV == "1.7.0")
            {
                switch (e.Value)
                {
                    case LocatorEnum.Saber:
                        return "sharedassets18.assets";
                    case LocatorEnum.Note:
                        return "sharedassets19.assets";
                    case LocatorEnum.Trail:
                        return "sharedassets18.assets";
                    case LocatorEnum.MenuTitle:
                        return "sharedassets23.assets";
                    case LocatorEnum.Platform:
                        return "sharedassets3.assets";
                }
            }
            return null;
        }
    }
}
