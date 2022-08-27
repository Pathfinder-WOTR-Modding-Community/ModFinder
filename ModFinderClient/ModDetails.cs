using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using ModFinder.Infrastructure;

namespace ModFinder
{

  /// <summary>
  /// This is current active state of a mod
  /// </summary>
  public class ModDetails : INotifyPropertyChanged
  {
    private static readonly Regex stripHtml = new(@"<.*?>");
    private readonly ModDetailsInternal Details;

    public event PropertyChangedEventHandler PropertyChanged;

    public ModDetails(ModDetailsInternal details)
    {
      Details = details;
    }


    public bool CanUninstall => State == ModState.Installed;
    public bool CanInstall => State == ModState.NotInstalled || (State == ModState.Installed && Details.Latest > InstalledVersion);
    public string InstallButtonText
    {
      get
      {
        if (CanInstall)
          return Details.Latest.ToString();
        else if (State == ModState.Installing)
          return "installing...";
        else
          return "up to date";
      }

    }

    private void Changed(params string[] props)
    {
      foreach (var prop in props)
        PropertyChanged?.Invoke(this, new(prop));
    }

    private ModVersion _InstalledVersion;
    public ModVersion InstalledVersion
    {
      get => _InstalledVersion;
      set
      {
        if (_InstalledVersion == value) return;
        _InstalledVersion = value;
        Changed(nameof(InstalledVersion), nameof(CanInstall), nameof(InstallButtonText));
      }
    }

    private ModState _State;
    public ModState State
    {
      get => _State;
      set
      {
        if (_State == value) return;
        _State = value;
        Changed(nameof(State), nameof(CanInstall), nameof(InstallButtonText));
      }
    }

    public ModVersion Latest => Details.Latest;
    public string Description => Details.Description ?? "-";
    public string DescriptionAsText => stripHtml.Replace(Description, "");
    public string DownloadLink => Details.DownloadLink;

    public ModId ModId => Details.ModId;
    public string Identifier => ModId.Identifier;
    public ModType ModType => ModId.ModType;
    public string UniqueId => Identifier + "_" + ModType.ToString();

    public string Name => Details.Name;
    public ModSource Source => Details.Source;
    public string Author => Details.Author;

    public IEnumerable<ChangelogEntry> Changelog => Details.Changelog ?? Enumerable.Empty<ChangelogEntry>();
  }
}
