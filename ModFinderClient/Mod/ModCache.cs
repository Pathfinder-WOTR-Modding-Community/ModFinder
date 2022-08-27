using Microsoft.VisualBasic.FileIO;
using ModFinder.UI;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ModFinder.Mod
{
  public static class ModCache
  {
    private static readonly string CacheDir = Path.Combine(Main.AppFolder, "CachedMods");
    private static readonly string ManifestFile = Path.Combine(CacheDir, "Manifest.json");

    /// <summary>
    /// Directories containing cached mods, indexed by ModId.
    /// </summary>
    private static Dictionary<VersionedModId, string> CachedMods
    {
      get
      {
        _cachedMods ??= LoadManifest();
        return _cachedMods;
      }
    }
    private static Dictionary<VersionedModId, string> _cachedMods;

    private static Dictionary<VersionedModId, string> LoadManifest()
    {
      var cachedMods = new Dictionary<VersionedModId, string>();
      if (File.Exists(ManifestFile))
      {
        var manifest = JsonConvert.DeserializeObject<CacheManifest>(File.ReadAllText(ManifestFile));
        foreach (var mod in manifest.Mods)
        {
          cachedMods.Add(mod.Id, mod.Dir);
        }
      }

      return cachedMods;
    }

    /// <summary>
    /// Attempts to restore a mod from the local cache.
    /// </summary>
    /// <returns>True if the mod was restored, false otherwise</returns>
    public static bool TryRestoreMod(ModId id, ModVersion version)
    {
      if (id.Type != ModType.UMM)
      {
        throw new NotSupportedException($"Currently {id.Type} mods are not supported.");
      }

      var versionedId = new VersionedModId(id, version);
      if (!CachedMods.ContainsKey(versionedId))
      {
        return false;
      }

      var cachePath = CachedMods[versionedId];
      FileSystem.CopyDirectory(cachePath, Path.Combine(ModInstaller.UMMInstallPath, Path.GetDirectoryName(cachePath)));
      Directory.Delete(cachePath, true);
      CachedMods.Remove(versionedId);
      return true;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="mod"> "ModDetails of the mod to be Cached/Uninstalled" </param>
    /// <param name="ModFolder">"Folder to be removed (Folder that contains your info.json, assemblies, etc...)"</param>
    public static void UninstallAndCache(ModViewModel mod, DirectoryInfo ModFolder)
    {
      //if (mod.ModType == ModType.Owlcat)
      //{
      //  Main.OwlcatMods.Remove(mod.Identifier);
      //}
      //if (!Directory.Exists(Path.Combine(Main.AppFolder, ModFolder.Name)))
      //{
      //  FileSystem.CopyDirectory(ModFolder.FullName, Path.Combine(Main.AppFolder, "CachedMods", ModFolder.Name));
      //  Directory.Delete(ModFolder.FullName, true);
      //  CachedMods.Add(new ModCache(new DirectoryInfo(Path.Combine(Main.AppFolder, "CachedMods", ModFolder.Name)), mod.Identifier));
      //}
    }

    private class CacheManifest
    {
      [JsonProperty]
      public readonly List<CachedMod> Mods;
    }

    /// <summary>
    /// Identifies a single cached mod.
    /// </summary>
    private class CachedMod
    {
      [JsonProperty]
      public VersionedModId Id { get; }

      [JsonProperty]
      public string Dir { get; }

      public CachedMod(ModId id, ModVersion version, string dir)
      {
        Id = new(id, version);
        Dir = dir;
      }
    }

    /// <summary>
    /// Combines Version & ID for ease of fetching mods from the cache.
    /// </summary>
    private class VersionedModId
    {
      [JsonProperty]
      public ModId Id { get; }

      [JsonProperty]
      public ModVersion Version { get; }

      public VersionedModId(ModId id, ModVersion version)
      {
        Id = id;
        Version = version;
      }

      public override bool Equals(object obj)
      {
        return obj is VersionedModId other && Id.Equals(other.Id) && Version.Equals(other.Version);
      }

      public override int GetHashCode()
      {
        return HashCode.Combine(Id, Version);
      }

      public static bool operator ==(VersionedModId left, VersionedModId right)
      {
        return left.Equals(right);
      }

      public static bool operator !=(VersionedModId left, VersionedModId right)
      {
        return !left.Equals(right);
      }
    }
  }
}
