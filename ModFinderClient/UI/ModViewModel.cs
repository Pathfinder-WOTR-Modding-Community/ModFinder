using ModFinder.Mod;
using System.ComponentModel;
using System.IO;
using System.Text.RegularExpressions;

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
    public ModVersion Latest;

    public event PropertyChangedEventHandler PropertyChanged;

    public ModViewModel(ModManifest manifest)
    {
      Refresh(manifest, refreshUI: false);
    }

    public void Refresh(ModManifest manifest, bool refreshUI = true)
    {
      Manifest = manifest;
      Latest = ModVersion.FromRelease(Manifest.Version.Latest);

      if (refreshUI)
        NotifyAll();
    }

    /** Start Properties referenced by views requiring notification. */

    public string Name => Manifest.Name;
    public string Author => Manifest.Author;
    public string Description => Manifest.Description ?? "-";

    public bool IsInstalled => Status.Installed();
    public bool CanInstall =>
      (!IsInstalled || Status.IsVersionBehind(Latest))
      && Service == HostService.GitHub
      && !string.IsNullOrEmpty(Latest.DownloadUrl);
    public bool CanUninstall => IsInstalled && ModDir != null;
    public bool CanDownload =>
      (!IsInstalled || Status.IsVersionBehind(Latest))
      && Service != HostService.GitHub
      && !string.IsNullOrEmpty(Latest.DownloadUrl);

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
        Changed(nameof(CanUninstall));
      }
    }

    public ModId ModId => Manifest.Id;
    public ModType Type => ModId.Type;
    public HostService Service => Manifest.Service;
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

      if (InstalledVersion < Latest)
      {
        return $"Update available from {InstalledVersion} to {Latest}";
      }

      return $"Latest version installed: {InstalledVersion}";
    }

    private string GetButtonText()
    {
      if (Status.State == InstallState.Installing)
        return "Installing...";
      if (CanInstall)
        return "Install";
      if (CanDownload)
        return "Download";
      if (Latest == default(ModVersion))
        return "Unavailable";
      return "Up to date";
    }

    private void NotifyAll()
    {
      Changed(nameof(Name), nameof(Author), nameof(Description));
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
