using System.Collections.Generic;

namespace ModFinder.Mod
{
  /// <summary>
  /// A copy of the JSON structure used for UMM mod manifests, i.e. <c>Info.json</c>
  /// </summary>
  public class UMMModInfo
  {
    public string Id { get; set; }
    public string Version { get; set; }

    public string Author { get; set; }
    public string DisplayName { get; set; }
    public string HomePage { get; set; }

    public List<string> Requirements { get; set; }

    public bool IsValid()
    {
      return !string.IsNullOrEmpty(Id) && !string.IsNullOrEmpty(Version);
    }
  }

  /// <summary>
  /// A copy of the JSON structure used for Owlcat's mod manifest, i.e. <c>OwlcatModificationManifest.json</c>
  /// </summary>
  public class OwlcatModInfo
  {
    public string UniqueName { get; set; }
    public string Version { get; set; }

    public string Author { get; set; }
    public string DisplayName { get; set; }
    public string HomePage { get; set; }

    public string Description { get; set; }

    public bool IsValid()
    {
      return !string.IsNullOrEmpty(UniqueName) && !string.IsNullOrEmpty(Version);
    }
  }
}
