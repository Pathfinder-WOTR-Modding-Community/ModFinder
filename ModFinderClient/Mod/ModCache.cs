using Microsoft.VisualBasic.FileIO;
using ModFinder.UI;
using ModFinder.Util;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ModFinder.Mod
{
  public static class ModCache
  {
    internal static readonly string CacheDir = Path.Combine(Main.AppFolder, "CachedMods");
    private static readonly string ManifestFile = Path.Combine(CacheDir, "Manifest.json");

    /// <summary>
    /// Directories containing cached mods, indexed by ModId.
    /// </summary>
    private static Dictionary<ModId, CachedMod> CachedMods
    {
      get
      {
        _cachedMods ??= LoadManifest();
        return _cachedMods;
      }
    }
    private static Dictionary<ModId, CachedMod> _cachedMods;

    private static Dictionary<ModId, CachedMod> LoadManifest()
    {
      var cachedMods = new Dictionary<ModId, CachedMod>();
      if (!Directory.Exists(CacheDir))
      {
        _ = Directory.CreateDirectory(CacheDir);
      }

      if (File.Exists(ManifestFile))
      {
        IOTool.SafeRun(
          () =>
          {
            var manifest = IOTool.Read<CacheManifest>(ManifestFile);
            foreach (var mod in manifest.Mods)
            {
              var createTime = new DateTime(mod.CreateTime);
              if (DateTime.Now.Subtract(createTime).Days > 90)
              {
                Evict(mod.Id, cacheDir: mod.Dir);
              }
              else if (Directory.Exists(mod.Dir))
              {
                cachedMods.Add(mod.Id, mod);
              }
            }
          });
      }

      return cachedMods;
    }

    /// <summary>
    /// Attempts to restore a mod from the local cache.
    /// </summary>
    /// <returns>Install dir if installation succeeded, an empty string otherwise</returns>
    public static async Task<bool> TryRestoreMod(ModId id)
    {
      var baseInstall = ModInstaller.GetModPath(id.Type);

      if (id.Type is not ModType.UMM and not ModType.Owlcat)
      {
        throw new NotSupportedException($"Currently {id.Type} mods are not supported.");
      }

      if (!CachedMods.ContainsKey(id))
      {
        return false;
      }

      Logger.Log.Info($"Restoring {id.Id} from local cache.");
      var cachePath = CachedMods[id].Dir;
      var installPath = Path.Combine(baseInstall, new DirectoryInfo(cachePath).Name);

      if (Directory.Exists(installPath))
      {
        Logger.Log.Info($"Deleting existing installation at {installPath}");
        Directory.Delete(installPath, true);
      }

      await Task.Run(() => FileSystem.CopyDirectory(cachePath, installPath));
      Evict(id);
      return true;
    }

    /// <returns>True if a version of the mod is cached locally, false otherwise</returns>
    public static bool IsCached(ModId id)
    {
      return CachedMods.ContainsKey(id);
    }

    public static async Task Uninstall(ModViewModel mod, bool cache = true)
    {
      mod.InstallState = InstallState.Uninstalling;
      if (cache)
        await Cache(mod);

      Logger.Log.Info($"Uninstalling {mod.Name}");
      await Task.Run(() =>
      {
        if (mod.ModId.Type == ModType.Portrait)
        {
          foreach (var folder in Directory.EnumerateDirectories(Path.Combine(Main.WrathDataDir, "Portraits")))
          {
            var earmarkPath = Path.Combine(folder, "Earmark.json");
            if (File.Exists(earmarkPath))
            {
              var earmark = IOTool.Read<PortraitEarmark>(earmarkPath);
              if (earmark.ModID == mod.ModId.Id)
              {
                Directory.Delete(folder, true);
              }
            }
          }
        }
        else if (mod.ModId.Type == ModType.Owlcat)
        {
          Main.OwlcatMods.Remove(mod.ModId.Id);
          Directory.Delete(mod.ModDir.FullName, true);
        }
        else
        {
          Directory.Delete(mod.ModDir.FullName, true);
        }
      });

    }

    public static async Task Cache(ModViewModel mod)
    {
      var cachePath = Path.Combine(CacheDir, mod.ModDir.Name);
      if (Directory.Exists(cachePath))
      {
        Logger.Log.Warning($"Cache already exists for {mod.Name} at {cachePath}, deleting it");
        Directory.Delete(cachePath, true);
      }

      if (CachedMods.ContainsKey(mod.ModId))
        CachedMods.Remove(mod.ModId);

      await Task.Run(() =>
      {
        Logger.Log.Info($"Caching {mod.Name} at {cachePath}");
        FileSystem.CopyDirectory(mod.ModDir.FullName, cachePath);
      });

      var cachedMod = new CachedMod(mod.ModId, cachePath, DateTime.Now.Ticks);
      CachedMods.Add(mod.ModId, cachedMod);
      IOTool.SafeRun(UpdateManifest);
    }

    public static void Clear()
    {
      Logger.Log.Info("Clearing cache.");
      CachedMods.Clear();
      var cache = new DirectoryInfo(CacheDir);
      foreach (var dir in cache.EnumerateDirectories())
        dir.Delete(recursive: true);
    }

    private static void Evict(ModId id, string cacheDir = null)
    {
      if (cacheDir is null && !CachedMods.ContainsKey(id))
      {
        Logger.Log.Error($"Cache evict requsted for {id}, but no cache was found");
        return;
      }

      cacheDir ??= CachedMods[id].Dir;
      if (Directory.Exists(cacheDir))
      {
        Logger.Log.Info($"Removing {id} from cache");
        Directory.Delete(cacheDir, true);
        CachedMods.Remove(id);
        UpdateManifest();
      }
    }

    private static void UpdateManifest()
    {
      var manifest = new CacheManifest(CachedMods.Values.ToList());
      IOTool.Write(manifest, ManifestFile);
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

      [JsonProperty]
      public long CreateTime { get; }

      public CachedMod(ModId id, string dir, long createTime)
      {
        Id = id;
        Dir = dir;
        CreateTime = createTime;
      }
    }
  }
}
