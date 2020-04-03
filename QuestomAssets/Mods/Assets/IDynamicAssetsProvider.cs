using System;
using System.Collections.Generic;
using System.Text;

namespace QuestomAssets.Mods.Assets
{
    public interface IDynamicAssetsProvider
    {
        string GetFile(string assetName, string version);
    }
}