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
                    default:
                        return null;
                }
            }
            return null;
        }
    }
}
