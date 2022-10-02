using ModFinder.Mod;
using System;
using System.Windows.Documents;
using System.Windows.Media;

namespace ModFinder.UI
{
  class ChangelogRenderer
  {
    public static void Render(FlowDocument doc, ModViewModel mod)
    {
      if (mod.Manifest.Version.VersionHistory is null)
        return;

      foreach (var release in mod.Manifest.Version.VersionHistory)
      {
        RenderSection(doc, release, mod.InstalledVersion);
      }
    }

    private static void RenderSection(FlowDocument doc, Release version, ModVersion installedVersion)
    {
      if (string.IsNullOrEmpty(version.Changelog))
        return;

      var section = new Section();

      if (version.Version >= installedVersion)
        section.Foreground = Brushes.Black;
      else
        section.Foreground = Brushes.DimGray;

      var heading = new Bold(new Run(version.Version.ToString()));
      heading.FontSize += 12;
      section.Blocks.Add(new Paragraph(heading));

      var entries = new List();
      foreach (var line in version.Changelog.Split('\n'))
      {
        var trimmed = line.AsSpan().TrimStart();
        if (trimmed.Length == 0)
          continue;
        else if (trimmed[0] == '#')
        {
          if (entries.ListItems.Count > 0)
          {
            section.Blocks.Add(entries);
            entries = new();
          }
          section.Blocks.Add(new Paragraph(new Bold(new Run(line.TrimStart('#')))));
        }
        else
        {
          var bold = false;
          if (trimmed.Length > 4 && trimmed.StartsWith("**") && trimmed.EndsWith("**"))
          {
            trimmed = trimmed.Slice(2, trimmed.Length - 4);
            bold = true;
          }
          if (!trimmed.StartsWith("**") && trimmed[0] is '*' or '>' or '-')
            trimmed = trimmed[1..];
          Inline run = new Run(trimmed.ToString());
          if (bold)
            run = new Bold(run);
          entries.ListItems.Add(new ListItem(new Paragraph(run)));
        }
      }
      if (entries.ListItems.Count > 0)
        section.Blocks.Add(entries);

      doc.Blocks.Add(section);
    }
  }
}
