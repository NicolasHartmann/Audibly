namespace Audibly.Mobile;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();
		// the player opens on top of the library
		Routing.RegisterRoute(nameof(Views.PlayerPage), typeof(Views.PlayerPage));
	}

	/// <summary>
	///     Android's back button or gesture: closes the player page, and from the Settings tab returns
	///     to the Library tab (which still shows the player when it was open). Only on the library
	///     itself does back leave the app.
	/// </summary>
	protected override bool OnBackButtonPressed()
	{
		if (Navigation.NavigationStack.Count > 1)
		{
			_ = GoToAsync("..");
			return true;
		}

		if (CurrentItem?.CurrentItem is { } tab && CurrentItem.Items.Count > 0 && tab != CurrentItem.Items[0])
		{
			CurrentItem.CurrentItem = CurrentItem.Items[0];
			return true;
		}

		return base.OnBackButtonPressed();
	}
}
