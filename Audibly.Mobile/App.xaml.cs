using Microsoft.Extensions.DependencyInjection;

namespace Audibly.Mobile;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		var window = new Window(new AppShell());
		// going to the background is the last reliable moment before Android may end the process
		window.Stopped += async (_, _) =>
		{
			try
			{
				if (Handler?.MauiContext?.Services.GetService<Services.PlaybackCoordinator>() is { } playback)
					await playback.FlushAsync();
			}
			catch (Exception e)
			{
				System.Diagnostics.Debug.WriteLine("[App] flush failed: " + e);
			}
		};
		return window;
	}
}