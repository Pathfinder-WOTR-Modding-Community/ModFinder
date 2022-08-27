using ModFinder.UI;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace ModFinder.Mods
{
  /// <summary>
  /// Master manifest containing a list of available mods.
  /// </summary>
  public class MasterManifest
  {
    [JsonProperty]
    public List<ModDetailsInternal> AvailableMods;
  }

  /// <summary>
  /// Manifest for an individual mod.
  /// </summary>
  public class ModManifest
  {
    [JsonProperty]
    public string Name { get; }

    [JsonProperty]
    public string Author { get; }
  }

  public class ModSource
  {
    [JsonProperty]
    public string DownloadLink { get; }
  }

  /// <summary>
  /// Manifest for an Owlcat mod.
  /// </summary>
  public class OwlcatManifest
  {

  }

  /// <summary>
  /// Common manifest params.
  /// </summary>
  private abstract class BaseManifest
  {

  }
}
