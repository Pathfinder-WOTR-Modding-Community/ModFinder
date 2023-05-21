using ManifestUpdater.Properties;
using ModFinder.Mod;
using ModFinder.Util;
using NexusModsNET;
using NexusModsNET.DataModels;
using Octokit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ProductHeaderValue = Octokit.ProductHeaderValue;
using Release = ModFinder.Mod.Release;

bool doUpload = true;

#if DEBUGTEST
doUpload = false;
#endif


var github = new GitHubClient(new ProductHeaderValue("ModFinder"));
var token = Environment.GetEnvironmentVariable("GITHUB_TOKEN");

github.Credentials = new Credentials(token);

var nexus = NexusModsClient.Create(Environment.GetEnvironmentVariable("NEXUS_APITOKEN"), "Modfinder_WOTR", "0");

var internalManifest = IOTool.FromString<List<ModManifest>>(Resources.internal_manifest);
var generatedManifest = IOTool.FromString<List<ModManifest>>(Resources.Generated_Manifest);
var tasks = new List<Task<ModManifest>>();

var updatedManifest = new List<ModManifest>();
foreach (var manifest in internalManifest)
{
  var lastChecked = DateTime.Now;
  tasks.Add(Task.Run(async () =>
  {
    var oldManifest =
      generatedManifest.FirstOrDefault(
        a => string.Equals(a.Id.Id, manifest.Id.Id, StringComparison.InvariantCultureIgnoreCase));
    try
    {
      if (manifest.Service.IsGitHub())
      {
        var repoInfo = manifest.Service.GitHub;
        var repo = await github.Repository.Get(repoInfo.Owner, repoInfo.RepoName);
        var releases = await github.Repository.Release.GetAll(repoInfo.Owner, repoInfo.RepoName);
        Octokit.Release latest = null;
        try
        {
          latest = await github.Repository.Release.GetLatest(repoInfo.Owner, repoInfo.RepoName);
        }
        catch (NotFoundException)
        {
          latest = releases.OrderBy(x => ModVersion.Parse(x.TagName)).LastOrDefault();
        }

        if (latest?.Assets?.Count == 0)
        {
          Console.WriteLine($"No assets for mod: {manifest.Name}");
          return null;
        }

        Readme readme = null;
        try
        {
          readme = await github.Repository.Content.GetReadme(repoInfo.Owner, repoInfo.RepoName);
        }
        catch (NotFoundException)
        {
          //fall back to description later if readme is null
        }

        var releaseAsset = latest.Assets[0];
        if (!string.IsNullOrEmpty(repoInfo.ReleaseFilter))
        {
          Regex filter = new Regex(repoInfo.ReleaseFilter, RegexOptions.Compiled);
          foreach (var asset in latest.Assets)
          {
            if (filter.IsMatch(asset.Name))
            {
              releaseAsset = asset;
              break;
            }
          }
        }

        var latestRelease = new Release(ModVersion.Parse(latest.TagName), releaseAsset.BrowserDownloadUrl);
        var releaseHistory = new List<Release>();
        foreach (var release in releases)
        {
          releaseHistory.Add(
            new Release(ModVersion.Parse(release.TagName), url: null, release.Body.Replace("\r\n", "\n")));
        }
        releaseHistory.Sort((a, b) => b.Version.CompareTo(a.Version));

        var lastUpdated = releaseAsset.UpdatedAt.DateTime;
        var newManifest =
          new ModManifest(
            manifest,
            new VersionInfo(latestRelease, lastUpdated, releaseHistory.Take(10).ToList()),
            lastChecked,
            readme?.Content ?? repo.Description);
        updatedManifest.Add(newManifest);
        return newManifest;
      }

      if (manifest.Service.IsNexus())
      {
        var modID = manifest.Service.Nexus.ModID;
        var nexusFactory = NexusModsFactory.New(nexus);
        var nexusMod = await nexusFactory.CreateModsInquirer().GetMod("pathfinderwrathoftherighteous", modID);
        var changelog =
          await nexusFactory.CreateModsInquirer().GetModChangelogs("pathfinderwrathoftherighteous", modID);
        var mod = await nexusFactory.CreateModFilesInquirer().GetModFilesAsync("pathfinderwrathoftherighteous", modID);

        var latestVersion = ModVersion.Parse(nexusMod.Version);
        var latestFileID = mod.ModFiles.Where(f => f.Category == NexusModFileCategory.Main).Last().FileId;
        var downloadUrl =
          @"https://www.nexusmods.com/pathfinderwrathoftherighteous/mods/" + modID + @"?tab=files&file_id=" + latestFileID;

        var releaseHistory = new List<Release>();
        if (changelog != null)
        {
          foreach (var entry in changelog)
          {
            releaseHistory.Add(new Release(ModVersion.Parse(entry.Key), url: null, string.Join("\n", entry.Value)));
          }

          releaseHistory.Sort((a, b) => b.Version.CompareTo(a.Version));
        }

        var latestRelease = new Release(latestVersion, downloadUrl);

        var lastUpdated = nexusMod.UpdatedTime.DateTime;
        var newManifest =
          new ModManifest(
            manifest,
            new VersionInfo(latestRelease, lastUpdated, releaseHistory.Take(10).ToList()),
            lastChecked,
            nexusMod.Description);
        updatedManifest.Add(newManifest);
        return newManifest;
      }
    }
    catch (Exception e)
    {
      Log(e.ToString());
      Log($"Failed to update {manifest.Id}, Using old data.");
      if (oldManifest != null)
      {
        updatedManifest.Add(oldManifest);
        return oldManifest;
      }
    }

    return null;
  }));
}

// We don't want to do console printing from inside the async tasks as they will interleave the output and it is
// confusing. Collect the results here and print them out. This will also wait for all tasks to complete.
foreach (var manifest in tasks.Select(t => t.Result).Where(r => r != null))
{
  Console.WriteLine();
  Log(manifest.Name);
  LogObj("  UniqueId: ", $"{manifest.Id}");
  LogObj("  Download: ", manifest.Version.Latest.Url);
  LogObj("  Latest: ", manifest.Version.Latest.Version);
}

var targetUser = "Pathfinder-WOTR-Modding-Community";
var targetRepo = "ModFinder";
var targetFile = "ManifestUpdater/Resources/generated_manifest.json";

updatedManifest.Sort((a, b) => a.Name.CompareTo(b.Name));

var serializedManifest = IOTool.Write(updatedManifest);

if (doUpload)
{
  var currentFile = await github.Repository.Content.GetAllContentsByRef(targetUser, targetRepo, targetFile, "main");
  var updateFile =
    new UpdateFileRequest("Update the mod manifest (bot)", serializedManifest, currentFile[0].Sha, "main", true);
  var newblob = new NewBlob();
  newblob.Content = serializedManifest;
  var blob = await github.Git.Blob.Create("Pathfinder-WOTR-Modding-Community", "ModFinder", newblob);
  if (blob.Sha != updateFile.Sha)
  {
    var result = await github.Repository.Content.UpdateFile(targetUser, targetRepo, targetFile, updateFile);
    LogObj("Updated: ", result.Commit.Sha);
  }
  else
  {
    LogObj("No Update: ", "Matching SHA's");
  }
}
else
{
  File.WriteAllText(@"C:\Users\worce\source\repos\ModFinder_Shared\ModFinderClient\test_generated.json", serializedManifest);
}

void Log(string str)
{
  Console.Write("[ModFinder]  ");
  Console.WriteLine(str);
}

void LogObj(string key, object value)
{
  Console.Write("[ModFinder]  ");
  Console.Write(key.PadRight(16));
  Console.WriteLine(value ?? "<null>");
}
