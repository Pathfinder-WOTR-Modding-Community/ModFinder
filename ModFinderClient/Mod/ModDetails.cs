using System;

namespace ModFinder.Mod
{
  /// <summary>
  /// Current state of the mod.
  /// </summary>
  public class ModStatus
  {
    public ModVersion Version { get; set; }

    public InstallState State { get; set; }

    public bool Installed()
    {
      return State == InstallState.Installed;
    }

    public bool IsVersionBehind(ModVersion latest)
    {
      return latest > Version;
    }
  }

  public enum InstallState
  {
    None,
    Installing,
    Installed,
    Uninstalling
  }

  public struct ChangelogEntry
  {
    public ModVersion version;
    public string contents;

    public ChangelogEntry(ModVersion version, string contents)
    {
      this.version = version;
      this.contents = contents;
    }

    public override bool Equals(object obj)
    {
      return obj is ChangelogEntry other && version.Equals(other.version) && contents == other.contents;
    }

    public override int GetHashCode()
    {
      return HashCode.Combine(version, contents);
    }

    public void Deconstruct(out ModVersion version, out string contents)
    {
      version = this.version;
      contents = this.contents;
    }

    public static implicit operator (ModVersion version, string contents)(ChangelogEntry value)
    {
      return (value.version, value.contents);
    }

    public static implicit operator ChangelogEntry((ModVersion version, string contents) value)
    {
      return new ChangelogEntry(value.version, value.contents);
    }

    public static bool operator ==(ChangelogEntry left, ChangelogEntry right)
    {
      return left.Equals(right);
    }

    public static bool operator !=(ChangelogEntry left, ChangelogEntry right)
    {
      return !(left == right);
    }
  }
}
