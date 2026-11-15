using Audibly.Mobile.Services;
using Microsoft.Extensions.Logging;

namespace Audibly.Mobile;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		builder.Services.AddSingleton(_ => new AppData(FileSystem.AppDataDirectory));
		builder.Services.AddSingleton<IPlaybackService, AndroidPlaybackService>();
		builder.Services.AddSingleton<ILibraryImporter, AndroidLibraryImporter>();
		builder.Services.AddSingleton<ISyncFolderAccess, AndroidSyncFolderAccess>();
		builder.Services.AddSingleton<SyncHost>();
		builder.Services.AddSingleton<PlaybackCoordinator>();
		builder.Services.AddTransient<Views.LibraryPage>();
		builder.Services.AddTransient<Views.PlayerPage>();
		builder.Services.AddTransient<Views.SettingsPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
