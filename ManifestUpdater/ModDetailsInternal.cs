using ModFinder;
using ModFinder.Mod;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace ManifestUpdater
{
  public class ModListBlob
  {
    [JsonProperty] public List<ModDetailsInternal> m_AllMods;
  }

  /// <summary>
  /// This is the "static" state of a mod
  /// </summary>
  public class ModDetailsInternal
  {
    public string Author { get; set; }

    public string Description { get; set; }

    public string DownloadLink { get; set; }

    public string GithubOwner { get; set; }

    public string GithubRepo { get; set; }

    //This MUST match the UMMInfo.Id or OwlcatMod.UniqueName
    public ModId ModId { get; set; }

    public List<ChangelogEntry> Changelog { get; set; }

    public ModVersion Latest { get; set; }

    //These need to be manually filled in
    public string Name { get; set; }
    public long NexusModID { get; set; }

    public ModFinder.Mod.HostService Source { get; set; }

    public bool IsSame(ModId id) => id == ModId;
  }

  /// <summary>
  /// Unique mod identifier (technically a UMM mod and owlcat mod can have the same unique name so we need to disambiguate them here)
  /// </summary>
  public struct ModId
  {
    public string Identifier { get; set; }
    public ModType ModType { get; set; }

    public ModId(string identifier, ModType modtype)
    {
      Identifier = identifier;
      ModType = modtype;
    }

    public override bool Equals(object obj)
    {
      return obj is ModId id &&
             Identifier == id.Identifier &&
             ModType == id.ModType;
    }

    public override int GetHashCode()
    {
      return HashCode.Combine(Identifier, ModType);
    }

    public static bool operator ==(ModId left, ModId right)
    {
      return left.Equals(right);
    }

    public static bool operator !=(ModId left, ModId right)
    {
      return !(left == right);
    }
  }

  public struct ChangelogEntry
  {
    public ModVersion version;
    public string contents;

    public ChangelogEntry(ModVersion version, string contents)
    {
      this.version = version;
      this.contents = contents;
    }

    public override bool Equals(object obj)
    {
      return obj is ChangelogEntry other &&
             version.Equals(other.version) &&
             contents == other.contents;
    }

    public override int GetHashCode()
    {
      return HashCode.Combine(version, contents);
    }

    public void Deconstruct(out ModVersion version, out string contents)
    {
      version = this.version;
      contents = this.contents;
    }

    public static implicit operator (ModVersion version, string contents)(ChangelogEntry value)
    {
      return (value.version, value.contents);
    }

    public static implicit operator ChangelogEntry((ModVersion version, string contents) value)
    {
      return new ChangelogEntry(value.version, value.contents);
    }
  }
}