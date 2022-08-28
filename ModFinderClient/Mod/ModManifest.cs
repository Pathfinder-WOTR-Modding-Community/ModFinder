using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace ModFinder.Mod
{
  /// <summary>
  /// Master manifest containing a list of available mods.
  /// </summary>
  public class MasterManifest
  {
    /// <summary>
    /// List of URLs linking to a mod's <see cref="ModManifest"/> JSON file. Should be directly accessible from this
    /// URL, e.g. the raw link for a JSON file hosted on GitHub.
    /// </summary>
    [JsonProperty]
    public List<string> ModManifestUrls;

    [JsonConstructor]
    private MasterManifest(List<string> modManifestUrls)
    {
      ModManifestUrls = modManifestUrls;
    }
  }

  /// <summary>
  /// Manifest for an individual mod.
  /// </summary>
  public class ModManifest
  {
    /// <summary>
    /// Required. Display name in ModFinder.
    /// </summary>
    [JsonProperty]
    public string Name { get; }

    /// <summary>
    /// Required. Mod author displayed in ModFinder.
    /// </summary>
    [JsonProperty]
    public string Author { get; }

    /// <summary>
    /// Required. Unique identifier for your mod.
    /// </summary>
    [JsonProperty]
    public ModId Id { get; }

    /// <summary>
    /// Required. Indicates which service hosts your mod. Needed to handle download / install behavior.
    /// </summary>
    [JsonProperty]
    public HostService Service { get; }

    /// <summary>
    /// Required. Details regarding your mod versions and how to download it.
    /// </summary>
    [JsonProperty]
    public VersionInfo Version { get; }

    /// <summary>
    /// Description displayed when users requests more info on your mod. Supports BBCode.
    /// </summary>
    [JsonProperty]
    public string Description { get; }

    /// <summary>
    /// Link to the home page of your mod.
    /// </summary>
    [JsonProperty]
    public HomePage Homepage { get; }

    [JsonConstructor]
    public ModManifest(
      string name,
      string author,
      ModId id,
      HostService hostService,
      VersionInfo version,
      string description = default,
      HomePage homepage = default)
    {
      Name = name;
      Author = author;
      Id = id;
      Service = hostService;
      Version = version;
      Description = description;
      Homepage = homepage;
    }

    public static ModManifest ForLocal(UMMModInfo info)
    {
      return new(info.DisplayName, info.Author, new(info.Id, ModType.UMM), HostService.Other, default);
    }
  }

  /// <summary>
  /// Indicates which 
  /// </summary>
  public enum HostService
  {
    /// <summary>
    /// GitHub supports directly downloading as long as you specify the *.zip url in LatestVersion.
    /// </summary>
    GitHub,
    Nexus,
    Other,
  }

  /// <summary>
  /// Wrapper to display a link to your mod's home page.
  /// </summary>
  public struct HomePage
  {
    [JsonProperty]
    public string Url { get; }

    /// <summary>
    /// Optional text to display like a hyperlink
    /// </summary>
    [JsonProperty]
    public string LinkText { get; }

    [JsonConstructor]
    public HomePage(string url, string linkText = "")
    {
      Url = url;
      LinkText = linkText;
    }
  }

  /// <summary>
  /// Contains details about your mod's versions.
  /// </summary>
  public struct VersionInfo
  {
    /// <summary>
    /// Required. Data needed to link to or fetch the latest version of your mod.
    /// </summary>
    [JsonProperty]
    public Release Latest { get; }

    /// <summary>
    /// Optional list of old release versions for generating a changelog.
    /// </summary>
    [JsonProperty]
    public List<Release> OldVersions { get; }

    /// <summary>
    /// Set to true if you list your old versions in order of newest to oldest so the changelog renders correctly.
    /// </summary>
    [JsonProperty]
    public bool ReverseVersionOrder { get; }

    [JsonConstructor]
    public VersionInfo(Release latest, List<Release> oldVersions, bool reverseVersionOrder)
    {
      Latest = latest;
      OldVersions = oldVersions;
      ReverseVersionOrder = reverseVersionOrder;
    }
  }

  /// <summary>
  /// Details about a release of your mod.
  /// </summary>
  public class Release
  {
    /// <summary>
    /// Required. String representation of your <see cref="ModVersion"/>.
    /// </summary>
    [JsonProperty]
    public string VersionString { get; }

    /// <summary>
    /// Required. Download url for this version.
    /// </summary>
    [JsonProperty]
    public string Url { get; }

    /// <summary>
    /// Description of the changes in this release. Supports BBCode.
    /// </summary>
    [JsonProperty]
    public string Changelog { get; }

    [JsonConstructor]
    public Release(string versionString, string url, string changelog)
    {
      VersionString = versionString;
      Url = url;
      Changelog = changelog;
    }
  }

  /// <summary>
  /// Indicates the style of mod, e.g. UMM or Owlcat
  /// </summary>
  public enum ModType
  {
    UMM,
    Owlcat
  }

  /// <summary>
  /// Unique mod identifier.
  /// </summary>
  /// 
  /// <remarks>Including <see cref="Type"/> ensures no conflict if a UMM and Owlcat mod have the same ID.</remarks>
  public class ModId
  {
    /// <summary>
    /// Required. The unique ID of the mod.
    /// </summary>
    /// 
    /// <remarks>
    /// <para>For UMM mods this is the <c>Id</c> in <c>Info.json</c></para>
    /// <para>For Owlcat mods this is the <c>UniqueName</c> in <c>OwlcatModificationManifest.json</c></para>
    /// </remarks>
    [JsonProperty]
    public string Id { get; }

    /// <summary>
    /// Required. The type of mod, currently either UMM or Owlcat
    /// </summary>
    [JsonProperty]
    public ModType Type { get; }

    [JsonConstructor]
    public ModId(string id, ModType type)
    {
      Id = id;
      Type = type;
    }

    public override bool Equals(object obj)
    {
      return obj is ModId id && Id == id.Id && Type == id.Type;
    }

    public override int GetHashCode()
    {
      return HashCode.Combine(Id, Type);
    }

    public static bool operator ==(ModId left, ModId right)
    {
      return left.Equals(right);
    }

    public static bool operator !=(ModId left, ModId right)
    {
      return !(left == right);
    }

    public override string ToString()
    {
      return $"{Id}-{Type}";
    }
  }
}
