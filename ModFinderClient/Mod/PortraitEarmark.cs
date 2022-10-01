using System;

namespace ModFinder.Mod
{
  [Serializable]
  public struct PortraitEarmark
  {
    public string ModID; //Mod ID of the portrait mod from manifest.

    public PortraitEarmark(string modId)
    {
      ModID = modId;
    }
  }
}