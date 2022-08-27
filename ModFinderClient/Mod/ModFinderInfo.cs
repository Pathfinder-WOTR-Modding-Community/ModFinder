using Newtonsoft.Json;
using System.Collections.Generic;

namespace ModFinder.Mod
{
  /// <summary>
  /// Use this schema for a JSON file in your mod repo to provide info to ModFinder.
  /// </summary>
  public class ModFinderInfo
  {
    [JsonProperty]
    public VersionRelease LatestVersion;

    /// <summary>
    /// Used to render the changelog.
    /// </summary>
    [JsonProperty]
    public List<VersionRelease> OldVersions;

    /// <summary>
    /// Set to true if you list your old versions in order of newest to oldest so the changelog renders correctly.
    /// </summary>
    [JsonProperty]
    public bool ReverseVersionOrder { get; }

    [JsonConstructor]
    private ModFinderInfo(VersionRelease latestVersion, List<VersionRelease> oldVersions, bool reverseVersionOrder)
    {
      LatestVersion = latestVersion;
      OldVersions = oldVersions;
      ReverseVersionOrder = reverseVersionOrder;
    }
  }

  /// <summary>
  /// Details about a version release.
  /// </summary>
  public class VersionRelease
  {
    /// <summary>
    /// String representation of your <see cref="ModVersion"/>.
    /// </summary>
    [JsonProperty]
    public string Version { get; }

    /// <summary>
    /// Download url for this version.
    /// </summary>
    [JsonProperty]
    public string Url { get; }

    /// <summary>
    /// Description of the changes in this release.
    /// </summary>
    [JsonProperty]
    public string Changelog { get; }

    [JsonConstructor]
    private VersionRelease(string version, string url, string changelog)
    {
      Version = version;
      Url = url;
      Changelog = changelog;
    }
  }
}
