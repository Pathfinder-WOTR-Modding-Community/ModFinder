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
    [JsonProperty]
    public List<ModManifest> AvailableMods;
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

    [JsonProperty]
    public string Description { get; }

    [JsonProperty]
    public ModId Id { get; }

    [JsonProperty]
    public ModSource Source { get; }

    [JsonConstructor]
    private ModManifest() { }

    private ModManifest(string name, string author, ModId id, ModSource source)
    {
      Name = name;
      Author = author;
      Description = "";
      Id = id;
      Source = source;
    }

    public static ModManifest FromLocalMod(UMMModInfo info)
    {
      return new(info.DisplayName, info.Author, new(info.Id, ModType.UMM), new());
    }
  }

  /// <summary>
  /// Details about where the mod is hosted.
  /// </summary>
  /// 
  /// <remarks>This should be kept a union.</remarks>
  public class ModSource
  {
    [JsonProperty]
    public GitHubInfo GitHub { get; }
  }

  /// <summary>
  /// Details about a mod hosted on GitHub.
  /// </summary>
  public class GitHubInfo
  {
    /// <summary>
    /// URL to the GitHub repo hosting the mod.
    /// </summary>
    [JsonProperty]
    public string RepoUrl { get; }

    /// <summary>
    /// Prefix of the release zip, e.g. "MyMod-1.0.0.zip" should use "MyMod". Used to find the latest release.
    /// </summary>
    [JsonProperty]
    public string ZipPrefix { get; }
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
    /// The unique ID of the mod.
    /// </summary>
    /// 
    /// <remarks>
    /// <para>For UMM mods this is the <c>Id</c> in <c>Info.json</c></para>
    /// <para>For Owlcat mods this is the <c>UniqueName</c> in <c>OwlcatModificationManifest.json</c></para>
    /// </remarks>
    [JsonProperty]
    public string Id { get; }

    /// <summary>
    /// The type of mod, currently either UMM or Owlcat
    /// </summary>
    [JsonProperty]
    public ModType Type { get; }

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
