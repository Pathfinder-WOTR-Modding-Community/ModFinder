using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ModFinder.UI
{
  /// <summary>
  /// Interaction logic for SlideToggle.xaml
  /// </summary>
  public partial class SlideToggle : UserControl
  {
    public SlideToggle()
    {
      InitializeComponent();
    }

    public static readonly DependencyProperty LeftTextProperty = DependencyProperty.Register(
    "LeftText", typeof(string),
    typeof(SlideToggle)
    );

    public static readonly DependencyProperty IsCheckedProperty = DependencyProperty.Register(
    name: "IsChecked", 
    propertyType: typeof(bool),
    ownerType: typeof(SlideToggle),
    typeMetadata: new(OnCheckedChanged)
    );

    private static void OnCheckedChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {

      Debug.WriteLine("Here");

    }


    public string LeftText
    {
      get => GetValue(LeftTextProperty) as string;
      set => SetValue(LeftTextProperty, value);
    }
    public bool IsChecked
    {
      get => (bool)GetValue(IsCheckedProperty);
      set
      {
        if (value == IsChecked) return;
        SetValue(IsCheckedProperty, value);
      }
    }

    private void Grid_MouseUp(object sender, MouseButtonEventArgs e)
    {
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
      e.Handled = true;
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
      if (!IsEnabled) return;

      IsChecked = !IsChecked;
      
    }
  }
}
