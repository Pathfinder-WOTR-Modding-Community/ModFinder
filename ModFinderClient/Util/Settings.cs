using Newtonsoft.Json;
using NexusModsNET;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace ModFinder.Util
{
  public class Settings
  {
    public string AutoWrathPath { get; set; }
    public string WrathPath { get; set; }
    public byte[] NexusApiKeyBytes { get; set; }

    [JsonIgnore]
    internal bool? IsKeyActuallyPremium;
    private string GetPlainNexusKey()
    {
      if (NexusApiKeyBytes == null)
      {
        return null;
      }
      var plain = ProtectedData.Unprotect(NexusApiKeyBytes, null, DataProtectionScope.CurrentUser);
      return Encoding.UTF8.GetString(plain);
    }
    public string MaybeGetNexusKey()
    {
      if (!IsKeyActuallyPremium.HasValue || !IsKeyActuallyPremium.Value)
      {
        return null;
      }
      return GetPlainNexusKey();
    }

    private static Settings _Instance;
    public static Settings Load()
    {
      if (_Instance == null)
      {
        if (Main.TryReadFile("Settings.json", out var settingsRaw))
        {
          _Instance = IOTool.FromString<Settings>(settingsRaw);
        }
        else
        {
          _Instance = new();
        }
        Task.Run(_Instance.VerifyNexusPremium);
      }

      return _Instance;
    }

    public void Save()
    {
      IOTool.SafeRun(() =>
      {
        IOTool.Write(this, Main.AppPath("Settings.json"));
      });
    }

    internal async void VerifyNexusPremium()
    {
      var maybeKey = GetPlainNexusKey();
      if (maybeKey != null)
      {
        var client = NexusModsFactory.New(maybeKey, "Modfinder", Main.ProductVersion);
        var user = await client.CreateUserInquirer().GetUserAsync();
        IsKeyActuallyPremium = user.IsPremium;
      }
    }
  }
}
