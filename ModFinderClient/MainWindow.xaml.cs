using ModFinder.Mod;
using ModFinder.UI;
using ModFinder.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Reflection; // DO NOT REMOVE OR I WILL HURT YOU
using System.Windows.Media;

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

        if (installedMods.Items.Count > 0)
          installedMods.SelectedIndex = 0;
      }
      catch (Exception e)
      {
        ShowError("Failed to start.");
        Logger.Log.Error($"Failed to initialize main window.", e);
        Close();
      }

      DetailsPanel.SizeChanged += DetailsPanel_SizeChanged;
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
      IOTool.Safe(() => CheckInstalledModsInternal());
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
        var modDir = Main.WrathPath.GetDirectories("Mods");
        if (modDir.Length > 0)
        {
          foreach (var dir in modDir[0].GetDirectories())
          {
            Logger.Log.Info($"Found installed mod: {dir.Name}");
            var mod = ProcessModDirectory(dir);
            if (mod is not null)
              installedMods.Add(mod.ModId, mod.InstalledVersion);
          }
        }

        // Update DB to allow installing cached local mods and support rollback
        if (Directory.Exists(ModCache.CacheDir))
        {
          foreach (var dir in Directory.GetDirectories(ModCache.CacheDir))
          {
            Logger.Log.Info($"Found cached mod: {dir}");
            ProcessModDirectory(new(dir), updateStatus: false);
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

    private static ModViewModel ProcessModDirectory(DirectoryInfo modDir, bool updateStatus = true)
    {
      var infoFile =
        modDir.GetFiles().FirstOrDefault(f => f.Name.Equals("info.json", StringComparison.OrdinalIgnoreCase));
      if (infoFile != null)
      {
        var info = IOTool.Read<UMMModInfo>(infoFile.FullName);

        var manifest = ModManifest.ForLocal(info);
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

      if (Path.GetExtension(path) != ".zip")
        return false;

      using var opened = ZipFile.OpenRead(path);
      return
        opened.Entries.Any(
          a =>
            a.Name.Equals("OwlcatModificationManifest.json", StringComparison.OrdinalIgnoreCase)
            || a.Name.Equals("Info.json", StringComparison.OrdinalIgnoreCase));
    }
    #endregion

    #region Install
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

    private void InstallOrUpdateMod(object sender, RoutedEventArgs e)
    {
      var mod = (sender as Button).Tag as ModViewModel;
      InstallOrUpdateMod(mod);
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
        ShowError("Installation failed.");
        Logger.Log.Error("Install/update failed.", e);
      }
    }

    private void UninstallMod(object sender, RoutedEventArgs e)
    {
      try
      {
        var mod = (sender as MenuItem).DataContext as ModViewModel;
        ModCache.Uninstall(mod);
        mod.OnUninstalled();
        RefreshInstalledMods();
      }
      catch (Exception ex)
      {
        ShowError("Uninstall failed.");
        Logger.Log.Error("Uninstall failed.", ex);
      }
    }

    private void Rollback(object sender, RoutedEventArgs e)
    {
      try
      {
        var mod = (sender as MenuItem).DataContext as ModViewModel;

        if (!ModCache.IsCached(mod.ModId))
          throw new InvalidOperationException("Cannot rollback mod without a cached copy");

        ModCache.Uninstall(mod, cache: false); // Don't cache, just delete it!
        ModCache.TryRestoreMod(mod.ModId);
        RefreshInstalledMods();
      }
      catch (Exception ex)
      {
        ShowError("Rollback failed.");
        Logger.Log.Error("Rollback failed.", ex);
      }
    }
    #endregion

    #region Show Dialogs & Open Web Pages
    public static void ShowError(string message)
    {
      _ = MessageBox.Show(
        Window,
        $"{message} Check the log at {Logger.LogFile} for more details.",
        "Error",
        MessageBoxButton.OK,
        MessageBoxImage.Error);
    }

    private void ShowModDescription(object sender, RoutedEventArgs e)
    {
      var mod = (sender as MenuItem).DataContext as ModViewModel;
      ShowPopup(mod, "description");
    }

    private void ShowModChangelog(object sender, RoutedEventArgs e)
    {
      var mod = (sender as MenuItem).DataContext as ModViewModel;
      ShowPopup(mod, "changelog");
    }

    private void ShowPopup(ModViewModel mod, string contentType)
    {
      var proxy = new DescriptionProxy(mod, contentType);
      DescriptionPopup.DataContext = proxy;
      var contents = DescriptionPopup.FindName("Contents") as FlowDocumentScrollViewer;
      contents.Document = proxy.Render();
      DescriptionPopup.IsOpen = true;
    }

    private void OpenHomepage(object sender, RoutedEventArgs e)
    {
      var mod = (sender as MenuItem).DataContext as ModViewModel;
      OpenUrl(mod.HomepageUrl);
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

    #region UI Event Handlers
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

    private void DataGridCell_Clicked(object sender, MouseButtonEventArgs e)
    {
      Debug.WriteLine("Hello");
      //DescriptionPopup.IsOpen = false;
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
      RefreshAllManifests();
      RefreshInstalledMods();
    }

    private void Filter_TextChanged(object sender, TextChangedEventArgs e)
    {
      ModDB.ApplyFilter((sender as TextBox).Text);
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
              BBCodeRenderer.Render(doc, Mod.DescriptionAsText);
            }
            catch (Exception)
            {
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
              ShowError("Changelog rendering failed.");
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
  }
}