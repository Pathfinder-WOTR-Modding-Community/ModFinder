using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModFinder.Infrastructure
{
    public class ModCache
    {
        public DirectoryInfo CachedModDir;
        public string ModIdentifier;
        public ModCache()
        {

        }
        public ModCache(DirectoryInfo folder, string ModIdentifier)
        {
            this.CachedModDir = folder;
            this.ModIdentifier = ModIdentifier;
        }
    }
    public static class ModCaching
    {
        public static void RestoreMod(ModDetails details)
        {
            var cachedmod = CachedMods.First(a => a.ModIdentifier == details.Identifier);
            if(details.ModType == ModType.Owlcat)
            {
                FileSystem.CopyDirectory(cachedmod.CachedModDir.FullName,Path.Combine(Main.WrathDataDir, "Modifications", cachedmod.CachedModDir.Name));
                cachedmod.CachedModDir.Delete(true);
                CachedMods.Remove(cachedmod);
            }
            else if(details.ModType == ModType.UMM)
            {
                FileSystem.CopyDirectory(cachedmod.CachedModDir.FullName, Path.Combine(Main.WrathPath.FullName, "Mods", cachedmod.CachedModDir.Name));
                cachedmod.CachedModDir.Delete(true);
                CachedMods.Remove(cachedmod);
            }
        }
        public static List<ModCache> GetModCaches()
        {
            var result = new List<ModCache>();
            foreach (var cachefolder in new DirectoryInfo(Path.Combine(Main.AppFolder, "CachedMods")).EnumerateDirectories())
            {
                string modid = "";
                foreach (var subfile in cachefolder.EnumerateFiles())
                {
                    if (subfile.Name.Equals("OwlcatModificationManifest.json", StringComparison.OrdinalIgnoreCase))
                    {
                        var manifest = Newtonsoft.Json.JsonConvert.DeserializeObject<OwlcatModInfo>(subfile.OpenText().ReadToEnd());
                        modid = manifest.UniqueName;
                    }
                    else if (subfile.Name.Equals("Info.json", StringComparison.OrdinalIgnoreCase))
                    {
                        var manifest = Newtonsoft.Json.JsonConvert.DeserializeObject<UMMModInfo>(subfile.OpenText().ReadToEnd());
                        modid = manifest.Id;
                    }
                }
                if (modid == "") throw new Exception("Failed to find mod ID of cached mod folder");
                var modcache = new ModCache(cachefolder, modid);
                result.Add(modcache);
            }
            return result;
        }
        public static List<ModCache> CachedMods
        {
            get
            {
                if (CachedMods == null)
                {
                    CachedMods = GetModCaches();
                }
                return CachedMods;
            }
            set { CachedMods = value; }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="mod"> "ModDetails of the mod to be Cached/Uninstalled" </param>
        /// <param name="ModFolder">"Folder to be removed (Folder that contains your info.json, assemblies, etc...)"</param>
        public static void UninstallAndCache(ModDetails mod, DirectoryInfo ModFolder)
        {
            if (mod.ModType == ModType.Owlcat)
            {
                Main.OwlcatMods.Remove(mod.Identifier);
            }
            if (!Directory.Exists(Path.Combine(Main.AppFolder, ModFolder.Name)))
            {
                FileSystem.CopyDirectory(ModFolder.FullName, Path.Combine(Main.AppFolder, "CachedMods", ModFolder.Name));
                Directory.Delete(ModFolder.FullName, true);
                CachedMods.Add(new ModCache(new DirectoryInfo(Path.Combine(Main.AppFolder, "CachedMods", ModFolder.Name)),mod.Identifier));
            }
        }
    }
}
