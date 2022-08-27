using ModFinder.Mod;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
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

    public bool IsInstalled => Details.InstallState == InstallState.Installed;
    public bool CanInstall => Details.InstallState != InstallState.Installing;
    public string InstallButtonText
    {
      get
      {
        if (IsInstalled)
          return Version.ToString();
        else if (Details.InstallState == InstallState.Installing)
          return "Installing...";
        else
          return "Install";
      }
    }

    public ModVersion Version
    {
      get => Details.InstalledVersion;
      set
      {
        if (Details.InstalledVersion == value) return;
        Details.InstalledVersion = value;
        Changed(nameof(Version), nameof(CanInstall), nameof(InstallButtonText));
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
  }
}
