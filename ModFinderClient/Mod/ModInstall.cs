using System;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using System.IO.Compression;
using ModFinder.Util;
using ModFinder.UI;
using System.Net;
using Newtonsoft.Json;
using System.Net.Cache;

namespace ModFinder.Mod
{
  /// <summary>
  /// Manages mod installation.
  /// </summary>
  public static class ModInstaller
  {
    public static readonly string UMMInstallPath = Path.Combine(Main.WrathPath.FullName, "Mods");

    public static async Task<InstallResult> Install(ModViewModel viewModel)
    {
      if (viewModel.ModId.Type != ModType.UMM)
      {
        return new($"Currently {viewModel.ModId.Type} mods are not supported.");
      }

      if (ModCache.TryRestoreMod(viewModel.ModId))
      {
        return new(InstallState.Installed);
      }

      if (viewModel.Source.GitHub is not null)
      {
        return await InstallFromRemoteZip(viewModel);
      }

      return new("Unknown mod source");
    }

    private static async Task<InstallResult> InstallFromRemoteZip(ModViewModel viewModel)
    {
      WebClient web = new();
      var file = Path.GetTempFileName();
      await web.DownloadFileTaskAsync(viewModel.LatestVersion.DownloadUrl, file);

      return await InstallFromZip(file, viewModel);
    }

    public static async Task<InstallResult> InstallFromZip(string path, ModViewModel viewModel = null)
    {
      using var zip = ZipFile.OpenRead(path);
      var manifestEntry =
        zip.Entries.FirstOrDefault(e => e.Name.Equals("Info.json", StringComparison.OrdinalIgnoreCase));

      if (manifestEntry is null)
      {
        return new("Unable to find manifest.");
      }

      var info = IOTool.Read<UMMModInfo>(manifestEntry.Open());
      if (viewModel is not null && viewModel.ModId.Id != info.Id)
      {
        return new($"ModId mismatch. Found {viewModel.ModId.Id} but expected {info.Id}");
      }

      var destination = UMMInstallPath;
      if (manifestEntry.FullName == manifestEntry.Name)
      {
        // Handle mods without a folder in the zip
        destination = Path.Combine(destination, info.Id);
      }

      await Task.Run(() => zip.ExtractToDirectory(destination, true));

      var manifest = viewModel?.Manifest ?? ModManifest.FromLocalMod(info);
      ModDetails mod = new(manifest);
      if (!ModDatabase.Instance.TryGet(mod.Manifest.Id, out viewModel))
      {
        viewModel = new(mod);
        ModDatabase.Instance.Add(viewModel);
      }
      viewModel.InstalledVersion = ModVersion.Parse(info.Version);
      viewModel.InstallState = InstallState.Installed;
      return new(InstallState.Installed);
    }

    public static void CheckInstalledMods()
    {
      IOTool.Safe(() => CheckInstalledModsInternal());
    }

    private static void CheckInstalledModsInternal()
    {
      foreach (var mod in ModDatabase.Instance.AllMods)
      {
        // Reset install state to make sure we capture any that were, for example, uninstalled but not updated.
        mod.InstallState = InstallState.None;
        mod.InstalledVersion = default;
      }

      var modDir = Main.WrathPath.GetDirectories("Mods");
      if (modDir.Length > 0)
      {
        foreach (var dir in modDir[0].GetDirectories())
        {
          var infoFile =
            dir.GetFiles().FirstOrDefault(f => f.Name.Equals("info.json", StringComparison.OrdinalIgnoreCase));
          if (infoFile != null)
          {
            var info = IOTool.Read<UMMModInfo>(infoFile.FullName);

            var manifest = ModManifest.FromLocalMod(info);
            if (!ModDatabase.Instance.TryGet(manifest.Id, out var mod))
            {
              ModDetails details = new(manifest);
              mod = new(details);
              ModDatabase.Instance.Add(mod);
            }

            mod.ModDir = dir;
            mod.InstallState = InstallState.Installed;
            mod.InstalledVersion = ModVersion.Parse(info.Version);
          }
        }
      }
    }

    public static void CheckForUpdates()
    {
      foreach (var mod in ModDatabase.Instance.AllMods)
      {
        var modFinderInfoUrl = mod.Source?.GitHub?.ModFinderInfoUrl;
        if (!string.IsNullOrEmpty(modFinderInfoUrl))
        {
          using var client = new WebClient();
          mod.ModFinderInfo = JsonConvert.DeserializeObject<ModFinderInfo>(client.DownloadString(modFinderInfoUrl));
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
