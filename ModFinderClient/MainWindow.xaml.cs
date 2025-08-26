using ModFinder.Mod;
using ModFinder.UI;
using ModFinder.Util;
using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.WebSockets;
using System.Reflection; // DO NOT REMOVE OR I WILL HURT YOU
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml;
using System.Xml.Linq;

namespace ModFinder
{
  /// <summary>
  /// Interaction logic for MainWindow.xaml
  /// </summary>
  public partial class MainWindow : Window
  {
    private static readonly ModDatabase ModDB = ModDatabase.Instance;
    private static MasterManifest Manifest;
    private static MainWindow Window;

    public MainWindow()
    {
      try
      {
        Logger.Log.Info("Loading main window.");

        InitializeComponent();

        Window = this;

        CheckForUpdate();

        installedMods.DataContext = ModDB;
        showInstalledToggle.DataContext = ModDB;
        showInstalledToggle.Click += ShowInstalledToggle_Click;

#if DEBUGTEST
        Logger.Log.Verbose("Reading test manifest.");
        using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ModFinder.test_master.json"))
        {
          using var reader = new StreamReader(stream);
          Manifest = IOTool.FromString<MasterManifest>(reader.ReadToEnd());
        }
#else
        Logger.Log.Verbose("Fetching remote manifest.");
        var json = HttpHelper.GetResponseContent("https://raw.githubusercontent.com/Pathfinder-WOTR-Modding-Community/ModFinder/main/ManifestUpdater/Resources/master_manifest.json");
        Manifest = IOTool.FromString<MasterManifest>(json);
#endif

        RefreshAllManifests();
        RefreshInstalledMods();

        // Do magic window dragging regardless where they click
        MouseDown += (sender, e) =>
        {
          if (e.ChangedButton == MouseButton.Left)
            DragMove();
        };

        LocationChanged += (sender, e) =>
        {
          double offset = DescriptionPopup.HorizontalOffset;
          DescriptionPopup.HorizontalOffset = offset + 1;
          DescriptionPopup.HorizontalOffset = offset;
        };

        // Close button
        closeButton.Click += (sender, e) =>
        {
          Logger.Log.Dispose();
          Close();
        };

        // Drag drop nonsense
        dropTarget.Drop += DropTarget_Drop;
        dropTarget.DragOver += DropTarget_DragOver;

        ModDB.InitSort();
        
        installedMods.SelectedIndex = 0;
      }
      catch (Exception e)
      {
        Logger.Log.Error($"Failed to initialize main window.", e);
        Close();
      }

      DetailsPanel.SizeChanged += DetailsPanel_SizeChanged;
    }
    private static void SetVersionInHeader(string version)
    {
      Application.Current.Dispatcher.Invoke(() =>
      {
        Window.Header.Text += $" - {version}";
      });
    }

    private static void CheckForUpdate()
    {
      Task.Run(
          async () =>
          {
            using WebClient client = new();
            client.Headers.Add("User-Agent", "ModFinder");
            var raw =
              await client.DownloadStringTaskAsync(
                "https://api.github.com/repos/Pathfinder-WOTR-Modding-Community/ModFinder/releases/latest");
            return JsonSerializer.Deserialize<JsonElement>(raw);
          })
        .ContinueWith(
          t =>
          {
            try
            {
              var json = t.Result;
              if (json.TryGetProperty("tag_name", out var tag))
              {
                long latest = ParseVersion(tag.GetString()[1..]);
                SetVersionInHeader(Main.ProductVersion);
                if (Main.ProductVersion.Contains("-dev"))
                {
                  return;
                }

                if (latest > ParseVersion(Main.ProductVersion))
                {
                  if (MessageBox.Show(
                    Window,
                    $"A newer version is available: ({tag}). Would you like to download it now?",
                    "Update Available",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information) == MessageBoxResult.Yes)
                  {
                    Process.Start(
                      "explorer", json.GetProperty("assets")[0].GetProperty("browser_download_url").GetString());
                  }
                }
              }
            }
            catch (Exception ex)
            {
              Logger.Log.Error("Failed to check for updates.", ex);
            }
          },
          TaskScheduler.FromCurrentSynchronizationContext());
    }

    private static long ParseVersion(string v)
    {
      v = Regex.Replace(v, "-rel.*","");
      var c = v.Split('.');
      return int.Parse(c[0]) * 65536 + int.Parse(c[1]) * 256 + int.Parse(c[2]);
    }

    #region Manifest / Local Mod Scanning
    public static void RefreshAllManifests()
    {
      try
      {
        RefreshGeneratedManifest();
        foreach (var url in Manifest.ExternalManifestUrls)
        {
          Logger.Log.Verbose($"Loading manifest from external URL: {url}");
          var json = HttpHelper.GetResponseContent(url);
          RefreshManifest(IOTool.FromString<ModManifest>(json));
        }
      }
      catch (Exception e)
      {
        Logger.Log.Error($"Failed to refresh manifests.", e);
      }
    }

    private static void RefreshGeneratedManifest()
    {
      string rawstring;
#if DEBUGTEST
      using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ModFinder.test_generated.json"))
      {
        using var reader = new StreamReader(stream);
        rawstring = reader.ReadToEnd();
      }
#else
      rawstring = HttpHelper.GetResponseContent(Manifest.GeneratedManifestUrl);
#endif
      foreach (var manifest in IOTool.FromString<List<ModManifest>>(rawstring))
      {
        Logger.Log.Verbose($"Loading manifest for {manifest.Name}");
        RefreshManifest(manifest);
      }
    }

    private static void RefreshManifest(ModManifest manifest)
    {
      if (ModDB.TryGet(manifest.Id, out var viewModel))
        viewModel.Refresh(manifest);
      else
        ModDB.Add(new(manifest));
    }

    public static void RefreshInstalledMods()
    {
      IOTool.SafeRun(CheckInstalledModsInternal);
      IOTool.SafeRun(CheckUMMState);
    }

    private static void CheckInstalledModsInternal()
    {
      try
      {
        foreach (var mod in ModDatabase.Instance.AllMods)
        {
          // Reset install state to make sure we capture any that were, for example, uninstalled but not updated.
          mod.InstallState = InstallState.None;
          mod.InstalledVersion = default;
        }

        var installedMods = new Dictionary<ModId, ModVersion>();
        var ummBase = ModInstaller.GetModPath(ModType.UMM);
        var ummDir = Directory.Exists(ummBase) ? Directory.GetDirectories(ummBase) : Array.Empty<string>();
        foreach (var dir in ummDir)
        {
          Logger.Log.Info($"Found installed mod: {dir}");
          var mod = ProcessUmmModDir(new(dir));
          if (mod is not null)
            installedMods.Add(mod.ModId, mod.InstalledVersion);
        }

        var owlcatBase = ModInstaller.GetModPath(ModType.Owlcat);
        var owlDir = Directory.Exists(owlcatBase) ? Directory.GetDirectories(owlcatBase) : Array.Empty<string>();
        foreach (var dir in owlDir)
        {
          Logger.Log.Info($"Found installed mod: {dir}");
          var mod = ProcessOwlModDirectory(new(dir));
          if (mod is not null)
            installedMods.Add(mod.ModId, mod.InstalledVersion);
        }

        // Update DB to allow installing cached local mods and support rollback
        if (Directory.Exists(ModCache.CacheDir))
        {
          foreach (var dir in Directory.GetDirectories(ModCache.CacheDir))
          {
            Logger.Log.Info($"Found cached mod: {dir}");
            ProcessUmmModDir(new(dir), updateStatus: false);
          }
        }

        // Update dependency state
        foreach (var mod in ModDatabase.Instance.AllMods)
        {
          mod.CheckRequirements(installedMods);
        }
      }
      catch (Exception e)
      {
        Logger.Log.Error($"Failed to check installed mods.", e);
      }
    }

    private static void CheckUMMState()
    {
      try
      {
        XElement ummParams = XElement.Load(Main.UMMParamsPath);
        IEnumerable<(string id, bool enabled)> mods =
          ummParams.Descendants("Mod")
            .Select(x => (x.Attribute("Id").Value, bool.Parse(x.Attribute("Enabled").Value)));

        foreach (var mod in ModDatabase.Instance.Installed)
        {
          if (mod.Type != ModType.UMM)
            continue;

          var modConfig = mods.FirstOrDefault(m => mod.ModId.Id == m.id);
          if (modConfig == default)
            mod.Enabled = true;
          else
            mod.Enabled = modConfig.enabled;
        }
      }
      catch (Exception e)
      {
        Logger.Log.Error("Failed to check UMM state. Make sure UMM is installed and launch WotR once.", e);
      }
    }

    private static ModViewModel ProcessOwlModDirectory(DirectoryInfo modDir, bool updateStatus = true)
    {
      var infoFile = 
        modDir.GetFiles().FirstOrDefault(f => f.Name.Equals("OwlcatModificationManifest.json", StringComparison.OrdinalIgnoreCase));
      if (infoFile != null)
      {
        var info = IOTool.Read<OwlcatModInfo>(infoFile.FullName);

        var manifest = ModManifest.ForLocal(info);
        if (manifest is null)
        {
          Logger.Log.Warning($"Invalid manifest: {infoFile}");
          return null;
        }

        if (!ModDatabase.Instance.TryGet(manifest.Id, out var mod))
        {
          mod = new(manifest);
          ModDatabase.Instance.Add(mod);
        }

        if (updateStatus)
        {
          mod.ModDir = modDir;
          mod.InstallState = InstallState.Installed;
          mod.InstalledVersion = ModVersion.Parse(info.Version);

          mod.Enabled = Main.OwlcatMods.Has(info.UniqueName);
          
          // set WrathPatches for a requirement for Owlmods
          mod.SetRequirements(["WrathPatches"]);
        }
        return mod;
      }
      return null;
    }

    private static ModViewModel ProcessUmmModDir(DirectoryInfo modDir, bool updateStatus = true)
    {
      var infoFile =
        modDir.GetFiles().FirstOrDefault(f => f.Name.Equals("info.json", StringComparison.OrdinalIgnoreCase));
      if (infoFile != null)
      {
        var info = IOTool.Read<UMMModInfo>(infoFile.FullName);

        var manifest = ModManifest.ForLocal(info);
        if (manifest is null)
        {
          Logger.Log.Warning($"Invalid manifest: {infoFile}");
          return null;
        }

        if (!ModDatabase.Instance.TryGet(manifest.Id, out var mod))
        {
          mod = new(manifest);
          ModDatabase.Instance.Add(mod);
        }

        if (updateStatus)
        {
          mod.ModDir = modDir;
          mod.InstallState = InstallState.Installed;
          mod.InstalledVersion = ModVersion.Parse(info.Version);

          mod.SetRequirements(info.Requirements);
        }
        return mod;
      }
      return null;
    }

    public static bool CheckIsMod(string path)
    {
      if (!File.Exists(path))
        return false;

      //if (Path.GetExtension(path) != ".zip")
      //  return false;
      return true;
      //Cant have anything below this point if we want portraits
      /* using var opened = ZipFile.OpenRead(path);
       return
         opened.Entries.Any(
           a =>
             a.Name.Equals("OwlcatModificationManifest.json", StringComparison.OrdinalIgnoreCase)
             || a.Name.Equals("Info.json", StringComparison.OrdinalIgnoreCase));*/
    }
    #endregion

    #region Install / Rollback / Uninstall / Enable / Disable
    public static void SetModEnabled(ModViewModel modVM, bool enabled)
    {
      try
      {
        var id = modVM.ModId;
        if (modVM.Type == ModType.UMM)
        {
          XDocument ummParams = XDocument.Load(Main.UMMParamsPath);
          var mod = ummParams.Descendants("Mod").FirstOrDefault(x => id.Id.Equals(x.Attribute("Id").Value));

          var enabledString = enabled ? "true" : "false";
          if (mod is null)
          {
            ummParams.Descendants("ModParams").First().Add(
              XElement.Parse($"<Mod Id=\"{id.Id}\" Enabled=\"{enabledString}\" />"));
          }
          else
          {
            mod.SetAttributeValue("Enabled", enabledString);
          }
          ummParams.Save(Main.UMMParamsPath);
        }
        else if (modVM.Type == ModType.Owlcat)
        {
          if (enabled)
            Main.OwlcatMods.Add(id.Id);
          else
            Main.OwlcatMods.Remove(id.Id);
        }
      }
      catch (Exception e)
      {
        Logger.Log.Error("Failed to update UMM state. Make sure UMM is installed and launch WotR once.", e);
      }
    }

    private void ProcessIntallResult(InstallResult result)
    {
      if (result.Error != null)
      {
        Logger.Log.Error($"Failed to install: {result.Error}");
        _ = MessageBox.Show(
          this, "Could not install mod: " + result.Error, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
      }
      else
      {
        RefreshInstalledMods();
      }
    }

    private async void InstallOrUpdateMod(ModViewModel mod)
    {
      try
      {
        if (mod.CanInstall)
        {
          var isUpdate = mod.IsInstalled;
          mod.InstallState = InstallState.Installing;
          var result = await ModInstaller.Install(mod, isUpdate);
          ProcessIntallResult(result);
        }
        else if (mod.CanDownload)
        {
          OpenUrl(mod.Latest.Url);
        }
        else
        {
          var nextMod = mod.GetNextAvailableRequirement();
          if (nextMod is not null)
          {
            mod.InstallState = InstallState.Installing;
            InstallOrUpdateMod(nextMod);
          }
          else
          {
            _ = MessageBox.Show(
              this,
              "Could not install mod: not available for install or download",
              "Error",
              MessageBoxButton.OK,
              MessageBoxImage.Error);
          }
        }
      }
      catch (Exception e)
      {
        Logger.Log.Error("Install/update failed.", e);
      }
    }

    private async static Task Rollback(ModViewModel mod)
    {
      if (!ModCache.IsCached(mod.ModId))
        throw new InvalidOperationException("Cannot rollback mod without a cached copy");

      await ModCache.Uninstall(mod, cache: false); // Don't cache, just delete it!
      await ModCache.TryRestoreMod(mod.ModId);
      RefreshInstalledMods();
    }

    private static async Task Uninstall(ModViewModel mod)
    {
      await ModCache.Uninstall(mod);
      mod.OnUninstalled();
      RefreshInstalledMods();
    }
    #endregion

    #region Show Dialogs & Open Web Pages
    public static void ShowError(string message)
    {
      if (Window is not null)
      {
        _ = MessageBox.Show(
          Window,
          $"{message} Check the log at {Logger.LogFile} for more details.",
          "Error",
          MessageBoxButton.OK,
          MessageBoxImage.Error);
      }
    }

    private void ShowPopup(ModViewModel mod, string contentType)
    {
      var proxy = new DescriptionProxy(mod, contentType);
      DescriptionPopup.DataContext = proxy;
      var contents = DescriptionPopup.FindName("Contents") as FlowDocumentScrollViewer;
      contents.Document = proxy.Render();
      DescriptionPopup.IsOpen = true;
    }
    
    private static void OpenUrl(string url)
    {
      Process.Start(
        new ProcessStartInfo
        {
          FileName = url,
          UseShellExecute = true
        });
    }
    #endregion

    public class DescriptionProxy
    {
      private readonly ModViewModel Mod;
      private readonly string DescriptionType;

      public DescriptionProxy(ModViewModel mod, string descriptionType)
      {
        Mod = mod;
        DescriptionType = descriptionType;
      }

      public string Name => Mod.Name + "   (" + Mod.InstalledVersion.ToString() + ")";

      internal FlowDocument Render()
      {
        var doc = new FlowDocument();

        switch (DescriptionType)
        {
          case "description":
            try
            {
              if (Mod.Manifest.Service.IsNexus())
                BBCodeRenderer.Render(doc, Mod.DescriptionAsText);
              else
                MarkdownRenderer.Render(doc, Mod.Description);
            }
            catch (Exception ex)
            {
              Logger.Log.Error("rendering description", ex);
              doc.Blocks.Add(new Paragraph(new Run(Mod.DescriptionAsText)));
            }
            break;

          case "changelog":
            try
            {
              ChangelogRenderer.Render(doc, Mod);
            }
            catch (Exception e)
            {
              Logger.Log.Error("Changelog rendering failed.", e);
            }
            break;

          default:
            doc.Blocks.Add(new Paragraph(new Run("<<<ERROR>>>")));

            break;
        }

        return doc;
      }
    }

    #region UI Event Handlers

    private void InstallOrUpdateMod(object sender, RoutedEventArgs e)
    {
      var mod = (sender as Button).Tag as ModViewModel;
      InstallOrUpdateMod(mod);
    }

    private async void UninstallMod(object sender, RoutedEventArgs e)
    {
      try
      {
        var mod = (sender as FrameworkElement).DataContext as ModViewModel;
        await Uninstall(mod);
      }
      catch (Exception ex)
      {
        ShowError("Uninstall failed.");
        Logger.Log.Error("Uninstall failed.", ex);
      }
    }

    private async void Rollback(object sender, RoutedEventArgs e)
    {
      try
      {
        var mod = (sender as FrameworkElement).DataContext as ModViewModel;
        await Rollback(mod);
      }
      catch (Exception ex)
      {
        Logger.Log.Error("Rollback failed.", ex);
      }
    }

    private void OpenHomepage(object sender, RoutedEventArgs e)
    {
      var mod = (sender as FrameworkElement).DataContext as ModViewModel;
      OpenUrl(mod.HomepageUrl);
    }

    private void ShowModDescription(object sender, RoutedEventArgs e)
    {
      var mod = (sender as FrameworkElement).DataContext as ModViewModel;
      ShowPopup(mod, "description");
    }

    private void ShowModChangelog(object sender, RoutedEventArgs e)
    {
      var mod = (sender as FrameworkElement).DataContext as ModViewModel;
      ShowPopup(mod, "changelog");
    }

    private void DetailsPanel_SizeChanged(object sender, SizeChangedEventArgs e)
    {
      using var g = DetailsPanelBackground.Open();
      var bg = Resources["details-bg-border"] as Image;
      g.PushOpacity(0.8);
      g.DrawImage(bg.Source, new(0, 0, e.NewSize.Width, bg.Source.Height));

      g.PushTransform(new ScaleTransform(1, -1));
      g.DrawImage(bg.Source, new(0, -e.NewSize.Height, e.NewSize.Width, bg.Source.Height));
      g.Pop();
    }

    private void ClosePopup_Click(object sender, RoutedEventArgs e)
    {
      DescriptionPopup.IsOpen = false;
    }

    private void DataGridRow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
      DescriptionPopup.IsOpen = false;

      if (sender is not DataGridRow row)
      {
        return;
      }

      installedMods.SelectedIndex = row.GetIndex();
    }

    private void DataGridRow_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
      DescriptionPopup.IsOpen = false;
    }

    private void DropTarget_DragOver(object sender, DragEventArgs e)
    {
      e.Effects = DragDropEffects.None;
      if (e.Data.GetFormats().Any(f => f == DataFormats.FileDrop))
      {
        string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
        if (files.All(f => CheckIsMod(f)))
        {
          e.Effects = DragDropEffects.Copy;
        }
      }
      e.Handled = true;
    }

    private async void DropTarget_Drop(object sender, DragEventArgs e)
    {
      var files = (string[])e.Data.GetData(DataFormats.FileDrop);
      foreach (var f in files)
      {
        var result = await ModInstaller.InstallFromZip(f);
        ProcessIntallResult(result);
      }
    }

    private void ShowInstalledToggle_Click(object sender, RoutedEventArgs e)
    {
      var togglebutton = sender as ToggleButton;
      ModDB.ShowInstalled = togglebutton.IsChecked ?? false;
    }

    private void MoreOptions_Click(object sender, RoutedEventArgs e)
    {
      var button = sender as Button;
      button.ContextMenu.DataContext = button.Tag;
      button.ContextMenu.StaysOpen = true;
      button.ContextMenu.IsOpen = true;
    }

    private void LookButton_Click(object sender, RoutedEventArgs e)
    {
      try
      {
        RefreshAllManifests();
        RefreshInstalledMods();
        CheckUMMState();
      }
      catch (Exception ex)
      {
        Logger.Log.Error("Failed to scan local mods.", ex);
      }
    }

    private void Filter_TextChanged(object sender, TextChangedEventArgs e)
    {
      ModDB.ApplyFilter((sender as TextBox).Text);
    }

    private void OpenFolder(object sender, RoutedEventArgs e)
    {
      try
      {
        var mod = (sender as FrameworkElement).DataContext as ModViewModel;
        string folder;
        if (mod.Type == ModType.Portrait)
        {
          folder = "";
        }
        else
        {
          folder = $"\"{Path.Combine(ModInstaller.GetModPath(mod.Type), mod.ModDir.Name)}\\\"";
        }
        Logger.Log.Info($"Opening folder: {folder}");
        // If you don't point to explorer process you get access denied error
        Process.Start(Environment.GetEnvironmentVariable("WINDIR") + @"\explorer.exe", folder);
      }
      catch (Exception ex)
      {
        Logger.Log.Error("Unable to open folder.", ex);
      }
    }

    private void DetailsPanel_MouseDown(object sender, MouseButtonEventArgs e)
    {
      DescriptionPopup.IsOpen = false;
    }

    private void OpenHyperlink(object sender, ExecutedRoutedEventArgs e)
    {
      Process.Start("explorer.exe", e.Parameter.ToString());
    }

    private void ClickOnImage(object sender, ExecutedRoutedEventArgs e)
    {

    }

    // When OnSort is called the direction isn't indicated so this tracks it.
    private static readonly Dictionary<SortColumn, bool> InvertSort =
      new()
      {
        { SortColumn.Author, false },
        { SortColumn.Status, true }, // On init status is the default sort so the next click should invert
      };
    private void OnSort(object sender, DataGridSortingEventArgs e)
    {
      var dataGrid = sender as DataGrid;
      var collectionView = (ListCollectionView)CollectionViewSource.GetDefaultView(dataGrid.ItemsSource);
      switch (e.Column.DisplayIndex)
      {
        case -1: // Fake for goto
          e.Handled = true;
          break;
        case 2: // Author
          collectionView.CustomSort = new ModSort(SortColumn.Author, InvertSort[SortColumn.Author]);
          InvertSort[SortColumn.Author] = !InvertSort[SortColumn.Author];
          goto case -1;
        case 3: // Status
          collectionView.CustomSort = new ModSort(SortColumn.Status, InvertSort[SortColumn.Status]);
          InvertSort[SortColumn.Status] = !InvertSort[SortColumn.Status];
          goto case -1;
      }
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
      var button = sender as Button;
      button.ContextMenu.DataContext = button.Tag;
      button.ContextMenu.StaysOpen = true;
      button.ContextMenu.IsOpen = true;
    }
    private async void TryAquireNexusAPIKeyAsync()
    {

      using var ws = new ClientWebSocket();
      using var cts = new CancellationTokenSource();
      await ws.ConnectAsync(new Uri("wss://sso.nexusmods.com"), cts.Token);

      var uuid = Guid.NewGuid().ToString();
      string token = null;
      var payload = JsonSerializer.Serialize(new { id = uuid, token, protcol = 2 });
      await ws.SendAsync(Encoding.UTF8.GetBytes(payload), WebSocketMessageType.Text, true, cts.Token);


      var authUrl = $"https://www.nexusmods.com/sso?id={Uri.EscapeDataString(uuid)}&application={Uri.EscapeDataString(Main.NexusSlug)}";

      Process.Start(new ProcessStartInfo { FileName = authUrl, UseShellExecute = true });
      var buffer = ArrayPool<byte>.Shared.Rent(32 * 1024);
      try
      {
        while (ws.State == WebSocketState.Open && !cts.IsCancellationRequested)
        {
          var seg = new ArraySegment<byte>(buffer);
          WebSocketReceiveResult result;
          var sb = new StringBuilder();
          do
          {
            result = await ws.ReceiveAsync(seg, cts.Token);
            if (result.MessageType == WebSocketMessageType.Close)
            {
              await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "closing", cts.Token);
              break;
            }

            sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
          } while (!result.EndOfMessage);
          if (sb.Length == 0)
          {
            continue;
          }

          /* Nexus API docs say I should get a response. Reality says I get the API key directly
          var t = new { success = true, data = new { api_key = "" }, error = "" };
          var msg = Newtonsoft.Json.JsonConvert.DeserializeAnonymousType(sb.ToString(), t);
          if (msg.success)
          {
            if (msg.data?.api_key is string key)
            {
              var bytes = Encoding.UTF8.GetBytes(key);
              Main.Settings.NexusApiKeyBytes = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
              break;
            }
          }
          else
          {
            var err = msg.error ?? "Unknown SSO error";
            throw new Exception($"SSO error: {err}");
          }
          */
          try
          {
            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            Main.Settings.NexusApiKeyBytes = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
            Main.Settings.Save();
            break;
          }
          catch (Exception ex)
          {
            throw new Exception($"Error trying to process SSO result:\n{ex}");
          }
        }
      }
      catch (Exception ex)
      {
        Logger.Log.Error("Failed to link Nexus Premium Account.", ex);
      }
      finally
      {
        ArrayPool<byte>.Shared.Return(buffer);
        if (ws.State == WebSocketState.Open)
        {
          await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None);
        }
      }
    }
    private void LinkNexusPremium(object sender, RoutedEventArgs e)
    {
      try
      {
        TryAquireNexusAPIKeyAsync();
      }
      catch (Exception ex)
      {
        Logger.Log.Error("Failed to link Nexus Premium Account.", ex);
      }
    }
    private void ClearCache(object sender, RoutedEventArgs e)
    {
      try
      {
        ModCache.Clear();
        RefreshInstalledMods();
      }
      catch (Exception ex)
      {
        Logger.Log.Error("Failed to clear cache.", ex);
      }
    }

    private void ShowHidden(object sender, RoutedEventArgs e)
    {

    }

    private void HideInstalled(object sender, RoutedEventArgs e)
    {

    }
  }
  #endregion
}