using Audibly.Repository.Interfaces;
using Audibly.Repository.Sql;
using Audibly.Stats;
using Microsoft.EntityFrameworkCore;

namespace Audibly.Mobile.Services;

/// <summary>
///     The app's data in its private storage: the same library database and statistics file as on
///     Windows. Created once at start.
/// </summary>
public sealed class AppData
{
    public AppData(string folder)
    {
        Directory.CreateDirectory(folder);
        DatabasePath = Path.Combine(folder, "Audibly.db");
        StatsPath = Path.Combine(folder, "stats.db");

        var options = new DbContextOptionsBuilder<AudiblyContext>()
            .UseSqlite("Data Source=" + DatabasePath)
            .Options;
        using (var context = new AudiblyContext(options))
        {
            // the migrations are the ones the Windows app applies, so both platforms share one schema
            context.Database.Migrate();
        }

        Repository = new SqlAudiblyRepository(options);
        Stats = new StatsStore(StatsPath);
    }

    public string DatabasePath { get; }
    public string StatsPath { get; }
    public IAudiblyRepository Repository { get; }
    public StatsStore Stats { get; }
}
