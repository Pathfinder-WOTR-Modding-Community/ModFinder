using System;
using System.Threading.Tasks;
using ModFinder;
using ModFinder.Infrastructure;
using Octokit;
using System.Collections.Generic;
using System.Linq;
using NexusModsNET;
using ManifestUpdater.Properties;
using System.IO;

var github = new GitHubClient(new ProductHeaderValue("ModFinder"));
var token = Environment.GetEnvironmentVariable("GITHUB_TOKEN");

github.Credentials = new Credentials(token);

var nexus = NexusModsClient.Create(Environment.GetEnvironmentVariable("NEXUS_APITOKEN"), "Modfinder", "0");

var contents = Resources.internal_manifest;

var details = ModFinderIO.FromString<ModListBlob>(Resources.internal_manifest);
var tasks = new List<Task<ModDetailsInternal>>();

foreach (var mod in details.m_AllMods)
{
    tasks.Add(Task.Run(async () =>
    {

        if (mod.Source == ModSource.GitHub)
        {
            var repo = await github.Repository.Get(mod.GithubOwner, mod.GithubRepo);
            var allReleases = await github.Repository.Release.GetAll(mod.GithubOwner, mod.GithubRepo);
            var latestRelease = await github.Repository.Release.GetLatest(mod.GithubOwner, mod.GithubRepo);
            if (latestRelease.Assets.Count == 0)
                return null;
            var todownload = latestRelease.Assets[0];

            mod.Changelog = new();
            foreach (var release in allReleases)
            {
                var version = ModVersion.Parse(release.TagName);
                mod.Changelog.Add((version, release.Body.Replace("\r\n", "\n")));
            }
            mod.Changelog.Sort((a, b) => a.version.CompareTo(b.version));

            mod.DownloadLink = todownload.BrowserDownloadUrl;
            mod.Description = repo.Description;
            mod.Latest = ModVersion.Parse(latestRelease.TagName); //This is not true???
                    return mod;
        }
        else if (mod.Source == ModSource.Nexus)
        {
            var nexusFactory = NexusModsFactory.New(nexus);
            var nexusmod = await nexusFactory.CreateModsInquirer().GetMod("pathfinderwrathoftherighteous", mod.NexusModID);
            var changelog = await nexusFactory.CreateModsInquirer().GetModChangelogs("pathfinderwrathoftherighteous", mod.NexusModID);
            var modde = await nexusFactory.CreateModFilesInquirer().GetModFilesAsync("pathfinderwrathoftherighteous", mod.NexusModID);

            var release = modde.ModFiles.Last();

            if (changelog != null)
            {
                mod.Changelog = new();
                foreach (var entry in changelog)
                {
                    var version = ModVersion.Parse(entry.Key);
                    mod.Changelog.Add((version, string.Join("\n", entry.Value)));
                }

                mod.Changelog.Sort((a, b) => a.version.CompareTo(b.version));
            }

            mod.Description = nexusmod.Description;
            mod.Latest = ModVersion.Parse(nexusmod.Version); //This is not true???
                    mod.DownloadLink = @"https://www.nexusmods.com/pathfinderwrathoftherighteous/mods/" + mod.NexusModID + @"?tab=files&file_id=" + release.FileId;

            return mod;
        }
        return null;
    }));
}

//We don't want to do console printing from inside the async tasks as they will interleave the output and it is confusing
//So collect the results here and print them out
//This will also wait for all tasks to complete
foreach (var mod in tasks.Select(t => t.Result).Where(r => r != null))
{
    Console.WriteLine();
    Log(mod.Name);
    LogObj("  UniqueId: ", $"{mod.ModId.Identifier}_{mod.ModId.ModType}");
    LogObj("  Download: ", mod.DownloadLink);
    LogObj("  Latest: ", mod.Latest);
}

var targetUser = "BarleyFlour";
var targetRepo = "Modfinder";
var targetFile = "ManifestUpdater/Resources/master_manifest.json";

var serializedDeets = ModFinderIO.Write(details);
var currentFile = await github.Repository.Content.GetAllContentsByRef(targetUser, targetRepo, targetFile, "master");
var updateFile = new UpdateFileRequest("Update the mod manifest (bot)", serializedDeets, currentFile[0].Sha, "master", true);
var newblob = new NewBlob();
newblob.Content = serializedDeets;
var blob = await github.Git.Blob.Create("BarleyFlour", "ModFinder",newblob);
if(blob.Sha != updateFile.Sha)
{
    var result = await github.Repository.Content.UpdateFile(targetUser, targetRepo, targetFile, updateFile);
    LogObj("Updated: ", result.Commit.Sha);
}
else
{
    LogObj("No Update: ", "Matching SHA's");
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
