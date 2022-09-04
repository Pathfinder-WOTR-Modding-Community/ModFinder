using ModFinder.Mod;
using ModFinder.Util;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ModFinder.UI
{
  public class FilterModel
  {
    private readonly HashSet<string> currentFilter = new();

    private readonly List<string> IncludeAuthors = new();
    private readonly List<string> ExcludeAuthors = new();

    private readonly List<string> IncludeNames = new();
    private readonly List<string> ExcludeNames = new();

    private readonly List<Tag> IncludeTags = new();
    private readonly List<Tag> ExcludeTags = new();

    private readonly List<string> RawFilters = new();

    /// <returns>True if the requested filter is different from the current filter, false otherwise</returns>
    public bool UpdateFilter(string textFilter)
    {
      var newFilter =
        textFilter.Split(' ').Select(t => t.Trim()).Where(t => !string.IsNullOrEmpty(t));

      if (EqualsCurrentFilter(newFilter))
        return false;

      ClearFilters();
      newFilter.ToList().ForEach(
        f =>
        {
          var filterStr = f.ToLower();
          if (currentFilter.Add(filterStr))
            ProcessFilterString(filterStr);
        });
      Logger.Log.Verbose($"Filter updated: {textFilter}");
      return true;
    }

    public bool Active => currentFilter.Any(); 

    public bool Matches(ModViewModel viewModel)
    {
      foreach (var author in ExcludeAuthors)
      {
        if (viewModel.MatchesAuthor(author))
          return false;
      }

      foreach (var name in ExcludeNames)
      {
        if (viewModel.MatchesName(name))
          return false;
      }

      foreach (var tag in ExcludeTags)
      {
        if (viewModel.HasTag(tag))
          return false;
      }

      foreach (var author in IncludeAuthors)
      {
        if (viewModel.MatchesAuthor(author))
          return true;
      }

      foreach (var name in IncludeNames)
      {
        if (viewModel.MatchesName(name))
          return true;
      }

      foreach (var tag in IncludeTags)
      {
        if (viewModel.HasTag(tag))
          return true;
      }

      if (!RawFilters.Any())
        return false;

      // Need to match all raw searches
      foreach (var raw in RawFilters)
      {
        if (!viewModel.MatchesAuthor(raw) && !viewModel.MatchesName(raw))
          return false;
      }

      return true;
    }

    private void ProcessFilterString(string filter)
    {
      var specialIndex = filter.IndexOf(':');
      if (specialIndex > 0)
      {
        var prefix = filter[..specialIndex];
        var filterStr = filter[(specialIndex + 1)..];
        var include = prefix[0] != '-';
        string type = include ? prefix : prefix[1..];
        switch (type)
        {
          case "a":
            AddAuthor(filterStr, include);
            break;
          case "n":
            AddName(filterStr, include);
            break;
          case "t":
            AddTag(filterStr, include);
            break;
          default:
            ProcessRawFilter(filter);
            break;
        }
      }
      else
      {
        ProcessRawFilter(filter);
      }
    }

    private void ProcessRawFilter(string filter)
    {
      RawFilters.Add(filter);
    }

    private void AddAuthor(string author, bool include = true)
    {
      if (include)
        IncludeAuthors.Add(author);
      else
        ExcludeAuthors.Add(author);
    }

    private void AddName(string name, bool include = true)
    {
      if (include)
        IncludeNames.Add(name);
      else
        ExcludeNames.Add(name);
    }

    private static readonly Dictionary<string, Tag> AllTags =
      new(Enum.GetValues<Tag>().Select(t => new KeyValuePair<string, Tag>(t.ToString(), t)));
    private void AddTag(string tag, bool include = true)
    {
      foreach (var tagStr in AllTags.Keys)
      {
        if (tagStr.Contains(tag))
        {
          if (include)
            IncludeTags.Add(AllTags[tagStr]);
          else
            ExcludeTags.Add(AllTags[tagStr]);
        }
      }

    }

    /// <returns>True if newFilter is equivalent to currentFilter, false otherwise</returns>
    private bool EqualsCurrentFilter(IEnumerable<string> newFilter)
    {
      if (newFilter.Count() != currentFilter.Count)
        return false;

      foreach (var str in newFilter)
      {
        if (!currentFilter.Contains(str))
          return false;
      }
      return true;
    }

    private void ClearFilters()
    {
      currentFilter.Clear();

      IncludeAuthors.Clear();
      ExcludeAuthors.Clear();

      IncludeNames.Clear();
      ExcludeNames.Clear();

      IncludeTags.Clear();
      ExcludeTags.Clear();

      RawFilters.Clear();
    }
  }
}
