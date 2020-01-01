using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Text;

namespace QuestomAssets.Mods
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum ModCategory
    {
        [ExclusiveMod]
        Saber,
        [ExclusiveMod]
        Note,
        [ExclusiveMod]
        Bloq,
        [ExclusiveMod]
        Platform,
        [ExclusiveMod]
        Title,
        [ExclusiveMod]
        Trail,
        Gameplay,
        Library,
        Other
    }
}
