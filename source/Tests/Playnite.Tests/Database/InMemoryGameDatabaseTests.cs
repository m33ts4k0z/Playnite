using NUnit.Framework;
using Playnite.Database;
using Playnite.SDK.Models;

namespace Playnite.Tests.Database
{
    [TestFixture]
    public class InMemoryGameDatabaseTests
    {
        [Test]
        public void CollectionsAreStableAndUsageEventsFollowChanges()
        {
            var database = new InMemoryGameDatabase();
            var platformChanges = 0;
            database.PlatformsInUseUpdated += (_, __) => platformChanges++;

            database.Platforms.Add(new Platform("Linux"));

            Assert.AreSame(database.CompletionStatuses, database.CompletionStatuses);
            Assert.AreSame(database.ImportExclusions, database.ImportExclusions);
            Assert.AreSame(database.FilterPresets, database.FilterPresets);
            Assert.AreEqual(1, platformChanges);
        }

        [Test]
        public void OpenDatabaseRaisesDatabaseOpened()
        {
            var database = new InMemoryGameDatabase();
            var opened = 0;
            database.DatabaseOpened += (_, __) => opened++;

            database.OpenDatabase();

            Assert.AreEqual(1, opened);
        }
    }
}
