using System;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using System.IO.Compression;
using ModFinder.Util;
using ModFinder.UI;
using System.Net.Http;

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
      var repoUri = new Uri(viewModel.Source.GitHub.RepoUrl);
      var releasesUri = new Uri(repoUri, "releases/latest");

      // First get the download link
      var client = new HttpClient();
      var releases = client.PostAsync(releasesUri, content: null);
      //WebClient web = new();
      //  await web.DownloadFileTaskAsync(mod.DownloadLink, file);

      return await InstallFromZip(null, null);// file, mod.ModId);
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
      viewModel.Version = ModVersion.Parse(info.Version);
      viewModel.InstallState = InstallState.Installed;
      return new(InstallState.Installed);
    }

    public static void CheckInstalledMods()
    {
      var modDir = Main.WrathPath.GetDirectories("Mods");
      if (modDir.Length > 0)
      {
        foreach (var modFiles in modDir[0].GetDirectories())
        {
          var infoFile =
            modFiles.GetFiles().FirstOrDefault(f => f.Name.Equals("info.json", StringComparison.OrdinalIgnoreCase));
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

            mod.InstallState = InstallState.Installed;
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
