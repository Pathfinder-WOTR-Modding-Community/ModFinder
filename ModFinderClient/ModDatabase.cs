using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace ModFinder
{
  public class ModDatabase : INotifyPropertyChanged
  {
    private readonly ObservableCollection<ModDetails> All = new();
    private readonly ObservableCollection<ModDetails> Installed = new();
    private readonly Dictionary<ModId, ModDetails> Mods = new();
    private bool _ShowInstalled;
    public event PropertyChangedEventHandler PropertyChanged;

    public string HeaderNameText => ShowInstalled ? "Update" : "Install";

    public ObservableCollection<ModDetails> Items => ShowInstalled ? Installed : All;

    public bool ShowInstalled
    {
      get => _ShowInstalled;
      set
      {
        _ShowInstalled = value;
        PropertyChanged?.Invoke(this, new(nameof(ShowInstalled)));
        PropertyChanged?.Invoke(this, new(nameof(HeaderNameText)));
        PropertyChanged?.Invoke(this, new(nameof(Items)));
      }
    }

    private ModDatabase() { }

    private static ModDatabase _Instance;
    public static ModDatabase Instance => _Instance ??= new();

    public IEnumerable<ModDetails> AllMods => All;

    public void Add(ModDetails mod)
    {
      All.Add(mod);
      Mods[mod.ModId] = mod;
      UpdateInstallState(mod);

      mod.PropertyChanged += (sender, e) =>
      {
        if (e.PropertyName == "State")
          UpdateInstallState(mod);
      };
    }

    private void UpdateInstallState(ModDetails mod)
    {
      if (mod.State == ModState.NotInstalled)
        _ = Installed.Remove(mod);
      else if (mod.State == ModState.Installed && !Installed.Contains(mod))
        Installed.Add(mod);
    }

    internal bool TryGet(ModId id, out ModDetails mod) => Mods.TryGetValue(id, out mod);
  }
}
