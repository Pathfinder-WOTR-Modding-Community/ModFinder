using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Windows.Markup;

namespace ModFinder.Mod
{
  /// <summary>
  /// Master manifest containing a list of available mods.
  /// </summary>
  public class MasterManifest
  {
    /// <summary>
    /// URL to the automatically generated manifest containing an array of <see cref="ModManifest"/>
    /// </summary>
    [JsonProperty]
    public string GeneratedManifestUrl;

    /// <summary>
    /// List of URLs to externally hosted <see cref="ModManifest"/> JSON files.
    /// </summary>
    /// 
    /// <remarks>
    /// Use this if you want to host your own manifest file. Otherwise just populate <c>internal_manifest.json</c> and
    /// the appropriate manifest is generated automatically.
    /// </remarks>
    [JsonProperty]
    public List<string> ExternalManifestUrls;

    [JsonConstructor]
    private MasterManifest(string generatedManifestUrl, List<string> externalManifestUrls)
    {
      GeneratedManifestUrl = generatedManifestUrl;
      ExternalManifestUrls = externalManifestUrls;
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
    /// 
    /// <remarks>
    /// Automatic: <c>GitHub</c>, <c>Nexus</c>
    /// </remarks>
    [JsonProperty]
    public VersionInfo Version { get; }

    /// <summary>
    /// Description displayed when users requests more info on your mod. Supports BBCode.
    /// </summary>
    ///
    /// <remarks>
    /// Automatic: <c>GitHub</c>, <c>Nexus</c>
    /// </remarks>
    [JsonProperty]
    public string Description { get; }

    /// <summary>
    /// Link to the home page of your mod.
    /// </summary>
    [JsonProperty]
    public HomePage Homepage { get; }

    /// <summary>
    /// Tags describing your mod used for filtering.
    /// </summary>
    [JsonProperty]
    public List<Tag> Tags { get; }

    [JsonConstructor]
    public ModManifest(
      string name,
      string author,
      ModId id,
      HostService service,
      VersionInfo version,
      string description = default,
      HomePage homepage = default,
      List<Tag> tags = default)
    {
      Name = name;
      Author = author;
      Id = id;
      Service = service;
      Version = version;
      Description = description;
      Homepage = homepage;
      Tags = tags;
    }

    public static ModManifest ForLocal(UMMModInfo info)
    {
      return new(info.DisplayName, info.Author, new(info.Id, ModType.UMM), service: default,  version: default);
    }
  }

  /// <summary>
  /// Tags used to filter mods when browsing.
  /// </summary>
  public enum Tag
  {
    Audio,
    Bugfix,
    Content,
    Gameplay,
    Homebrew,
    Miscellaneous,
    Portraits,
    Romance,
    Story,
    UserInterface,
    Utilities,
    Visuals,
  }

  /// <summary>
  /// Details about the hosting. <c>DownloadUrl</c> is universal but the per-service details should be a union, i.e.
  /// only a single one should be populated.
  /// </summary>
  public struct HostService
  {
    /// <summary>
    /// Required. URL used to download the mod. 
    /// </summary>
    /// 
    /// <remarks>
    /// <para>Automatic: <c>GitHub</c> and <c>Nexus</c> mods.</para>
    /// <para>Not populated for mods discovered locally.</para>
    /// </remarks>
    [JsonProperty]
    public string DownloadUrl { get; }

    /// <summary>
    /// Details for mods hosted on GitHub.
    /// </summary>
    [JsonProperty]
    public GitHub GitHub { get; }

    /// <summary>
    /// Details for mods hosted on Nexus.
    /// </summary>
    [JsonProperty]
    public Nexus Nexus { get; }

    [JsonConstructor]
    public HostService(string downloadUrl, GitHub gitHub, Nexus nexus)
    {
      DownloadUrl = downloadUrl;
      GitHub = gitHub;
      Nexus = nexus;
    }
  }

  /// <summary>
  /// Details for mods hosted on GitHub. Supports automatic version updates, download, and install.
  /// </summary>
  public struct GitHub
  {
    /// <summary>
    /// Required. Name of the GitHub account or organization hosting the mod repo.
    /// </summary>
    [JsonProperty]
    public string Owner { get; }

    /// <summary>
    /// Required. Name of the GitHub repo hosting the mod.
    /// </summary>
    [JsonProperty]
    public string RepoName { get; }

    /// <summary>
    /// Setting this will filter the release assets for the one matching the specified regex string. Useful if you have
    /// multiple releases / mods in the same repo.
    /// </summary>
    /// 
    /// <remarks>
    /// For example, MewsiferConsole hosts both <c>MewsiferConsole.1.1.1.zip</c> and
    /// <c>MewsiferConsole.Menu.1.0.0.zip</c>. Setting <c>MewsiferConsole\.[\d+]</c> would select the former while
    /// <c>MewsiferConsole\.Menu</c> would select the latter.
    /// </remarks>
    [JsonProperty]
    public string ReleaseFilter { get; }

    [JsonConstructor]
    public GitHub(string owner, string repoName, string releaseFilter)
    {
      Owner = owner;
      RepoName = repoName;
      ReleaseFilter = releaseFilter;
    }
  }

  /// <summary>
  /// Details for mods hosted on Nexus. Supports automatic version updates.
  /// </summary>
  public struct Nexus
  {
    /// <summary>
    /// Required. The ID for the mod as displayed in the URL on nexus mods.
    /// </summary>
    /// 
    /// <remarks>
    /// E.g. https://www.nexusmods.com/pathfinderwrathoftherighteous/mods/360 has a ModID of 360. Note that only mods
    /// hosted under <c>pathfinderwrathoftherighteous</c> work.
    /// </remarks>
    [JsonProperty]
    public string ModID { get; }

    [JsonConstructor]
    public Nexus(string modID)
    {
      ModID = modID;
    }
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
