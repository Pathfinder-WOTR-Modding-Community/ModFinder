using Microsoft.Win32;
using ModFinder.Mod;
using ModFinder.Util;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;

namespace ModFinder
{
  public static class Main
  {
    /// <summary>
    /// Modfinder version
    /// </summary>
    public static string ProductVersion = FileVersionInfo.GetVersionInfo(Assembly.GetEntryAssembly().Location).ProductVersion;
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

    /// <summary>
    /// Prompts user to manually input path to wrath
    /// </summary>
    public static void GetWrathPathManual()
    {
      Logger.Log.Info($"WrathPath not found, prompting user.");
      var dialog = new OpenFileDialog
      {
        FileName = "Wrath",
        DefaultExt = ".exe",
        Filter = "Executable (.exe)|*.exe",
        Title = "Select Wrath.exe (in the installation directory)"
      };

      var result = dialog.ShowDialog();
      if (result is not null && result.Value)
      {
        _WrathPath = new(Path.GetDirectoryName(dialog.FileName));
      }
      else
      {
        Logger.Log.Error("Unable to find Wrath installation path.");
      }
    }
    private static Regex extractGameVersion = new(".*?Found game version string: '(.*)'.*");
    public static ModVersion GameVersion;

    private static ModVersion GameVersionRaw
    {
      get
      {
        if (WrathPath == null) { return new(); }

        var log = Path.Combine(WrathDataDir, "Player.log");
        if (!File.Exists(log))
        {
          return new();
        }

        try
        {
          using var sr = new StreamReader(File.OpenRead(log));

          for (int i = 0; i < 200; i++)
          {
            var line = sr.ReadLine();
            if (line is null) return new();

            var match = extractGameVersion.Match(line);
            if (match.Success)
            {
              Logger.Log.Info($"Found game version: {match.Groups[1].Value}");
              return ModVersion.Parse(match.Groups[1].Value);
            }

          }
        }
        catch (Exception e)
        {
          Logger.Log.Error("Unable to find Wrath game version", e);
        }

        return new();
      }
    }

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
          Logger.Log.Info($"Using WrathPath from settings: {Settings.WrathPath}");
          _WrathPath = new(Settings.WrathPath);
        }
        else if (Directory.Exists(Settings.AutoWrathPath) && File.Exists(Path.Combine(Settings.AutoWrathPath, "Wrath.exe")))
        {
          Logger.Log.Info($"Using auto WrathPath from settings: {Settings.AutoWrathPath}");
          _WrathPath = new(Settings.AutoWrathPath);
        }
        else
        {
          var log = Path.Combine(WrathDataDir, "Player.log");
          if (!File.Exists(log))
          {
            GetWrathPathManual();
          }
          else
          {
            try
            {
              Logger.Log.Info($"Getting WrathPath from UMM log.");

              using var sr = new StreamReader(File.OpenRead(log));
              var firstline = sr.ReadLine();

              var extractPath = new Regex(".*?'(.*)'");
              _WrathPath = new(extractPath.Match(firstline).Groups[1].Value);
              _WrathPath = _WrathPath.Parent.Parent;
            }
            catch (Exception e)
            {
              Logger.Log.Error("Unable to find Wrath installation path, Prompting manual input.", e);
              GetWrathPathManual();
            }
          }

          Settings.AutoWrathPath = WrathPath.FullName;
          Settings.Save();
        }

        GameVersion = GameVersionRaw;
        return _WrathPath;
      }
    }

    public static readonly string UMMParamsPath =
      Path.Combine(Main.WrathPath.FullName, @"Wrath_Data\Managed\UnityModManager\Params.xml");

    public static readonly string UMMInstallPath = Path.Combine(WrathPath.FullName, "Mods");

    private static DirectoryInfo _WrathPath; 
  }
}
