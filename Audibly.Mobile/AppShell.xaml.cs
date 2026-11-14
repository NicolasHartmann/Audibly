namespace Audibly.Mobile;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();
		// the player opens on top of the library
		Routing.RegisterRoute(nameof(Views.PlayerPage), typeof(Views.PlayerPage));
	}
}
