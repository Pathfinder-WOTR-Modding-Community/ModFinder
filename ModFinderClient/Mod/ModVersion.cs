using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks.Dataflow;

namespace ModFinder.Mod
{
  /// <summary>
  /// Represents a mod version.
  /// </summary>
  /// 
  /// <remarks>
  /// Format is any amount of integers separated by dots '.' with an optional char suffix e.x. X.Y.Zd Where <c>X, Y, Z</c> are integers.
  /// <c>d</c> is a single character used to further indicate a patch version.
  /// </remarks>
  public struct ModVersion : IComparable<ModVersion>, IEquatable<ModVersion>
  {
    public int[] VersionNumbers; // Ordered by importance, i.e. Major first then Minor, Patch, etc.
    public string Suffix;

    public bool Valid
    {
      get
      {
        if (VersionNumbers == null) return false; // VersionNumbers can be false, and that causes a NullReferenceException in the manifest updater.
        return VersionNumbers.Any(i => i != 0);
      }
    }

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
      if (raw == null)
      {
        var emptyVersion = new ModVersion();
        emptyVersion.VersionNumbers = new int[] { };
        return emptyVersion;
      }

      if (raw.StartsWith("v")) raw = raw[1..];

      Regex suffixRegex = new(@"(\d+)*(\D+)");

      ModVersion version = new();
      var rawStrings = raw.Split('.');
      version.VersionNumbers = new int[rawStrings.Length];

      for (int i = 0; i < rawStrings.Length; i++)
      {
        if (int.TryParse(rawStrings[i], out int versionNum))
          version.VersionNumbers[i] = versionNum;

        else if (i == rawStrings.Length - 1)
        {
          var
            match = suffixRegex.Match(
              rawStrings[i]); // we have 3 groups, first is full string, second is digits, third is letters. (i'm not very good at regex)
          if (int.TryParse(match.Groups[1].Value, out int versionNum2))
            version.VersionNumbers[i] = versionNum2;
          SetSuffix(match.Groups[2], out version.Suffix);
        }
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
      if (this.VersionNumbers is null && other.VersionNumbers is null)
        return 0;
      
      if (this.VersionNumbers is not null && other.VersionNumbers is null)
        return 1;
      if (this.VersionNumbers is null && other.VersionNumbers is not null)
        return -1;

      var longestLength = this.VersionNumbers.Length > other.VersionNumbers.Length
        ? this.VersionNumbers.Length
        : other.VersionNumbers.Length;

      int c;

      for (int i = 0; i < longestLength; i++)
      {
        var thisVersion = this.VersionNumbers.Length < i + 1 ? 0 : this.VersionNumbers[i];
        var otherVersion = other.VersionNumbers.Length < i + 1 ? 0 : other.VersionNumbers[i];
        c = thisVersion.CompareTo(otherVersion);
        if (c != 0) return c;
      }

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
      var stringBuilder = new StringBuilder(VersionNumbers.Length + (Suffix == "" ? 0 : 1));
      int noDotNumber = VersionNumbers.Length - 1; // so we dont do the same computation each iteration.
      for (int i = 0; i < VersionNumbers.Length; i++)
      {
        stringBuilder.Append(VersionNumbers[i]);
        if (i != noDotNumber)
          stringBuilder.Append('.');
      }

      if (Suffix != default)
        stringBuilder.Append(Suffix);
      return stringBuilder.ToString();
    }

    public override bool Equals(object obj)
    {
      if (obj is ModVersion other)
        return CompareTo(other) == 0;
      return false;
    }

    public bool Equals(ModVersion other)
    {
      if (this.VersionNumbers == null && other.VersionNumbers == null)
        return false;
      if (this.VersionNumbers == null || other.VersionNumbers == null)
        return false;
      return this.VersionNumbers.SequenceEqual(other.VersionNumbers) && this.Suffix == other.Suffix;
    }

    public override int GetHashCode()
    {
      var stringBuilder = new StringBuilder(capacity: VersionNumbers.Length);
      foreach (int i in VersionNumbers)
      {
        stringBuilder.Append(i);
      }

      return HashCode.Combine(stringBuilder.ToString(), Suffix); // Would just using VersionNumbers here be sufficient?
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