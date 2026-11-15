using Audibly.Mobile.Views;

namespace Audibly.Mobile;

/// <summary>
///     The player is the app's home. Library and Settings open on top of it, from the quick link
///     or the menu, and Android's back button returns to the player.
/// </summary>
public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();
		Routing.RegisterRoute(nameof(LibraryPage), typeof(LibraryPage));
		Routing.RegisterRoute(nameof(SettingsPage), typeof(SettingsPage));
	}

	private async Task GoAsync(string route)
	{
		FlyoutIsPresented = false;
		// one page on top of the player at a time, so back always returns to the player
		if (Navigation.NavigationStack.Count > 1) await Navigation.PopToRootAsync(false);
		if (!string.IsNullOrEmpty(route)) await GoToAsync(route);
	}

	private async void Player_Clicked(object? sender, EventArgs e) => await GoAsync(string.Empty);
	private async void Library_Clicked(object? sender, EventArgs e) => await GoAsync(nameof(LibraryPage));
	private async void Settings_Clicked(object? sender, EventArgs e) => await GoAsync(nameof(SettingsPage));

	private async void Speed_Clicked(object? sender, EventArgs e)
	{
		await GoAsync(string.Empty);
		(CurrentPage as PlayerPage)?.ShowSpeed();
	}

	private async void Timer_Clicked(object? sender, EventArgs e)
	{
		await GoAsync(string.Empty);
		if (CurrentPage is PlayerPage player) await player.ShowTimerAsync();
	}

	/// <summary>Android's back button: closes the menu or the page on top; on the player it leaves the app.</summary>
	protected override bool OnBackButtonPressed()
	{
		if (FlyoutIsPresented)
		{
			FlyoutIsPresented = false;
			return true;
		}

		if (Navigation.NavigationStack.Count > 1)
		{
			_ = GoToAsync("..");
			return true;
		}

		return base.OnBackButtonPressed();
	}
}
