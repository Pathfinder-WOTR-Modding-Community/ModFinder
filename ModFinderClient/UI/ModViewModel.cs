using ModFinder.Mod;
using System.ComponentModel;
using System.IO;
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
      if (Latest.Version == default(ModVersion))
        return "Unavailable";
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
