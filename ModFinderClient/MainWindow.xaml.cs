using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using ModFinder.Infrastructure;
using System;
using System.Text;
using System.Windows.Documents;
using System.Windows.Media;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Net;

namespace ModFinder
{

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly ModDatabase modListData = ModDatabase.Instance;

        public MainWindow()
        {
            InitializeComponent();
            installedMods.DataContext = modListData;
            showInstalledToggle.DataContext = modListData;
            showInstalledToggle.Click += ShowInstalledToggle_Click;

#if DEBUG
            var manifest = ModFinderIO.Read<ModListBlob>(Environment.GetEnvironmentVariable("MODFINDER_LOCAL_MANIFEST"));
#else
            using var client = new System.Net.WebClient();
            var rawstring = client.DownloadString("https://raw.githubusercontent.com/BarleyFlour/ModFinder_WOTR/master/ManifestUpdater/Resources/master_manifest.json");
            var manifest = ModFinderIO.FromString<ModListBlob>(rawstring);
#endif

            installedMods.SelectedCellsChanged += (sender, e) =>
            {
                if (e.AddedCells.Count > 0)
                    installedMods.SelectedItem = null;
            };

            foreach (var mod in manifest.m_AllMods)
                modListData.Add(new(mod));

            ModInstall.ParseInstalledMods();


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


        public static bool CheckIsMod(string path)
        {
            if (!File.Exists(path))
                return false;

            if (System.IO.Path.GetExtension(path) != ".zip")
                return false;

            //BARLEY CODE HERE
            using var opened = ZipFile.OpenRead(path);
            return opened.Entries.Any(a => a.Name.Equals("OwlcatModificationManifest.json", StringComparison.OrdinalIgnoreCase) || a.Name.Equals("Info.json", StringComparison.OrdinalIgnoreCase));
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
            if (result.Complete)
                result.Mod.State = ModState.Installed;
            else if (result.Mod != null)
                result.Mod.State = ModState.NotInstalled;

            if (result.Error != null)
            {
                _ = MessageBox.Show(this, "Could not install mod: " + result.Error, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void DropTarget_Drop(object sender, DragEventArgs e)
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            foreach (var f in files)
            {
                var result = await ModInstall.InstallFromZip(f);
                ProcessIntallResult(result);
            }
        }

        private async void InstallOrUpdateMod(object sender, RoutedEventArgs e)
        {
            var toInstall = (sender as Button).Tag as ModDetails;
            toInstall.State = ModState.Installing;

            var result = await ModInstall.InstallMod(toInstall);
            ProcessIntallResult(result);

        }


        private void ShowInstalledToggle_Click(object sender, RoutedEventArgs e)
        {
            var togglebutton = sender as ToggleButton;
            modListData.ShowInstalled = togglebutton.IsChecked ?? false;
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
            ModInstall.ParseInstalledMods();
        }

        private void ShowModDescription(object sender, RoutedEventArgs e)
        {
            var mod = (sender as MenuItem).DataContext as ModDetails;
            ShowPopup(mod, "description");
        }

        private void ShowModChangelog(object sender, RoutedEventArgs e)
        {
            var mod = (sender as MenuItem).DataContext as ModDetails;
            ShowPopup(mod, "changelog");
        }

        private void ShowPopup(ModDetails mod, string contentType)
        {
            var proxy = new DescriptionProxy(mod, contentType);
            DescriptionPopup.DataContext = proxy;
            var contents = DescriptionPopup.FindName("Contents") as FlowDocumentScrollViewer;
            contents.Document = proxy.Render();
            DescriptionPopup.IsOpen = true;
        }
    }

    public class DescriptionProxy
    {
        private readonly ModDetails Mod;
        private readonly string DescriptionType;

        public DescriptionProxy(ModDetails mod, string descriptionType)
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
