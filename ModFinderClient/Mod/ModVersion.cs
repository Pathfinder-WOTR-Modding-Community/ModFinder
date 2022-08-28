using System;
using System.Text.RegularExpressions;

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
    public char Suffix;

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
      if (raw == null) return new ModVersion();
      Regex extractVersion = new(@"[^\d]*(\d+)[^\d]*(\d+)[^\d]*(\d*)(.*)");
      var match = extractVersion.Match(raw);
      ModVersion version = new();
      if (!match.Success)
        return version;
      if (!int.TryParse(match.Groups[1].Value, out version.Major))
        return version;
      if (!int.TryParse(match.Groups[2].Value, out version.Minor))
        return version;
      if (match.Groups[3].Success && match.Groups[3].Length > 0)
        if (!int.TryParse(match.Groups[3].Value, out version.Patch))
          return version;

      if (match.Groups[4].Success && match.Groups[4].Length == 1)
        version.Suffix = match.Groups[4].Value[0];

      return version;
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

