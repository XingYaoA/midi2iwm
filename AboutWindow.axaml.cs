using Avalonia.Controls;
using Avalonia.Interactivity;

namespace midi2iwmAvalonia;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
    }

    private void Close(object sender, RoutedEventArgs e)
    {
        Close();
    }
}