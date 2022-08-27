using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;

namespace ModFinder.UI
{
  public class NonTopPopup : Popup
  {
    protected override void OnOpened(EventArgs e)
    {
      var hwnd = ((HwndSource)PresentationSource.FromVisual(Child)).Handle;

      if (GetWindowRect(hwnd, out var rect))
      {
        _ = SetWindowPos(hwnd, -2, rect.Left, rect.Top, (int)Width, (int)Height, 0);
      }
    }

    #region P/Invoke imports & definitions

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
      public int Left;
      public int Top;
      public int Right;
      public int Bottom;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32", EntryPoint = "SetWindowPos")]
    private static extern int SetWindowPos(IntPtr hWnd, int hwndInsertAfter, int x, int y, int cx, int cy, int wFlags);

    #endregion
  }
}
