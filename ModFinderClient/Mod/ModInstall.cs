using System;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;
using System.IO.Compression;
using ModFinder.Util;
using ModFinder.UI;

namespace ModFinder.Mod
{
  /// <summary>
  /// Manages mod installation.
  /// </summary>
  public static class ModInstaller
  {
    public static readonly string UMMInstallPath = Path.Combine(Main.WrathPath.FullName, "Mods");

    public static async Task<InstallResult> Install(ModManifest manifest, ModVersion requestedVersion)
    {
      if (manifest.Id.Type != ModType.UMM)
      {
        return new($"Currently {manifest.Id.Type} mods are not supported.");
      }

      if (ModCache.TryRestoreMod(manifest.Id, requestedVersion))
      {
        return new(InstallState.Installed);
      }

      if (manifest.Source.GitHub is not null)
      {
        return await InstallFromRemoteZip(manifest);
      }

      return new("Unknown mod source");
    }

    private static async Task<InstallResult> InstallFromRemoteZip(ModManifest manifest)
    {
      var name = mod.UniqueId + "_" + mod.Latest + ".zip"; //what about non-zip?
      var file = Main.CachePath(name);
      if (!File.Exists(file))
      {
        System.Net.WebClient web = new();
        await web.DownloadFileTaskAsync(mod.DownloadLink, file);
      }

      return await InstallFromZip(file, mod.ModId);
    }

    public static async Task<InstallResult> InstallFromZip(string path, ModId? current = null)
    {
      using var zip = ZipFile.OpenRead(path);
      var asUmm = zip.Entries.FirstOrDefault(e => e.Name.Equals("Info.json", StringComparison.OrdinalIgnoreCase));
      var asOwl = zip.Entries.FirstOrDefault(e => e.Name.Equals("OwlcatModificationManifest.json", StringComparison.OrdinalIgnoreCase));

      string destination = null;

      ModDetails newMod = new();

      if (asUmm != null)
      {
        destination = Path.Combine(Main.WrathPath.FullName, "Mods");

        var info = IOTool.Read<UMMModInfo>(asUmm.Open());

        newMod.ModId = new()
        {
          Id = info.Id,
          Type = ModType.UMM,
        };

        newMod.Latest = ModVersion.Parse(info.Version);
        newMod.Author = info.Author;
        newMod.Source = ModSource.Other;
        newMod.Name = info.DisplayName;

        //Some mods are naughty and don't have a folder inside the zip
        if (asUmm.FullName == asUmm.Name)
          destination = Path.Combine(destination, info.Id);
      }
      else if (asOwl != null)
      {
        destination = Path.Combine(Main.WrathDataDir, "Modifications");

        var info = IOTool.Read<OwlcatModInfo>(asOwl.Open());

        newMod.ModId = new()
        {
          Id = info.UniqueName,
          Type = ModType.Owlcat,
        };

        newMod.Latest = ModVersion.Parse(info.Version);
        newMod.Author = info.Author;
        newMod.Source = ModSource.Other;
        newMod.Name = info.DisplayName;
        newMod.Description = info.Description;

      }

      if (current != null && current != newMod.ModId)
      {
        return new("Id in mod.zip does not match id in mod manifest");
      }


      if (!ModDatabase.Instance.TryGet(newMod.ModId, out var mod))
      {
        mod = new(newMod);
        ModDatabase.Instance.Add(mod);
      }

      mod.Version = newMod.Latest;

      await Task.Run(() => zip.ExtractToDirectory(destination, true));

      if (mod.ModId.Type == ModType.Owlcat)
        Main.OwlcatMods.Add(mod.Identifier);

      return new(mod, true);
    }

    public static void ParseInstalledMods()
    {
      foreach (var mod in ModDatabase.Instance.AllMods)
      {
        if (mod.InstallState == ModDetails.Installed)
          mod.InstallState = ModDetails.NotInstalled;
      }

      var wrath = Main.WrathPath;
      var modDir = wrath.GetDirectories("Mods");
      if (modDir.Length > 0)
      {
        foreach (var maybe in modDir[0].GetDirectories())
        {
          var infoFile = maybe.GetFiles().FirstOrDefault(f => f.Name.Equals("info.json", StringComparison.OrdinalIgnoreCase));
          if (infoFile != null)
          {
            var info = IOTool.Read<UMMModInfo>(infoFile.FullName);

            ModId id = new(info.Id, ModType.UMM);

            if (!ModDatabase.Instance.TryGet(id, out var mod))
            {
              ModDetails details = new();
              details.ModId = id;
              details.Name = info.DisplayName;
              details.Latest = ModVersion.Parse(info.Version);
              details.Source = ModSource.Other;
              details.Author = info.Author;
              details.Description = "";

              mod = new(details);
              ModDatabase.Instance.Add(mod);
            }

            mod.InstallState = ModDetails.Installed;
            mod.Version = ModVersion.Parse(info.Version);
          }
        }
      }
      var OwlcatModDir = new DirectoryInfo(Main.WrathDataDir).GetDirectories("Modifications");
      if (OwlcatModDir.Length > 0)
      {
        foreach (var maybe in OwlcatModDir[0].GetDirectories())
        {
          var infoFile = maybe.GetFiles().FirstOrDefault(f => f.Name.Equals("OwlcatModificationManifest.json", StringComparison.OrdinalIgnoreCase));
          if (infoFile != null)
          {
            var info = IOTool.Read<OwlcatModInfo>(infoFile.FullName);

            ModId id = new(info.UniqueName, ModType.Owlcat);

            if (!ModDatabase.Instance.TryGet(id, out var mod))
            {
              ModDetails details = new();
              details.ModId = id;
              details.Name = info.DisplayName;
              details.Latest = ModVersion.Parse(info.Version);
              details.Source = ModSource.Other;
              details.Author = info.Author;
              details.Description = "";

              mod = new(details);
              ModDatabase.Instance.Add(mod);
            }

            mod.InstallState = ModDetails.Installed;
            mod.Version = ModVersion.Parse(info.Version);
          }
        }
      }
    }
  }

  public class InstallResult
  {
    public readonly InstallState State;
    public readonly string Error;

    public InstallResult(InstallState state)
    {
      State = state;
    }

    public InstallResult(string error)
    {
      State = InstallState.None;
      Error = error;
    }
  }
}
