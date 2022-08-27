using ModFinder.UI;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace ModFinder.Mods
{
  public class ModListBlob
  {
    [JsonProperty] public List<ModDetailsInternal> m_AllMods;
  }
}
