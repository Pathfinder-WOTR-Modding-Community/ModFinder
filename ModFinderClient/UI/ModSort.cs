using ModFinder.Mod;
using System;
using System.Collections;
using System.Linq;

namespace ModFinder.UI
{
  // Supported columns for custom sorting
  internal enum SortColumn
  {
    Author,
    Status
  }

  internal class ModSort : IComparer
  {
    private readonly SortColumn Column;
    private readonly bool Invert;

    public ModSort(SortColumn column, bool invert)
    {
      Column = column;
      Invert = invert;
    }

    public int Compare(object x, object y)
    {
      var modelX = x as ModViewModel;
      var modelY = y as ModViewModel;
      if (x is null || y is null)
        throw new ArgumentException("Can only sort ModViewModel.");

      return Column switch
      {
        SortColumn.Author => Invert ? CompareAuthor(modelY, modelX) : CompareAuthor(modelX, modelY),
        SortColumn.Status => Invert ? CompareStatus(modelY, modelX) : CompareStatus(modelX, modelY),
        _ => throw new ArgumentException($"Unsupported column for sorting: {Column}"),
      };
    }

    private int CompareAuthor(ModViewModel x, ModViewModel y)
    {
      if (x.Author == y.Author)
        return Invert ? CompareStatus(y, x) : CompareStatus(x, y); // Don't invert the secondary sorting
      if (string.IsNullOrEmpty(x.Author))
        return 1;
      if (string.IsNullOrEmpty(y.Author))
        return -1;
      return x.Author.CompareTo(y.Author);
    }

    private int CompareStatus(ModViewModel x, ModViewModel y)
    {
      var statusX = GetStatus(x);
      var statusY = GetStatus(y);
      if (statusX == statusY)
        return Invert ? y.Name.CompareTo(x.Name) : x.Name.CompareTo(y.Name); // Don't invert the secondary sorting
      return statusX - statusY;
    }

    private static Status GetStatus(ModViewModel mod)
    {
      if (mod.InstallState == InstallState.Installing)
        return Status.Installing;
      if (!mod.IsInstalled && !mod.IsCached)
        return Status.Uninstalled;
      if (!mod.IsInstalled && mod.IsCached)
        return Status.Cached;
      if (mod.InstalledVersion < mod.Latest.Version)
        return Status.UpdateAvailable;
      if (mod.MissingRequirements.Any())
        return Status.MissingRequirements;
      return Status.Installed;
    }

    // Numbers are assigned to create priority for easy sorting
    private enum Status
    {
      MissingRequirements = 1,
      UpdateAvailable = 2,
      Installing = 3,
      Installed = 4,
      Cached = 5,
      Uninstalled = 6,
    }
  }
}
