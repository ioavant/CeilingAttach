using System;
using System.Windows;
using System.Windows.Interop;

public static class WindowHelper
{
    public static Window ToWpfWindow(this IntPtr handle)
    {
        var window = new Window();
        new WindowInteropHelper(window).Owner = handle;
        return window;
    }
}