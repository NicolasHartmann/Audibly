using Audibly.Core;

namespace Audibly.Mobile;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();

        // skeleton check: the shared library is reachable from the Android app
        StatusLabel.Text = "Shared library ready: " + PlayerProgress.Elapsed(3_723_000);
    }
}
