using ModFinder.Mod;
using ModFinder.Util;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

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

    public string LastUpdated => GetLastUpdated();
    public string LastChecked => GetLastChecked();

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
    public string About => Manifest.About;
    public string Description => Manifest.Description ?? "-";

    public string Service => GetSourceText();

    public bool IsInstalled => Status.Installed();
    public bool IsCached => ModCache.IsCached(ModId);

    public bool CanInstall =>
      (IsCached && !IsInstalled)
        || (Manifest.Service.IsGitHub()
          && (!IsInstalled || Status.IsVersionBehind(Latest.Version))
          && !string.IsNullOrEmpty(Latest.Url));
    public bool CanDownload =>
      Manifest.Service.IsNexus()
      && (!IsInstalled || Status.IsVersionBehind(Latest.Version))
      && !string.IsNullOrEmpty(Latest.Url);
    public bool CanInstallOrDownload => InstallOrDownloadAvailable();
    public bool CanUninstall => IsInstalled && ModDir != null;

    public Visibility UninstallVisibility => GetUninstallVisibility();
    public Visibility HomepageVisibility => GetHomepageVisibility();
    public Visibility RollbackVisibility => GetRollbackVisibility();

    public string StatusText => GetStatusText();
    public string ButtonText => GetButtonText();

    public StatusIcon StatusIcon => GetStatusIcon();

    /** End Properties referenced by views requiring notification. */

    private readonly List<(ModId id, ModVersion version)> MissingRequirements = new();

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
      {
        Logger.Log.Info($"{Name} has no required modes.");
        return;
      }

      foreach (var id in requirements)
      {
        ModVersion requiredVersion = default;
        var idStr = id;
        var separatorIndex = id.LastIndexOf('-');
        if (separatorIndex > 0)
        {
          // There's a version requirement
          requiredVersion = ModVersion.Parse(id[separatorIndex..]);

          // The mod ID might have a '-' :( *cough*TTT*cough*
          if (requiredVersion != default)
            idStr = id[..separatorIndex];
        }

        Logger.Log.Info($"{Name} requires {idStr} at version {requiredVersion}");
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
              MissingRequirements.Add((id, version));
          }
          else
          {
            MissingRequirements.Add((id, version));
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

    public ModViewModel GetNextAvailableRequirement()
    {
      foreach (var (id, _) in MissingRequirements)
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

    private bool InstallOrDownloadAvailable()
    {
      var installAvailable = CanInstall || CanDownload;
      var nextMod = GetNextAvailableRequirement();
      if (nextMod is null)
        return installAvailable;
      return installAvailable || nextMod.CanInstall || nextMod.CanDownload;
    }

    public string GetLastChecked()
    {
      if (Manifest.LastChecked == default)
        return "-";
      return Manifest.LastChecked.ToString("MMM dd H:mm");
    }

    public string GetLastUpdated()
    {
      if (Manifest.Version.LastUpdated == default)
        return "-";
      return Manifest.Version.LastUpdated.ToString("MMM dd H:mm");
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
        return $"Update available from {InstalledVersion} to {Latest.Version}";
      }

      if (MissingRequirements.Any())
      {
        var sb = new StringBuilder();
        foreach (var (id, version) in MissingRequirements)
        {
          if (version == default)
            sb.Append($"{id.Id}, ");
          else
            sb.Append($"{id.Id}-{version}, ");
        }
        sb.Remove(sb.Length - 2, 2);
        return $"Missing pre-reqs: {sb}";
      }

      return $"Installed: {InstalledVersion}";
    }

    private StatusIcon GetStatusIcon()
    {
      if (IsInstalled)
      {
        if (MissingRequirements.Any())
          return StatusIcon.Error;
        if (InstalledVersion < Latest.Version)
          return StatusIcon.Warning;
        return StatusIcon.Okay;
      }
      return StatusIcon.None;
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
      if (IsInstalled && InstalledVersion < Latest.Version)
        return "Update";
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
          {
            var text = $"Install {nextMod.Name}";
            if (text.Length > 20)
              text = $"{text[..20]}...";
            return text;
          }
          if (nextMod.CanDownload)
          {
            var text = $"Download {nextMod.Name}";
            if (text.Length > 20)
              text = $"{text[..20]}...";
            return text;
          }
        }
      }
      if (Latest.Version == default)
        return "-";
      return "Up to date";
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

    private Visibility GetRollbackVisibility()
    {
      if (IsCached && IsInstalled)
        return Visibility.Visible;
      return Visibility.Collapsed;
    }

    #region Notify
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
        nameof(CanDownload),
        nameof(CanInstallOrDownload),
        nameof(UninstallVisibility),
        nameof(RollbackVisibility),
        nameof(StatusIcon));
    }

    private void Changed(params string[] props)
    {
      foreach (var prop in props)
        PropertyChanged?.Invoke(this, new(prop));
    }
    #endregion
  }

  // StatusIcon value is used as priority for sorting
  public enum StatusIcon
  {
    None = 0,
    Okay = 1,
    Warning = 2,
    Error = 3
  }
}
