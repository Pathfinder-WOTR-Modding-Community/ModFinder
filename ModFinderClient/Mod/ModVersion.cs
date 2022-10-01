using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks.Dataflow;

namespace ModFinder.Mod
{
  /// <summary>
  /// Represents a mod version.
  /// </summary>
  /// 
  /// <remarks>
  /// Format is <c>A.B.Cd</c> where <c>A, B, and C</c> are integers representing the Major, Minor, and Patch versions.
  /// <c>d</c> is a single character used to further indicate a patch version.
  /// </remarks>
  public struct ModVersion : IComparable<ModVersion>, IEquatable<ModVersion>
  {
    public int Major, Minor, Patch;
    public string Suffix;

    public bool Valid => !(Major == 0 && Minor == 0 && Patch == 0);

    public static bool operator <(ModVersion left, ModVersion right)
    {
      return ((IComparable<ModVersion>)left).CompareTo(right) < 0;
    }

    public static bool operator <=(ModVersion left, ModVersion right)
    {
      return ((IComparable<ModVersion>)left).CompareTo(right) <= 0;
    }

    public static bool operator >(ModVersion left, ModVersion right)
    {
      return ((IComparable<ModVersion>)left).CompareTo(right) > 0;
    }

    public static bool operator >=(ModVersion left, ModVersion right)
    {
      return ((IComparable<ModVersion>)left).CompareTo(right) >= 0;
    }

    public static ModVersion Parse(string raw)
    {
      if (raw == null) return new();

      Regex extractVersion0 = new(@"[^\d]*(\d*)(.*)");
      Regex extractVersion1 = new(@"[^\d]*(\d+)[^\d]*(\d*)(.*)");
      Regex extractVersion2 = new(@"[^\d]*(\d+)[^\d]*(\d+)[^\d]*(\d*)(.*)");

      int dots = raw.Count(ch => ch == '.');

      ModVersion version = new();

      switch (dots)
      {
        case 0:
          {
            var match = extractVersion0.Match(raw);
            if (!match.Success) return new();

            if (!int.TryParse(match.Groups[1].Value, out version.Major))
              return version;

            SetSuffix(match.Groups[2], out version.Suffix);
            break;
          }
        case 1:
          {
            var match = extractVersion1.Match(raw);
            if (!int.TryParse(match.Groups[1].Value, out version.Major))
              return version;
            if (!int.TryParse(match.Groups[2].Value, out version.Minor))
              return version;

            SetSuffix(match.Groups[3], out version.Suffix);

            break;
          }
        case 2:
          {
            var match = extractVersion2.Match(raw);
            if (!int.TryParse(match.Groups[1].Value, out version.Major))
              return version;
            if (!int.TryParse(match.Groups[2].Value, out version.Minor))
              return version;
            if (!int.TryParse(match.Groups[3].Value, out version.Patch))
              return version;

            SetSuffix(match.Groups[4], out version.Suffix);

            break;
          }
        default:
          return new();
      }


      return version;
    }

    private static void SetSuffix(Group group, out string suffix)
    {
      suffix = null;

      if (!group.Success)
      {
        return;
      }

      if (!group.Value.Contains("-") && !group.Value.Contains("alpha"))
      {
        suffix = group.Value;
      }
    }

    public int CompareTo(ModVersion other)
    {
      int c;

      c = Major.CompareTo(other.Major);
      if (c != 0) return c;

      c = Minor.CompareTo(other.Minor);
      if (c != 0) return c;

      c = Patch.CompareTo(other.Patch);
      if (c != 0) return c;

      if (Suffix is null && other.Suffix is null)
        return 0;

      if (Suffix is null && other.Suffix is not null)
        return 1;
      if (Suffix is not null && other.Suffix is null)
        return -1;

      c = Suffix.CompareTo(other.Suffix);
      return c;
    }

    public override string ToString()
    {
      if (!Valid)
        return "-";
      var normal = $"{Major}.{Minor}.{Patch}";
      if (Suffix != default)
        normal += Suffix;
      return normal;
    }

    public override bool Equals(object obj)
    {
      if (obj is ModVersion other)
        return CompareTo(other) == 0;
      return false;
    }

    public bool Equals(ModVersion other)
    {
      return Major == other.Major &&
             Minor == other.Minor &&
             Patch == other.Patch &&
             Suffix == other.Suffix;
    }

    public override int GetHashCode()
    {
      return HashCode.Combine(Major, Minor, Patch, Suffix);
    }

    public static bool operator ==(ModVersion left, ModVersion right)
    {
      return left.Equals(right);
    }

    public static bool operator !=(ModVersion left, ModVersion right)
    {
      return !(left == right);
    }
  }
}

