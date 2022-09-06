using ModFinder.UI;
using ModFinder.Util;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;

namespace ModFinder.Mod
{
  /// <summary>
  /// Manages mod installation.
  /// </summary>
  public static class ModInstaller
  {
    public static async Task<InstallResult> Install(ModViewModel viewModel, bool isUpdate)
    {
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

    public static async Task<InstallResult> InstallFromZip(
      string path, ModViewModel viewModel = null, bool isUpdate = false)
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

      // Remove and cache the current version
      if (isUpdate)
      {
        ModCache.Uninstall(viewModel);
      }

      var destination = Main.UMMInstallPath;
      if (manifestEntry.FullName == manifestEntry.Name)
      {
        Logger.Log.Verbose($"Creating mod directory.");
        // Handle mods without a folder in the zip
        destination = Path.Combine(destination, info.Id);
      }

      await Task.Run(() => zip.ExtractToDirectory(destination, true));

      var manifest = viewModel?.Manifest ?? ModManifest.ForLocal(info);
      if (!ModDatabase.Instance.TryGet(manifest.Id, out viewModel))
      {
        viewModel = new(manifest);
        ModDatabase.Instance.Add(viewModel);
      }
      viewModel.InstalledVersion = ModVersion.Parse(info.Version);
      viewModel.InstallState = InstallState.Installed;

      Logger.Log.Info($"{viewModel.Name} successfully installed with version {viewModel.InstalledVersion}.");
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