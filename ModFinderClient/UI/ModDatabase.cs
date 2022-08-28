using ModFinder.Mod;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

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
        if (Filters.Any())
          return Filtered;
        if (ShowInstalled)
          return Installed;
        return All;
      }
    }

    private readonly HashSet<Tag> Filters = new();

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

    public void AddFilter(Tag tag)
    {
      Filters.Add(tag);
      UpdateFilter();
    }

    public void RemoveFilter(Tag tag)
    {
      Filters.Remove(tag);
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

    private void UpdateFilter()
    {
      Filtered.Clear();
      if (Filters.Any())
      {
        Filtered.Clear();
        var source = ShowInstalled ? Installed : All;
        foreach (var viewModel in source)
        {
          foreach (var tag in Filters)
          {
            if (viewModel.HasTag(tag))
            {
              Filtered.Add(viewModel);
              break; // As long as any tag matches include it, so just exit the inner loop
            }
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
