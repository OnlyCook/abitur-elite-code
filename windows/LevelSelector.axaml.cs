using Avalonia.Controls;
using Avalonia.Input;

namespace AbiturEliteCode;

public partial class LevelSelector : Window
{
    public LevelSelector()
    {
        InitializeComponent();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) Close();
    }
}