using ModFinder.Mod;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;

namespace ModFinder.UI
{
  /// <summary>
  /// View model tracking the current mod state.
  /// </summary>
  public class ModViewModel : INotifyPropertyChanged
  {
    private static readonly Regex stripHtml = new(@"<.*?>");

    private readonly ModStatus Status = new();
    private readonly List<(ModId id, ModVersion version)> RequiredMods = new();

    public ModManifest Manifest;
    public Release Latest => Manifest.Version.Latest;
    public string HomepageUrl => Manifest.HomepageUrl;

    public event PropertyChangedEventHandler PropertyChanged;

    public ModViewModel(ModManifest manifest)
    {
      Refresh(manifest, refreshUI: false);
    }

    public void Refresh(ModManifest manifest, bool refreshUI = true)
    {
      Manifest = manifest;

      if (refreshUI)
        NotifyAll();
    }

    /** Start Properties referenced by views requiring notification. */

    public string Name => Manifest.Name;
    public string Author => Manifest.Author;
    public string Description => Manifest.Description ?? "-";

    public string Service => GetSourceText();
    public bool IsInstalled => Status.Installed();
    public bool CanInstallOrDownload => CanInstall || CanDownload;
    public bool CanInstall =>
      Manifest.Service.IsGitHub()
      && (!IsInstalled || Status.IsVersionBehind(Latest.Version))
      && !string.IsNullOrEmpty(Latest.Url);
    public bool CanUninstall => IsInstalled && ModDir != null;
    public bool CanDownload =>
      Manifest.Service.IsNexus()
      && (!IsInstalled || Status.IsVersionBehind(Latest.Version))
      && !string.IsNullOrEmpty(Latest.Url);

    public Visibility UninstallVisibility => GetUninstallVisibility();
    public Visibility HomepageVisibility => GetHomepageVisibility();

    public string StatusText => GetStatusText();
    public string ButtonText => GetButtonText();

    /** End Properties referenced by views requiring notification. */

    private readonly List<ModId> MissingRequirements = new();

    private DirectoryInfo _modDir;
    public DirectoryInfo ModDir
    {
      get => _modDir;
      set
      {
        if (_modDir is not null && _modDir.FullName == value.FullName)
          return;
        _modDir = value;
        Changed(nameof(CanUninstall), nameof(UninstallVisibility));
      }
    }

    public ModId ModId => Manifest.Id;
    public ModType Type => ModId.Type;
    public string DescriptionAsText => stripHtml.Replace(Description, "");

    public ModVersion InstalledVersion
    {
      get => Status.Version;
      set
      {
        if (Status.Version == value) return;
        Status.Version = value;
        NotifyStatus();
      }
    }

    public InstallState InstallState
    {
      get => Status.State;
      set
      {
        if (Status.State == value) return;
        Status.State = value;
        NotifyStatus();
      }
    }

    /// <summary>
    /// Takes the "Requirements" entry from UMM's mod info and converts to a list of required ModIds.
    /// </summary>
    public void SetRequirements(List<string> requirements)
    {
      RequiredMods.Clear();

      if (requirements is null)
        return;

      foreach (var id in requirements)
      {
        ModVersion requiredVersion = default;
        var idStr = id;
        var separatorIndex = id.IndexOf('-');
        if (separatorIndex > 0)
        {
          // There's a version requirement
          requiredVersion = ModVersion.Parse(id[separatorIndex..]);
          idStr = id[..separatorIndex];
        }

        RequiredMods.Add((new(idStr, ModType.UMM), requiredVersion));
      }
    }

    /// <summary>
    /// Checks whether the required mods are available and updates state accordingly.
    /// </summary>
    public void CheckRequirements(Dictionary<ModId, ModVersion> installedMods)
    {
      MissingRequirements.Clear();
      if (RequiredMods.Any())
      {
        foreach (var (id, version) in RequiredMods)
        {
          if (installedMods.TryGetValue(id, out var installedVersion))
          {
            if (installedVersion < version)
              MissingRequirements.Add(id);
          }
          else
          {
            MissingRequirements.Add(id);
          }
        }
      }

      NotifyAll();
    }

    public bool MatchesAuthor(string author)
    {
      return Author is not null && Author.Contains(author, System.StringComparison.CurrentCultureIgnoreCase);
    }

    public bool MatchesName(string name)
    {
      return Name is not null && Name.Contains(name, System.StringComparison.CurrentCultureIgnoreCase);
    }

    public bool HasTag(Tag tag)
    {
      return Manifest.Tags.Contains(tag);
    }

    public void OnUninstalled()
    {
      InstalledVersion = default;
      InstallState = InstallState.None;
    }

    private string GetStatusText()
    {
      if (InstallState == InstallState.Installing)
      {
        return "Installing...";
      }

      if (!IsInstalled)
      {
        return "Not installed";
      }

      if (InstalledVersion < Latest.Version)
      {
        return $"Update available from {InstalledVersion} to {Latest}";
      }

      if (RequiredMods.Any())
      {
        var sb = new StringBuilder();
        foreach (var (id, version) in RequiredMods)
        {
          if (version == default)
            sb.Append($"{id.Id}, ");
          else
            sb.Append($"{id.Id}-{version}, ");
        }
        sb.Remove(sb.Length - 2, 2);
        return $"Missing pre-reqs: {sb}";
      }

      return $"Latest version installed: {InstalledVersion}";
    }

    private string GetSourceText()
    {
      if (Manifest.Service.IsGitHub())
        return "GitHub";
      if (Manifest.Service.IsNexus())
        return "Nexus";
      return "Local";
    }

    private string GetButtonText()
    {
      if (Status.State == InstallState.Installing)
        return "Installing...";
      if (CanInstall)
        return "Install";
      if (CanDownload)
        return "Download";
      if (MissingRequirements.Any())
      {
        var nextMod = GetNextAvailableRequirement();
        if (nextMod is not null)
        {
          if (nextMod.CanInstall)
            return $"Install {nextMod.Name}";
          if (nextMod.CanDownload)
            return $"Download {nextMod.Name}";
        }
      }
      if (Latest.Version == default)
        return "Unavailable";
      return "Up to date";
    }

    public ModViewModel GetNextAvailableRequirement()
    {
      foreach (var id in MissingRequirements)
      {
        var nextMod = ModDatabase.Instance.GetModViewModel(id);
        if (nextMod is not null)
        {
          if (nextMod.CanInstall || nextMod.CanDownload)
            return nextMod;
        }
      }
      return null;
    }

    private Visibility GetHomepageVisibility()
    {
      if (!string.IsNullOrEmpty(HomepageUrl))
        return Visibility.Visible;
      return Visibility.Collapsed;
    }

    private Visibility GetUninstallVisibility()
    {
      if (CanUninstall)
        return Visibility.Visible;
      return Visibility.Collapsed;
    }

    private void NotifyAll()
    {
      Changed(
        nameof(Name),
        nameof(Author),
        nameof(Description),
        nameof(Service),
        nameof(HomepageVisibility));
      NotifyStatus();
    }

    private void NotifyStatus()
    {
      Changed(
        nameof(StatusText),
        nameof(ButtonText),
        nameof(IsInstalled),
        nameof(CanInstall),
        nameof(CanUninstall),
        nameof(CanDownload));
    }

    private void Changed(params string[] props)
    {
      foreach (var prop in props)
        PropertyChanged?.Invoke(this, new(prop));
    }
  }
}
