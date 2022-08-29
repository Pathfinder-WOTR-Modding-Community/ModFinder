using ModFinder.Mod;
using ModFinder.UI;
using ModFinder.Util;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;

namespace ModFinder
{
  /// <summary>
  /// Interaction logic for MainWindow.xaml
  /// </summary>
  public partial class MainWindow : Window
  {
    private static readonly ModDatabase ModDB = ModDatabase.Instance;
    private static MasterManifest Manifest;

    public MainWindow()
    {
      InitializeComponent();
      installedMods.DataContext = ModDB;
      showInstalledToggle.DataContext = ModDB;
      showInstalledToggle.Click += ShowInstalledToggle_Click;

#if DEBUG
      using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ModFinder.test_master.json"))
      {
        using var reader = new StreamReader(stream);
        Manifest = IOTool.FromString<MasterManifest>(reader.ReadToEnd());
      }
#else
      using var client = new WebClient();
      var rawstring = client.DownloadString("https://raw.githubusercontent.com/Pathfinder-WOTR-Modding-Community/ModFinder/main/ManifestUpdater/Resources/master_manifest.json");
      Manifest = IOTool.FromString<MasterManifest>(rawstring);
#endif

      installedMods.SelectedCellsChanged += (sender, e) =>
      {
        if (e.AddedCells.Count > 0)
          installedMods.SelectedItem = null;
      };

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
        Close();
      };

      // Drag drop nonsense
      dropTarget.Drop += DropTarget_Drop;
      dropTarget.DragOver += DropTarget_DragOver;
    }

    public static void RefreshAllManifests()
    {
      RefreshGeneratedManifest();
      foreach (var url in Manifest.ExternalManifestUrls)
      {
        using var client = new WebClient();
        var rawstring = client.DownloadString(url);
        RefreshManifest(IOTool.FromString<ModManifest>(rawstring));
      }
    }

    private static void RefreshGeneratedManifest()
    {
      string rawstring;
#if DEBUG
      using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ModFinder.test_generated.json"))
      {
        using var reader = new StreamReader(stream);
        rawstring = reader.ReadToEnd();
      }
#else
      using var client = new WebClient();
      rawstring = client.DownloadString(Manifest.GeneratedManifestUrl);
#endif
      foreach (var manifest in IOTool.FromString<List<ModManifest>>(rawstring))
      {
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
          var infoFile =
            dir.GetFiles().FirstOrDefault(f => f.Name.Equals("info.json", StringComparison.OrdinalIgnoreCase));
          if (infoFile != null)
          {
            var info = IOTool.Read<UMMModInfo>(infoFile.FullName);

            var manifest = ModManifest.ForLocal(info);
            if (!ModDatabase.Instance.TryGet(manifest.Id, out var mod))
            {
              mod = new(manifest);
              ModDatabase.Instance.Add(mod);
            }

            mod.ModDir = dir;
            mod.InstallState = InstallState.Installed;
            mod.InstalledVersion = ModVersion.Parse(info.Version);

            mod.SetRequirements(info.Requirements);
            installedMods.Add(mod.ModId, mod.InstalledVersion);
          }
        }
      }

      // Update dependency state
      foreach (var mod in ModDatabase.Instance.AllMods)
      {
        mod.CheckRequirements(installedMods);
      }
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

    private void ClosePopup_Click(object sender, RoutedEventArgs e)
    {
      DescriptionPopup.IsOpen = false;
    }

    private void DataGridRow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
      DescriptionPopup.IsOpen = false;
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

    private void ProcessIntallResult(InstallResult result)
    {
      if (result.Error != null)
      {
        _ = MessageBox.Show(
          this, "Could not install mod: " + result.Error, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
      }
      else
      {
        RefreshInstalledMods();
      }
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

    private void InstallOrUpdateMod(object sender, RoutedEventArgs e)
    {
      var mod = (sender as Button).Tag as ModViewModel;
      InstallOrUpdateMod(mod);
    }

    private async void InstallOrUpdateMod(ModViewModel mod)
    {
      if (mod.CanInstall)
      {
        var result = await ModInstaller.Install(mod);
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

    private void UninstallMod(object sender, RoutedEventArgs e)
    {
      var mod = (sender as MenuItem).DataContext as ModViewModel;
      ModCache.UninstallAndCache(mod);
      mod.OnUninstalled();
      RefreshInstalledMods();
    }

    private void Filter_TextChanged(object sender, TextChangedEventArgs e)
    {
      ModDB.ApplyFilter((sender as TextBox).Text);
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

        if (DescriptionType == "description")
        {
          try
          {
            BBCodeRenderer.Render(doc, Mod.DescriptionAsText);
          }
          catch (Exception)
          {
            doc.Blocks.Add(new Paragraph(new Run(Mod.DescriptionAsText)));
          }
        }
        else if (DescriptionType == "changelog")
        {
          ChangelogRenderer.Render(doc, Mod);
        }
        else
        {
          doc.Blocks.Add(new Paragraph(new Run("<<<ERROR>>>")));
        }

        return doc;
      }
    }
  }
}
