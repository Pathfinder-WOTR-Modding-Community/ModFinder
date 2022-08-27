using ModFinder.Mod;
using ModFinder.Util;
using System;
using System.IO;
using System.Text.RegularExpressions;

namespace ModFinder
{
  public static class Main
  {
    /// <summary>
    /// Modfinder settings
    /// </summary>
    public static Settings Settings => m_Settings ??= Settings.Load();
    private static Settings m_Settings;

    /// <summary>
    /// Owlcat mods, use to toggle enabled status
    /// </summary>
    public static readonly OwlcatModificationSettingsManager OwlcatMods = new();

    /// <summary>
    /// Folder where we put modfinder stuff
    /// </summary>
    public static string AppFolder
    {
      get
      {
        if (!Directory.Exists(_appFolder))
        {
          _ = Directory.CreateDirectory(_appFolder);
        }
        return _appFolder;
      }
    }
    private static readonly string _appFolder =
      Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Modfinder");

    /// <summary>
    /// Get path to file in modfinder app folder
    /// </summary>
    /// <param name="file">file to get path to</param>
    public static string AppPath(string file) => Path.Combine(AppFolder, file);

    /// <summary>
    /// Try to read a file in the modfinder app folder
    /// </summary>
    /// <param name="file">file name</param>
    /// <param name="contents">contents of the file will be put here</param>
    /// <returns>true if the file exists and was read successfully</returns>
    public static bool TryReadFile(string file, out string contents)
    {
      var path = AppPath(file);
      if (File.Exists(path))
      {
        contents = File.ReadAllText(path);
        return true;
      }
      else
      {
        contents = null;
        return false;
      }
    }

    /// <summary>
    /// %AppData%/..
    /// </summary>
    public static string AppDataRoot => Directory.GetParent(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)).FullName;

    /// <summary>
    /// Wrath data directory, i.e. "%AppData%/../LocalLow/Owlcat Games/Pathfinder Wrath Of The Righteous"
    /// </summary>
    public static string WrathDataDir => Path.Combine(AppDataRoot, "LocalLow", "Owlcat Games", "Pathfinder Wrath Of The Righteous");

    //Maybe change this to read registry and look for path?
    /// <summary>
    /// Path to the installed wrath folder
    /// </summary>
    public static DirectoryInfo WrathPath
    {
      get
      {
        if (_WrathPath != null) return _WrathPath;

        if (Directory.Exists(Settings.WrathPath) && File.Exists(Path.Combine(Settings.WrathPath, "Wrath.exe")))
        {
          _WrathPath = new(Settings.WrathPath);
        }
        else if (Directory.Exists(Settings.AutoWrathPath) && File.Exists(Path.Combine(Settings.AutoWrathPath, "Wrath.exe")))
        {
          _WrathPath = new(Settings.AutoWrathPath);
        }
        else
        {
          var log = Path.Combine(WrathDataDir, "Player.log");
          if (!File.Exists(log))
            throw new Exception("Unable to find Wrath Installation path, please launch the game once before starting the mod manager.");

          var temp = Path.Combine(Path.GetTempPath(), "modfinder_log_" + Path.GetRandomFileName());
          File.Copy(log, temp, true);
          using var sr = new StreamReader(File.OpenRead(log));

          var firstline = sr.ReadLine();
          var extractPath = new Regex(".*?'(.*)'");
          _WrathPath = new(extractPath.Match(firstline).Groups[1].Value);
          _WrathPath = _WrathPath.Parent.Parent;
          Settings.AutoWrathPath = WrathPath.FullName;
          Settings.Save();

          File.Delete(temp);
        }

        return _WrathPath;
      }
    }

    private static DirectoryInfo _WrathPath;
  }
}
