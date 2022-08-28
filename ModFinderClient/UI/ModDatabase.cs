using ModFinder.Mod;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace ModFinder.UI
{
  public class ModDatabase : INotifyPropertyChanged
  {
    private readonly ObservableCollection<ModViewModel> All = new();
    private readonly ObservableCollection<ModViewModel> Installed = new();
    private readonly Dictionary<ModId, ModViewModel> Mods = new();
    private bool _ShowInstalled;
    public event PropertyChangedEventHandler PropertyChanged;

    public string HeaderNameText => ShowInstalled ? "Update" : "Install";

    public ObservableCollection<ModViewModel> Items => ShowInstalled ? Installed : All;

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

    public IEnumerable<ModViewModel> AllMods => All;

    public void Add(ModViewModel mod)
    {
      All.Add(mod);
      Mods[mod.ModId] = mod;
      UpdateInstallState(mod);

      mod.PropertyChanged += (sender, e) =>
      {
        if (e.PropertyName == "IsInstalled")
          UpdateInstallState(mod);
      };
    }

    private void UpdateInstallState(ModViewModel mod)
    {
      if (!mod.IsInstalled)
        _ = Installed.Remove(mod);
      else if (mod.IsInstalled && !Installed.Contains(mod))
        Installed.Add(mod);
    }

    internal bool TryGet(ModId id, out ModViewModel mod) => Mods.TryGetValue(id, out mod);
  }
}
