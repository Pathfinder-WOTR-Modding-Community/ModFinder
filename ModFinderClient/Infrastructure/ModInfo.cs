namespace ModFinder.Infrastructure
{
  //Mod type, determined by Manifest type, or lack of Manifset
  public enum ModType
  {
    Owlcat = 0,
    UMM = 1,
    Other = 2
  }
  public enum ModSource
  {
    GitHub = 0,
    Nexus = 1,
    Other = 2,
    ModDB = 3
  }
  public class UMMModInfo
  {
    public string Id { get; set; }
    public string Version { get; set; }

    public string Author { get; set; }
    public string DisplayName { get; set; }
    public string HomePage { get; set; }
  }

  public class OwlcatModInfo
  {
    public string UniqueName { get; set; }
    public string Version { get; set; }

    public string Author { get; set; }
    public string DisplayName { get; set; }
    public string HomePage { get; set; }

    public string Description { get; set; }
  }
}
