using ModFinder.Mod;
using ModFinder.Util;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;

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

    public void InitSort()
    {
      List<ModViewModel> allMods = new(All);
      allMods.Sort((a, b) => (int)b.StatusIcon - (int)a.StatusIcon);
      All.Clear();
      allMods.ForEach(m => All.Add(m));

      List<ModViewModel> installedMods = new(Installed);
      installedMods.Sort((a, b) => (int)b.StatusIcon - (int)a.StatusIcon);
      Installed.Clear();
      installedMods.ForEach(m => Installed.Add(m));

      PropertyChanged?.Invoke(this, new(nameof(Items)));
    }

    public void ApplyFilter(string filter)
    {
      try
      {
        if (Filters.UpdateFilter(filter))
          UpdateFilter();
      }
      catch (Exception e)
      {
        Logger.Log.Error("Failed to apply filter.", e);
      }
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
