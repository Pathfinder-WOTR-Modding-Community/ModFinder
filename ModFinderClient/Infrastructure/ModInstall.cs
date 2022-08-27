using System;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using System.Net;
using System.Diagnostics;
using System.IO.Compression;

namespace ModFinder.Infrastructure
{
    public class ModInstall
    {
        public static async Task<InstallResult> InstallMod(ModDetails toInstall)
        {
            if(ModCaching.CachedMods.Any(a => a.ModIdentifier == toInstall.Identifier))
            {
                ModCaching.RestoreMod(toInstall);
            }
            if (toInstall.Source == ModSource.Nexus)
            {
                Process.Start("explorer", '"' + toInstall.DownloadLink + '"').Dispose();
                return new(toInstall, false);
            }

            if (toInstall.Source == ModSource.GitHub)
            {
                return await InstallFromRemoteZip(toInstall);
            }

            return new("Unknown mod source");
        }

        internal static async Task<InstallResult> InstallFromRemoteZip(ModDetails mod)
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

            ModDetailsInternal newMod = new();

            if (asUmm != null)
            {
                destination = Path.Combine(Main.WrathPath.FullName, "Mods");

                var info = ModFinderIO.Read<UMMModInfo>(asUmm.Open());

                newMod.ModId = new()
                {
                    Identifier = info.Id,
                    ModType = ModType.UMM,
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

                var info = ModFinderIO.Read<OwlcatModInfo>(asOwl.Open());

                newMod.ModId = new()
                {
                    Identifier = info.UniqueName,
                    ModType = ModType.Owlcat,
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

            mod.InstalledVersion = newMod.Latest;

            await Task.Run(() => zip.ExtractToDirectory(destination, true));

            if (mod.ModId.ModType == ModType.Owlcat)
                Main.OwlcatMods.Add(mod.Identifier);

            return new(mod, true);
        }

        public static void ParseInstalledMods()
        {
            foreach (var mod in ModDatabase.Instance.AllMods)
            {
                if (mod.State == ModState.Installed)
                    mod.State = ModState.NotInstalled;
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
                        var info = ModFinderIO.Read<UMMModInfo>(infoFile.FullName);

                        ModId id = new(info.Id, ModType.UMM);

                        if (!ModDatabase.Instance.TryGet(id, out var mod))
                        {
                            ModDetailsInternal details = new();
                            details.ModId = id;
                            details.Name = info.DisplayName;
                            details.Latest = ModVersion.Parse(info.Version);
                            details.Source = ModSource.Other;
                            details.Author = info.Author;
                            details.Description = "";

                            mod = new(details);
                            ModDatabase.Instance.Add(mod);
                        }

                        mod.State = ModState.Installed;
                        mod.InstalledVersion = ModVersion.Parse(info.Version);
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
                        var info = ModFinderIO.Read<OwlcatModInfo>(infoFile.FullName);

                        ModId id = new(info.UniqueName, ModType.Owlcat);

                        if (!ModDatabase.Instance.TryGet(id, out var mod))
                        {
                            ModDetailsInternal details = new();
                            details.ModId = id;
                            details.Name = info.DisplayName;
                            details.Latest = ModVersion.Parse(info.Version);
                            details.Source = ModSource.Other;
                            details.Author = info.Author;
                            details.Description = "";

                            mod = new(details);
                            ModDatabase.Instance.Add(mod);
                        }

                        mod.State = ModState.Installed;
                        mod.InstalledVersion = ModVersion.Parse(info.Version);
                    }
                }
            }
        }
    }


    public class InstallResult
    {
        public ModDetails Mod;
        public bool Complete = true;
        public string Error;

        public InstallResult(ModDetails mod, bool complete) { Mod = mod; Complete = complete; }

        public InstallResult(string error)
        {
            Complete = false;
            Error = error;
        }
    }
}
