using ModFinder.Mod;
using System.ComponentModel;
using System.Text.RegularExpressions;

namespace ModFinder.UI
{
  /// <summary>
  /// View model tracking the current mod state.
  /// </summary>
  public class ModViewModel : INotifyPropertyChanged
  {
    private static readonly Regex stripHtml = new(@"<.*?>");
    private readonly ModDetails Details;

    public event PropertyChangedEventHandler PropertyChanged;

    public ModViewModel(ModDetails details)
    {
      Details = details;
    }

    public ModViewModel(ModManifest manifest)
    {
      Details = new(manifest);
    }

    public bool IsInstalled => Details.InstallState == InstallState.Installed;
    public bool CanInstall => !IsInstalled || LatestVersion > InstalledVersion;
    public string InstallButtonText
    {
      get
      {
        if (Details.InstallState == InstallState.Installing)
          return "Installing...";
        else if (!IsInstalled)
          return "Install";
        else if (LatestVersion == default(ModVersion))
          return "Unavailable";
        else if (LatestVersion > InstalledVersion)
          return "Update";
        else
          return "Up to date";
      }
    }

    public ModManifest Manifest => Details.Manifest;

    public ModVersion LatestVersion
    {
      get => Details.LatestVersion;
      set
      {
        if (Details.LatestVersion == value) return;
        Details.LatestVersion = value;
        Changed(nameof(LatestVersion), nameof(CanInstall), nameof(InstallButtonText));
      }
    }

    public ModVersion InstalledVersion
    {
      get => Details.InstalledVersion;
      set
      {
        if (Details.InstalledVersion == value) return;
        Details.InstalledVersion = value;
        Changed(nameof(InstalledVersion), nameof(CanInstall), nameof(InstallButtonText));
      }
    }

    private ModFinderInfo _modFinderInfo;
    public ModFinderInfo ModFinderInfo
    {
      get => _modFinderInfo;
      set
      {
        _modFinderInfo = value;
        LatestVersion = ModVersion.FromInfo(_modFinderInfo.LatestVersion);
      }
    }

    public InstallState InstallState
    {
      get => Details.InstallState;
      set
      {
        if (Details.InstallState == value) return;
        Details.InstallState = value;
        Changed(nameof(InstallState), nameof(CanInstall), nameof(InstallButtonText));
      }
    }

    private void Changed(params string[] props)
    {
      foreach (var prop in props)
        PropertyChanged?.Invoke(this, new(prop));
    }

    public string Name => Details.Manifest.Name;
    public string Author => Details.Manifest.Author;
    public string Description => Details.Manifest.Description ?? "-";
    public string DescriptionAsText => stripHtml.Replace(Description, "");

    public ModId ModId => Details.Manifest.Id;
    public string Identifier => ModId.Id;
    public ModType ModType => ModId.Type;
    public string UniqueId => Identifier + "_" + ModType.ToString();

    public ModSource Source => Details.Manifest.Source;
    public string SourceString => GetSourceString();

    private string GetSourceString()
    {
      if (Source.GitHub is not null)
      {
        return "GitHub";
      }
      return "Local";
    }
  }
}
