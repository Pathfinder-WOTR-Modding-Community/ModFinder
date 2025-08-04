using ModFinder.UI;
using ModFinder.Util;
using NexusModsNET;
using NexusModsNET.DataModels;
using SharpCompress.Archives;
using SharpCompress.Common;
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
    private static MainWindow Window;

    public static async Task<InstallResult> Install(ModViewModel viewModel, bool isUpdate)
    {
      switch (viewModel.ModId.Type)
      {
        case ModType.UMM:
          if (!isUpdate && await ModCache.TryRestoreMod(viewModel.ModId))
          {
            return new(InstallState.Installed);
          }

          if (viewModel.CanInstall)
          {
            return await InstallFromRemoteZip(viewModel, isUpdate);
          }

          break;
        case ModType.Owlcat:
          if (!isUpdate && await ModCache.TryRestoreMod(viewModel.ModId))
          {
            return new(InstallState.Installed);
          }

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


      if (!isUpdate && await ModCache.TryRestoreMod(viewModel.ModId))
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

      var file = Path.GetTempFileName();

      string url;
      if (viewModel.Manifest.Service.IsNexus())
      {
        var key = Main.Settings.NexusApiKey;
        if (string.IsNullOrWhiteSpace(key))
        {
          var expectedZipName = $"{viewModel.Manifest.Id.Id}-{viewModel.Latest.Version}.zip";
          //example: https://github.com/Pathfinder-WOTR-Modding-Community/WrathModsMirror/releases/download/BubbleBuffs%2F5.0.0/BubbleBuffs-5.0.0.zip
          url = $"{viewModel.Manifest.Service.Nexus.DownloadMirror}/releases/download/{viewModel.Manifest.Id.Id}%2F{viewModel.Latest.Version}/{expectedZipName}";
        }
        else
        {
          var game = "pathfinderwrathoftherighteous";
          var client = NexusModsFactory.New(key);
          var modInquirer = client.CreateModsInquirer();
          var filesInquirer = client.CreateModFilesInquirer();
          var files = await filesInquirer.GetModFilesAsync(game, viewModel.Manifest.Service.Nexus.ModID);
          var mostRecent = files.ModFiles.Where(f => f.Category == NexusModFileCategory.Main).Last();
          var links = await filesInquirer.GetModFileDownloadLinksAsync(game, viewModel.Manifest.Service.Nexus.ModID, mostRecent.FileId);
          url = links.First().Uri.ToString();
        }
      }
      else
      {
        url = viewModel.Latest.Url;
      }
      Logger.Log.Info($"Fetching zip from {url}");

      await HttpHelper.DownloadFileAsync(url, file);

      return await InstallFromZip(file, viewModel, isUpdate);
    }

    public static string GetModPath(ModType type)
    {
      switch (type)
      {
        case ModType.UMM:
          return Main.UMMInstallPath;
        case ModType.Owlcat:
          return Path.Combine(Main.WrathDataDir, "Modifications");
        case ModType.Portrait:
          return Path.Combine(Main.WrathDataDir, "Portraits");
        default:
          throw new Exception("Unrecognized Mod Type");
      }
    }

    private static ModType GetModTypeFromArchive(IArchive archive)
    {
      //if (archive.Entries.Any(e => e.Key.ToString().Equals("Info.json", StringComparison.CurrentCultureIgnoreCase)))
      if (archive.Entries.FirstOrDefault(e => Path.GetFileName(e.Key).Equals("Info.json", StringComparison.OrdinalIgnoreCase)) != null)
      return ModType.UMM;
      //if (archive.Entries.Any(e => e.Key.ToString().Equals("OwlcatModificationManifest.json", StringComparison.CurrentCultureIgnoreCase)))
      if (archive.Entries.FirstOrDefault(e => Path.GetFileName(e.Key).Equals("OwlcatModificationManifest.json", StringComparison.OrdinalIgnoreCase)) != null)
        return ModType.Owlcat;
      else return ModType.Portrait;
    }

    public const string modFinderPrefix = "ModFinderPortrait_";

    public static async Task<InstallResult> InstallFromZip(
      string path, ModViewModel viewModel = null, bool isUpdate = false)
    {
      /*
      var c = Path.GetExtension(path);
      if (c != ".zip")
      {
        MessageBox.Show(Window, "Provided file is not a file format we currently support (" + c + ")", "Unsupported File", MessageBoxButton.OK);
        return new(InstallState.None);
      }*/

      InstallModManifest info;

      using var stream = File.OpenRead(path);
      using var archive = ArchiveFactory.AutoFactory.Open(stream);
      ModType modType = viewModel == null ? GetModTypeFromArchive(archive) : viewModel.ModId.Type;

      // If the mod is not in the first level folder in the zip we need to reach in and grab it
      string rootInZip = null;

      switch (modType)
      {
        case ModType.UMM:
          {
            var manifestEntry =
              archive.Entries.FirstOrDefault(e => Path.GetFileName(e.Key).Equals("Info.json", StringComparison.OrdinalIgnoreCase));

            if (manifestEntry is null)
            {
              return new("Unable to find manifest.");
            }

            var fullName = manifestEntry.Key;
            var fileName = Path.GetFileName(fullName);

            if (fullName != fileName)
            {
              int root = fullName.Length - fileName.Length;
              rootInZip = fullName[..root];
            }

            var UMMManifest = IOTool.Read<UMMModInfo>(manifestEntry.OpenEntryStream());
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
              archive.Entries.FirstOrDefault(e =>
                Path.GetFileName(e.Key).Equals("OwlcatModificationManifest.json", StringComparison.OrdinalIgnoreCase));

            var fullName = manifestEntry.Key;
            var fileName = Path.GetFileName(fullName);

            if (fullName != fileName)
            {
              int root = fullName.Length - fileName.Length;
              rootInZip = fullName[..root];
            }

            if (manifestEntry is null)
            {
              return new("Unable to find manifest.");
            }

            var OwlcatManifest = IOTool.Read<OwlcatModInfo>(manifestEntry.OpenEntryStream());
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

      // Cache the current version
      if (isUpdate)
      {
        await ModCache.Cache(viewModel);
      }


      var destination = GetModPath(modType);
      //  if (manifestEntry.FullName == manifestEntry.Name) ZIP can have different name from mod ID
      {
        Logger.Log.Verbose($"Creating mod directory. \"{destination}\"");
        // Handle mods without a folder in the zip
        destination = modType == ModType.Portrait ? destination : Path.Combine(destination, info.ModId);
        Logger.Log.Verbose($"Finished creating mod directory. \"{destination}\"");
      }

      static void ExtractInParts(string path, string destination)
      {
        Logger.Log.Verbose("Starting to extract in parts");

        using var stream = File.OpenRead(path);
        using var archive = ArchiveFactory.AutoFactory.Open(stream);

        foreach (var part in archive.Entries)
        {
          if (part.ToString() != "")
          {
            var extPath = Path.Combine(destination, part.ToString());
            try
            {
              part.WriteToFile(extPath, new ExtractionOptions()
              {
                ExtractFullPath = true,
                Overwrite = true
              });
            }
            catch (DirectoryNotFoundException)
            {
              var tempPath = extPath.Replace(part.ToString(), "");
              Directory.CreateDirectory(tempPath);
            }
            catch (Exception ex)
            {
              Logger.Log.Verbose("[Line 275] Destination is - " + destination.ToString());
              Logger.Log.Verbose("[Line 276] File is - " + part.ToString());
              Logger.Log.Verbose("[Line 277] Full path is - " + extPath.ToString());
              Logger.Log.Verbose(ex.ToString());
            }
          }
        }
      }

      //Non-portrait mods just extract to the destination directory
      if (modType != ModType.Portrait)
      {
        try
        {
          await Task.Run(() =>
          {
            using var stream = File.OpenRead(path);
            using var archive = ArchiveFactory.AutoFactory.Open(stream);

            if (rootInZip != null)
            {
              Logger.Log.Verbose("Extracting the archive with root folder in pieces");
              Directory.CreateDirectory(destination);
              if (modType == ModType.Owlcat)
              {
                Directory.CreateDirectory(Path.Combine(destination, "Assemblies"));
                Directory.CreateDirectory(Path.Combine(destination, "Blueprints"));
                Directory.CreateDirectory(Path.Combine(destination, "Bundles"));
                Directory.CreateDirectory(Path.Combine(destination, "Localization"));
              }
              foreach (var entry in archive.Entries)
              {
                string relativepath = Path.GetRelativePath(rootInZip, entry.ToString());
                if (relativepath.Contains(".."))
                  continue;
                string entryDest = Path.Combine(destination, relativepath);
                if (entry.IsDirectory)
                {
                  Directory.CreateDirectory(entryDest);
                }
                else
                {
                  try
                  {
                    entry.WriteToFile(entryDest, new ExtractionOptions()
                    {
                      ExtractFullPath = true,
                      Overwrite = true
                    });
                  }
                  catch (DirectoryNotFoundException)
                  {
                    Directory.CreateDirectory(Path.GetDirectoryName(entryDest));
                    entry.WriteToFile(entryDest, new ExtractionOptions()
                    {
                      ExtractFullPath = true,
                      Overwrite = true
                    });
                  }
                  catch (Exception ex)
                  {
                    Logger.Log.Verbose(ex.ToString());
                  }
                }
              }
            }
            else
            {
              Logger.Log.Verbose(destination);
              Directory.CreateDirectory(destination);
              archive.WriteToDirectory(destination, new ExtractionOptions()
              {
                ExtractFullPath = true,
                Overwrite = true
              });
            }

          });
        }
        catch (IOException ex)
        {
          Logger.Log.Verbose("[Line 311] Destination is - " + destination.ToString());
          Logger.Log.Verbose(ex.ToString());
          ExtractInParts(path, destination);
        }
        catch (Exception ex)
        {
          Logger.Log.Error(ex.ToString());
        }
      }
      else
      {
        var enumeratedFolders = Directory.EnumerateDirectories(Path.Combine(Main.WrathDataDir, "Portraits"));
        var PortraitFolder = Path.Combine(Main.WrathDataDir, "Portraits");
        var tmpFolder = Path.Combine(Environment.GetEnvironmentVariable("TMP"), Guid.NewGuid().ToString());
        try
        {
          archive.ExtractToDirectory(tmpFolder);
        }
        catch
        {
          ExtractInParts(path, tmpFolder);
        }
        if (Directory.EnumerateDirectories(tmpFolder).Count() <= 1)
        {
          tmpFolder = Path.Combine(tmpFolder, "Portraits");
        }

        //var folderToEnumerate = zip.Entries.Count > 1 ? zip.Entries : zip.Entries.FirstOrDefault(a => a.Name == "Portraits");
        foreach (var portraitFolder in Directory.EnumerateDirectories(tmpFolder))
        {
          var builtString = modFinderPrefix + Guid.NewGuid();
          var earMark = new PortraitEarmark(path.Split('\\').Last()); //Put modid here
          while (Directory.Exists(builtString))
          {
            builtString = modFinderPrefix + Guid.NewGuid();
          }
          var newPortraitFolderPath = Path.Combine(PortraitFolder, builtString);
          Directory.Move(portraitFolder, newPortraitFolderPath);
          ModFinder.Util.IOTool.Write(earMark, Path.Combine(newPortraitFolderPath, "Earmark.json"));
        }
        Directory.Delete(tmpFolder);
      }

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