using Microsoft.VisualBasic.FileIO;
using ModFinder.UI;
using ModFinder.Util;
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
    private static Dictionary<ModId, string> CachedMods
    {
      get
      {
        _cachedMods ??= LoadManifest();
        return _cachedMods;
      }
    }
    private static Dictionary<ModId, string> _cachedMods;

    private static Dictionary<ModId, string> LoadManifest()
    {
      var cachedMods = new Dictionary<ModId, string>();
      if (!Directory.Exists(CacheDir))
      {
        _ = Directory.CreateDirectory(CacheDir);
      }

      if (File.Exists(ManifestFile))
      {
        IOTool.Safe(
          () =>
          {
            var manifest = JsonConvert.DeserializeObject<CacheManifest>(File.ReadAllText(ManifestFile));
            foreach (var mod in manifest.Mods)
            {
              cachedMods.Add(mod.Id, mod.Dir);
            }
          });
      }

      return cachedMods;
    }

    /// <summary>
    /// Attempts to restore a mod from the local cache.
    /// </summary>
    /// <returns>True if the mod was restored, false otherwise</returns>
    public static bool TryRestoreMod(ModId id)
    {
      if (id.Type != ModType.UMM)
      {
        throw new NotSupportedException($"Currently {id.Type} mods are not supported.");
      }

      if (!CachedMods.ContainsKey(id))
      {
        return false;
      }

      var cachePath = CachedMods[id];
      FileSystem.CopyDirectory(cachePath, Path.Combine(ModInstaller.UMMInstallPath, Path.GetDirectoryName(cachePath)));
      Directory.Delete(cachePath, true);
      CachedMods.Remove(id);
      UpdateManifest();
      return true;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="mod"> "ModDetails of the mod to be Cached/Uninstalled" </param>
    /// <param name="ModFolder">"Folder to be removed (Folder that contains your info.json, assemblies, etc...)"</param>
    public static void UninstallAndCache(ModViewModel mod, DirectoryInfo ModFolder)
    {
      if (mod.ModType != ModType.UMM)
      {
        throw new InvalidOperationException($"{mod.ModType} is not supported");
      }
      if (!Directory.Exists(Path.Combine(Main.AppFolder, ModFolder.Name)))
      {
        var cachePath = Path.Combine(CacheDir, ModFolder.Name);
        FileSystem.CopyDirectory(ModFolder.FullName, cachePath);
        Directory.Delete(ModFolder.FullName, true);
        CachedMods.Add(mod.ModId, cachePath);
        IOTool.Safe(UpdateManifest);
      }
    }

    private static void UpdateManifest()
    {
      var manifest = new CacheManifest(CachedMods.Select(entry => new CachedMod(entry.Key, entry.Value)).ToList());
      File.WriteAllText(ManifestFile, JsonConvert.SerializeObject(manifest));
    }

    private class CacheManifest
    {
      [JsonProperty]
      public readonly List<CachedMod> Mods;

      [JsonConstructor]
      public CacheManifest(List<CachedMod> mods)
      {
        Mods = mods;
      }
    }

    /// <summary>
    /// Identifies a single cached mod.
    /// </summary>
    private class CachedMod
    {
      [JsonProperty]
      public ModId Id { get; }

      [JsonProperty]
      public string Dir { get; }

      public CachedMod(ModId id, string dir)
      {
        Id = id;
        Dir = dir;
      }
    }
  }
}
