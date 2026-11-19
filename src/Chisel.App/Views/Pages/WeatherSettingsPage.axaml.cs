using Avalonia.Controls;
using Chisel.App.ViewModels;

namespace Chisel.App.Views.Pages;

public partial class WeatherSettingsPage : UserControl
{
    public WeatherSettingsPage()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is MainViewModel main)
            {
                this.FindControl<AutoCompleteBox>("WeatherSearchBox")!.AsyncPopulator = main.WeatherSettings.SuggestAsync;
            }
        };
    }
}
