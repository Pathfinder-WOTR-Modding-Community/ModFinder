using Newtonsoft.Json;
using System.Collections.Generic;

namespace ModFinder.Infrastructure
{
    public class ModListBlob
    {
        [JsonProperty] public List<ModDetailsInternal> m_AllMods;
    }

}
