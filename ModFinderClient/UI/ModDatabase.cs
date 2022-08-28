using ModFinder.Mod;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace ModFinder.UI
{
  public class ModDatabase : INotifyPropertyChanged
  {
    private readonly ObservableCollection<ModViewModel> All = new();
    private readonly ObservableCollection<ModViewModel> Installed = new();
    private readonly ObservableCollection<ModViewModel> Filtered = new();
    private readonly Dictionary<ModId, ModViewModel> Mods = new();
    private bool _ShowInstalled;
    public event PropertyChangedEventHandler PropertyChanged;

    public string HeaderNameText => ShowInstalled ? "Update" : "Install";

    public ObservableCollection<ModViewModel> Items
    {
      get
      {
        if (Filters.Active)
          return Filtered;
        if (ShowInstalled)
          return Installed;
        return All;
      }
    }

    private readonly FilterModel Filters = new();

    public bool ShowInstalled
    {
      get => _ShowInstalled;
      set
      {
        _ShowInstalled = value;
        UpdateFilter();
        PropertyChanged?.Invoke(this, new(nameof(ShowInstalled)));
        PropertyChanged?.Invoke(this, new(nameof(HeaderNameText)));
        PropertyChanged?.Invoke(this, new(nameof(Items)));
      }
    }

    private ModDatabase() { }

    private static ModDatabase _Instance;
    public static ModDatabase Instance => _Instance ??= new();

    public IEnumerable<ModViewModel> AllMods => All;

    public void ApplyFilter(string filter)
    {
      if (Filters.UpdateFilter(filter))
        UpdateFilter();
    }

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

    public ModViewModel GetModViewModel(ModId id)
    {
      if (Mods.ContainsKey(id))
        return Mods[id];
      return null;
    }

    private void UpdateFilter()
    {
      Filtered.Clear();
      if (Filters.Active)
      {
        var source = ShowInstalled ? Installed : All;
        foreach (var viewModel in source)
        {
          if (Filters.Matches(viewModel))
          {
            Filtered.Add(viewModel);
          }
        }
      }
      PropertyChanged?.Invoke(this, new(nameof(Items)));
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
