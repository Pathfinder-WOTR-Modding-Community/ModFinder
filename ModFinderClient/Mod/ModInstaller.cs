using ModFinder.UI;
using ModFinder.Util;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace ModFinder.Mod
{
  /// <summary>
  /// Manages mod installation.
  /// </summary>
  public static class ModInstaller
  {
    public static async Task<InstallResult> Install(ModViewModel viewModel, bool isUpdate)
    {
      switch (viewModel.ModId.Type)
      {
        case ModType.UMM:
          if (!isUpdate && ModCache.TryRestoreMod(viewModel.ModId))
          {
            return new(InstallState.Installed);
          }

          if (viewModel.CanInstall)
          {
            return await InstallFromRemoteZip(viewModel, isUpdate);
          }

          break;
        case ModType.Owlcat:
          //cache system needs to be adapted to Owl/Portrait but i dont have knowledge of that code, better if Bubbles or Wolfie does it.
          /*if (!isUpdate && ModCache.TryRestoreMod(viewModel.ModId))
          {
            return new(InstallState.Installed);
          }*/

          if (viewModel.CanInstall)
          {
            return await InstallFromRemoteZip(viewModel, isUpdate);
          }

          break;
        case ModType.Portrait:
          //cache system needs to be adapted to Owl/Portrait but i dont have knowledge of that code, better if Bubbles or Wolfie does it.
          /*
            return new(InstallState.Installed);
          }*/

          if (viewModel.CanInstall)
          {
            return await InstallFromRemoteZip(viewModel, isUpdate);
          }

          break;
      }

      if (viewModel.ModId.Type != ModType.UMM)
      {
        return new($"Currently {viewModel.ModId.Type} mods are not supported.");
      }


      if (!isUpdate && ModCache.TryRestoreMod(viewModel.ModId))
      {
        return new(InstallState.Installed);
      }

      if (viewModel.CanInstall)
      {
        return await InstallFromRemoteZip(viewModel, isUpdate);
      }

      return new("Unknown mod source");
    }

    private static async Task<InstallResult> InstallFromRemoteZip(ModViewModel viewModel, bool isUpdate)
    {
      Logger.Log.Info($"Fetching zip from {viewModel.Latest.Url}");

      var file = Path.GetTempFileName();
      await HttpHelper.DownloadFileAsync(viewModel.Latest.Url, file);

      return await InstallFromZip(file, viewModel, isUpdate);
    }

    public static string GetModpath(ModType type)
    {
      switch (type)
      {
        case ModType.UMM:
          return Main.UMMInstallPath;
        case ModType.Owlcat:
          return Path.Combine(Main.WrathDataDir, "Modifications");
        case ModType.Portrait:
          return Path.Combine(Main.WrathDataDir, "Portraits");
      }

      throw new Exception("Unrecognized Mod Type");
    }

    private static ModType GetModTypeFromZIP(ZipArchive zip)
    {
      if (zip.Entries.Any(e => e.Name.Equals("Info.json"))) return ModType.UMM;
      if (zip.Entries.Any(e => e.Name.Equals("OwlcatModificationManifest.json"))) return ModType.Owlcat;
      else return ModType.Portrait;
    }

    public static async Task<InstallResult> InstallFromZip(
      string path, ModViewModel viewModel = null, bool isUpdate = false)
    {
      using var zip = ZipFile.OpenRead(path);
      InstallModManifest info;
      ModType modType = viewModel == null ? GetModTypeFromZIP(zip) : viewModel.ModId.Type;
      switch (modType)
      {
        case ModType.UMM:
          {
            var manifestEntry =
              zip.Entries.FirstOrDefault(e => e.Name.Equals("Info.json", StringComparison.OrdinalIgnoreCase));

            if (manifestEntry is null)
            {
              return new("Unable to find manifest.");
            }

            var UMMManifest = IOTool.Read<UMMModInfo>(manifestEntry.Open());
            info = new InstallModManifest(UMMManifest.Id, UMMManifest.Version);

            var manifest = viewModel?.Manifest ?? ModManifest.ForLocal(UMMManifest);
            if (!ModDatabase.Instance.TryGet(manifest.Id, out viewModel))
            {
              viewModel = new(manifest);
              ModDatabase.Instance.Add(viewModel);
            }

            break;
          }
        case ModType.Owlcat:
          {
            var manifestEntry =
              zip.Entries.FirstOrDefault(e => e.Name.Equals("OwlcatModificationManifest.json", StringComparison.OrdinalIgnoreCase));

            if (manifestEntry is null)
            {
              return new("Unable to find manifest.");
            }

            var OwlcatManifest = IOTool.Read<OwlcatModInfo>(manifestEntry.Open());
            info = new InstallModManifest(OwlcatManifest.UniqueName, OwlcatManifest.Version);

            var manifest = viewModel?.Manifest ?? ModManifest.ForLocal(OwlcatManifest);
            if (!ModDatabase.Instance.TryGet(manifest.Id, out viewModel))
            {
              viewModel = new(manifest);
              ModDatabase.Instance.Add(viewModel);
            }
            Main.OwlcatMods.Add(OwlcatManifest.UniqueName);

            break;
          }
        case ModType.Portrait:
          {
            var name = Guid.NewGuid().ToString();
            info = new InstallModManifest(name, null);
            break;
          }
        default:
          {
            throw new Exception("Unable to determine mod type or invalid modtype");
          }
      }


      if (viewModel is not null && viewModel.ModId.Id != info.ModId)
      {
        return new($"ModId mismatch. Found {viewModel.ModId.Id} but expected {info.ModId}");
      }

      // Remove and cache the current version
      if (isUpdate)
      {
        ModCache.Uninstall(viewModel);
      }

      var destination = GetModpath(modType);
      //Gotta check to make sure ...\Owlcat Games\Pathfinder Wrath Of The Righteous\Modifications exists
      //  if (manifestEntry.FullName == manifestEntry.Name) ZIP can have different name from mod ID
      {
        Logger.Log.Verbose($"Creating mod directory. \"{destination}\"");
        // Handle mods without a folder in the zipm
        destination = modType == ModType.Portrait ? destination : Path.Combine(destination, info.ModId);
      }
      
      await Task.Run(() => zip.ExtractToDirectory(destination, true));

      if (viewModel != null)
      {
        viewModel.InstalledVersion = ModVersion.Parse(info.Version);
        viewModel.InstallState = InstallState.Installed;
      }

      Logger.Log.Info($"{viewModel?.Name} successfully installed with version {viewModel?.InstalledVersion}.");
      return new(InstallState.Installed);
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