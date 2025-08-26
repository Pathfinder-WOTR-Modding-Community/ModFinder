using ModFinder.Mod;
using ModFinder.Util;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
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
  public class ModViewModel : DependencyObject, INotifyPropertyChanged
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
    public string EnabledText => Enabled ? "On" : "Off";
    public string Source => Manifest.Service.Name;

    public bool Enabled
    {
      get => (bool)GetValue(EnabledProperty);
      set
      {
        //if (Enabled == value) return;
        SetValue(EnabledProperty, value);
      }
    }
    public bool IsInstalled => Status.Installed();
    public bool IsCached => ModCache.IsCached(ModId);

    public bool CanInstall =>
      (IsCached && !IsInstalled)
        || (Manifest.Service.IsGitHub()
          && (!IsInstalled || Status.IsVersionBehind(Latest.Version))
          && !string.IsNullOrEmpty(Latest.Url))
        || (Manifest.Service.IsNexus()
          && (!IsInstalled || Status.IsVersionBehind(Latest.Version))
          && (Manifest.Service.Nexus.DownloadMirror != null || !string.IsNullOrWhiteSpace(Main.Settings.MaybeGetNexusKey())));
    public bool CanDownload =>
      Manifest.Service.IsNexus()
      && (!IsInstalled || Status.IsVersionBehind(Latest.Version))
      && !string.IsNullOrEmpty(Latest.Url);
    public bool CanInstallOrDownload => InstallOrDownloadAvailable();
    public bool CanUninstall => IsInstalled && ModDir != null;

    public Visibility UninstallVisibility => CanUninstall ? Visibility.Visible : Visibility.Collapsed;

    public bool HasHomepage => !string.IsNullOrEmpty(HomepageUrl);
    public Visibility HomepageVisibility => HasHomepage ? Visibility.Visible : Visibility.Collapsed;

    public bool CanRollback => IsCached && IsInstalled;
    public Visibility RollbackVisibility => CanRollback ? Visibility.Visible : Visibility.Collapsed;

    public string StatusText => GetStatusText();
    public string ButtonText => GetButtonText();

    public StatusIcon StatusIcon => GetStatusIcon();

    /** End Properties referenced by views requiring notification. */

    internal readonly List<(ModId id, ModVersion version)> MissingRequirements = new();

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

    private static DependencyProperty MakeProp<T>(string name, Action<ModViewModel, T> onChange)
    {
      return DependencyProperty.Register(name, typeof(T), typeof(ModViewModel),
        new((sender, args) =>
        {
          if (sender is not ModViewModel self) return;
          onChange(self, (T)args.NewValue);
        })
      );
    }

    public static readonly DependencyProperty EnabledProperty = MakeProp<bool>("Enabled", (self, enabled) =>
    {
      if (self.IsInstalled)
      {
        MainWindow.SetModEnabled(self, enabled);
      }
      self.Changed(nameof(EnabledText));
    });

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
        var iDVersionpattern = @"(.*)-(\d+.*)";
        var match = Regex.Match(idStr, iDVersionpattern);
        if (match.Success)
        {
          requiredVersion = ModVersion.Parse(match.Groups[2].Value);
          idStr = match.Groups[1].Value;
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
      Enabled = false;
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
      if (InstallState == InstallState.Installing || InstallState == InstallState.Uninstalling)
      {
        return false;
      }

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
      return Manifest.LastChecked.ToString("MMM dd yyyy");
    }

    public string GetLastUpdated()
    {
      if (Manifest.Version.LastUpdated == default)
        return "-";
      return Manifest.Version.LastUpdated.ToString("MMM dd yyyy");
    }

    private string GetStatusText()
    {
      if (InstallState == InstallState.Installing)
      {
        return "Installing...";
      }

      if (!IsInstalled)
      {
        if (IsCached)
          return "Not installed (in cache)";
        else
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

    private string GetButtonText()
    {
      if (Status.State == InstallState.Installing)
        return "Installing...";
      if (Status.State == InstallState.Uninstalling)
        return "Uninstalling...";
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

    #region Notify
    private void NotifyAll()
    {
      Changed(
        nameof(Name),
        nameof(Author),
        nameof(About),
        nameof(Description),
        nameof(HomepageVisibility));
      NotifyStatus();
    }

    private void NotifyStatus()
    {
      Changed(
        nameof(StatusText),
        nameof(ButtonText),
        nameof(EnabledText),
        nameof(Enabled),
        nameof(IsInstalled),
        nameof(CanInstall),
        nameof(CanUninstall),
        nameof(CanDownload),
        nameof(CanRollback),
        nameof(CanInstallOrDownload),
        nameof(HasHomepage),
        nameof(UninstallVisibility),
        nameof(RollbackVisibility),
        nameof(StatusIcon),
        nameof(Latest),
        nameof(LastChecked),
        nameof(LastUpdated),
        nameof(InstalledVersion));
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
