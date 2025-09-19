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
    /// URL to the automatically generated manifest containing an array of <see cref="ModManifest"/>
    /// </summary>
    [JsonProperty]
    public string GeneratedManifestUrl;

    /// <summary>
    /// List of URLs to externally hosted <see cref="ModManifest"/> JSON files.
    /// </summary>
    /// 
    /// <remarks>
    /// <para>
    /// Use this if you want to host your own manifest file. Otherwise just populate <c>internal_manifest.json</c> and
    /// the appropriate manifest is generated automatically.
    /// </para>
    /// 
    /// <para>
    /// The URL needs to be directly accessible to download, e.g. use raw links for files on GitHub.
    /// </para>
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
  ///
  /// </summary>
  public struct InstallModManifest
  {
    public string ModId;
    public string Version;

    public InstallModManifest(string modid, string version)
    {
      ModId = modid;
      Version = version;
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
    /// Required. A brief description of what your mod does.
    /// </summary>
    [JsonProperty]
    public string About { get; }

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
    /// Required. Records the last time this mod was checked for an update successfully.
    /// </summary>
    /// 
    /// <remarks>
    /// Automatic: All
    /// </remarks>
    [JsonProperty]
    public DateTime LastChecked { get; }

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
    public string HomepageUrl { get; }

    /// <summary>
    /// Tags describing your mod used for filtering.
    /// </summary>
    [JsonProperty]
    public List<Tag> Tags { get; }

    [JsonConstructor]
    public ModManifest(
      string name,
      string author,
      string about,
      ModId id,
      HostService service,
      VersionInfo version,
      DateTime lastChecked = default,
      string description = default,
      string homepageUrl = default,
      List<Tag> tags = default)
    {
      Name = name;
      Author = author;
      About = about;
      Id = id;
      Service = service;
      Version = version;
      LastChecked = lastChecked;
      Description = description;
      HomepageUrl = homepageUrl;
      Tags = tags ?? new();
    }

    public ModManifest(ModManifest manifest, VersionInfo version, DateTime lastChecked, string description)
    {
      Name = manifest.Name;
      Author = manifest.Author;
      About = manifest.About;
      Id = manifest.Id;
      Service = manifest.Service;
      HomepageUrl = manifest.HomepageUrl;
      Tags = manifest.Tags;

      // Updated automatically
      Version = version;
      LastChecked = lastChecked;
      Description = description;
    }

    public static ModManifest ForLocal(UMMModInfo info)
    {
      if (!info.IsValid())
        return null;

      return new(
        name: string.IsNullOrEmpty(info.DisplayName) ? info.Id : info.DisplayName,
        author: info.Author,
        about: string.Empty,
        id: new(info.Id, ModType.UMM),
        service: default,
        version: default,
        homepageUrl: string.IsNullOrEmpty(info.HomePage) ? default : info.HomePage);
    }

    public static ModManifest ForLocal(OwlcatModInfo info)
    {
      if (!info.IsValid())
        return null;

      return new(
        name: string.IsNullOrEmpty(info.DisplayName) ? info.UniqueName : info.DisplayName,
        author: info.Author,
        about: string.Empty,
        id: new(info.UniqueName, ModType.Owlcat),
        service: default, 
        version: default,
        homepageUrl: string.IsNullOrEmpty(info.HomePage) ? default : info.HomePage);
    }
  }

  /// <summary>
  /// Details about the hosting. <c>DownloadUrl</c> is universal but the per-service details should be a union, i.e.
  /// only a single one should be populated.
  /// </summary>
  public struct HostService
  {
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
    public string Name
    {
      get
      {
        if (IsLocal())
          return "Local";
        if (IsNexus())
          return "Nexus";
        return "Github";
      }
    }

    [JsonConstructor]
    public HostService(GitHub gitHub, Nexus nexus)
    {
      GitHub = gitHub;
      Nexus = nexus;
    }

    public bool IsGitHub()
    {
      return GitHub is not null;
    }

    public bool IsNexus()
    {
      return Nexus is not null;
    }

    public bool IsLocal()
    {
      return !IsGitHub() && !IsNexus();
    }
  }

  /// <summary>
  /// Details for mods hosted on GitHub. Supports automatic version updates, download, and install.
  /// </summary>
  /// 
  /// <remarks>
  /// Your release tags should be in the format <c>1.2.3e</c> in order to parse the version properly. Non-digit
  /// prefixes are ignored, e.g. <c>v1.2.1f</c> parses as <c>1.2.1f</c>.
  /// </remarks>
  public class GitHub
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
    /// <para>
    /// For example, MewsiferConsole hosts both <c>MewsiferConsole.1.1.1.zip</c> and
    /// <c>MewsiferConsole.Menu.1.0.0.zip</c>. Setting <c>MewsiferConsole\.[\d+]</c> would select the former while
    /// <c>MewsiferConsole\.Menu</c> would select the latter.
    /// </para>
    /// 
    /// <para>
    /// The version is specified using the tag so in this example both would be interpreted as the same version. For
    /// that reason you either need to share versionining with all mods in a repo or you need to define your
    /// <see cref="ModManifest"/> file in your own repo, add it to <see cref="MasterManifest.ExternalManifestUrls"/> in
    /// <c>master_manifest.json</c>, and remove it from <c>internal_manifest.json</c>. This gives you complete control
    /// over the versionining but you lose automatic versionining, description, and changelog generation.
    /// </para>
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
  public class Nexus
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
    public long ModID { get; }

    /// <summary>
    /// Optional. A mirror where releases can be downloaded from.
    /// </summary>
    /// 
    /// <remarks>
    /// E.g. "https://github.com/Pathfinder-WOTR-Modding-Community/WrathModsMirror", releases **must** be in the form Id.Id/Version with the filename Id.Id-Version.zip, resulting in:
    /// E.g. https://github.com/Pathfinder-WOTR-Modding-Community/WrathModsMirror/releases/download/BubbleBuffs%2F5.0.0/BubbleBuffs-5.0.0.zip
    /// </remarks>
    [JsonProperty]
    public string DownloadMirror { get; }

    [JsonConstructor]
    public Nexus(long modID, string downloadMirror = "")
    {
      ModID = modID;
      DownloadMirror = downloadMirror;
    }
  }

  /// <summary>
  /// Contains details about your mod's versions.
  /// </summary>
  ///
  /// <remarks>
  /// Automatic: <c>GitHub</c>, <c>Nexus</c>
  /// </remarks>
  public struct VersionInfo
  {
    /// <summary>
    /// Required. Data needed to link to or fetch the latest version of your mod.
    /// </summary>
    [JsonProperty]
    public Release Latest { get; }

    /// <summary>
    /// Required. Records when the update was detected.
    /// </summary>
    [JsonProperty]
    public DateTime LastUpdated { get; }

    /// <summary>
    /// Optional. The game version when this mod was marked as deprecated, or never.
    /// </summary>
    [JsonProperty]
    public ModVersion DeprecatedInGameVersion { get; }

    /// <summary>
    /// Version history used to generate changelog.
    /// </summary>
    [JsonProperty]
    public List<Release> VersionHistory { get; }

    [JsonConstructor]
    public VersionInfo(Release latest, DateTime lastUpdated, List<Release> versionHistory, ModVersion deprecatedInGameVersion = default)
    {
      Latest = latest;
      LastUpdated = lastUpdated;
      VersionHistory = versionHistory ?? new();
      DeprecatedInGameVersion = deprecatedInGameVersion;
    }
  }

  /// <summary>
  /// Details about a release of your mod.
  /// </summary>
  public struct Release
  {
    /// <summary>
    /// Required.
    /// </summary>
    [JsonProperty]
    public ModVersion Version { get; }

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
    public Release(ModVersion version, string url, string changelog = "")
    {
      Version = version;
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
    Owlcat,
    Portrait
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
    /// Required. The type of mod. Currently either UMM or Owlcat though only UMM is supported.
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
