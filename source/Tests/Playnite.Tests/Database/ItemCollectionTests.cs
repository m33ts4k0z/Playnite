using NUnit.Framework;
using Playnite.Common;
using Playnite.Database;
using Playnite.SDK;
using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Playnite.Tests.Database
{
    [TestFixture]
    public class ItemCollectionTests
    {
        private const string LiteDbV4Database =
            "H4sIAAAAAAACCu3ZPU7DMBTA8ec4CCFQqRAHsBBTJNg6slQdK1hYEXKLKUEwNQwwMXEKTgDscA9OwgmCnY+mtAwVQlWH/0+x5Tw7HhJleH4iIiqvKGkkiTm9SsfGX9b008z1uuYyvXEmSdaVhNWxLEq1fNezmR3YsTsZXLthFgkAAAAAAAAAAAAAAPgvoQ4f/Vb/ny/Z17Oh7r/h23l6cbR/6Huli3joleh6g3LLZkxw4SAAAAAAAH+kZlVxXbUi+dTyuNU8YiSfMslVS7o4O8ixdGruw7T4JKsgEvXj7bdlc+/p4et996378Xywc3f2+RpP/rn6uAwAAAAAliWumpSp5ct2MxUiHen40Zov8ks7rJxNaKJje+skHBr03cgO780o3AMAAAAAgJXyDTyfjL4AUAAA";

        [Test]
        public void MigratesLiteDbV4DatabaseAndRetainsBackup()
        {
            using (var temp = TempDirectory.Create())
            {
                var collectionPath = Path.Combine(temp.TempPath, "legacy");
                var databasePath = collectionPath + ".db";
                using (var compressed = new MemoryStream(Convert.FromBase64String(LiteDbV4Database)))
                using (var gzip = new GZipStream(compressed, CompressionMode.Decompress))
                using (var database = File.Create(databasePath))
                {
                    gzip.CopyTo(database);
                }

                int migratedCount;
                using (var collection = new ItemCollection<DatabaseObject>(collectionPath, null))
                {
                    migratedCount = collection.Count;
                }

                Assert.IsTrue(File.Exists(databasePath + ".v4.backup"));
                using (var database = new LiteDB.LiteDatabase(databasePath))
                {
                    Assert.AreEqual(
                        1,
                        database.GetCollection("DatabaseObject").Count(),
                        "Migrated collections: " + string.Join(", ", database.GetCollectionNames()));
                    Assert.AreEqual(1, database.GetCollection<DatabaseObject>().FindAll().Count());
                }

                Assert.AreEqual(1, migratedCount, "The migrated item was not loaded into memory.");

                using (var reopened = new ItemCollection<DatabaseObject>(collectionPath, null))
                {
                    Assert.AreEqual(1, reopened.Count);
                    Assert.AreEqual("Legacy game", reopened[Guid.Parse("f17a8622-14b7-42ac-b89c-2d12755dd3ab")].Name);
                }
            }
        }

        [Test]
        public void AddTest()
        {
            var newGame = new Game("TestGame");
            using (var temp = TempDirectory.Create())
            using (var col = new ItemCollection<Game>(Path.Combine(temp.TempPath, "db"), null))
            {
                col.Add(newGame);
                Assert.AreEqual(1, col.Count);
            }
        }

        [Test]
        public void EventsInvokeCountNonBufferedTest()
        {
            using (var temp = TempDirectory.Create())
            using (var col = new ItemCollection<DatabaseObject>(Path.Combine(temp.TempPath, "db"), null))
            {
                var itemUpdates = 0;
                var colUpdates = 0;
                col.ItemUpdated += (e, args) => itemUpdates++;
                col.ItemCollectionChanged += (e, args) => colUpdates++;

                var item = new DatabaseObject();
                col.Add(item);
                col.Update(item);
                col.Remove(item);
                Assert.AreEqual(1, itemUpdates);
                Assert.AreEqual(2, colUpdates);
            }
        }

        [Test]
        public void EventsInvokeCountBufferedTest()
        {
            using (var temp = TempDirectory.Create())
            using (var col = new ItemCollection<DatabaseObject>(Path.Combine(temp.TempPath, "db"), null))
            {
                var itemUpdates = 0;
                var colUpdates = 0;
                col.ItemUpdated += (e, args) => itemUpdates++;
                col.ItemCollectionChanged += (e, args) => colUpdates++;

                var item = new DatabaseObject();
                col.BeginBufferUpdate();
                col.Add(item);
                col.Update(item);
                col.Remove(item);
                Assert.AreEqual(0, itemUpdates);
                Assert.AreEqual(0, colUpdates);
                col.EndBufferUpdate();
                Assert.AreEqual(1, itemUpdates);
                Assert.AreEqual(1, colUpdates);
            }
        }

        [Test]
        public void EventsArgsNonBufferedTest()
        {
            using (var temp = TempDirectory.Create())
            using (var col = new ItemCollection<DatabaseObject>(Path.Combine(temp.TempPath, "db"), null))
            {
                ItemCollectionChangedEventArgs<DatabaseObject> itemColArgs = null;
                ItemUpdatedEventArgs<DatabaseObject> itemUpdateArgs = null;
                col.ItemUpdated += (e, args) => itemUpdateArgs = args;
                col.ItemCollectionChanged += (e, args) => itemColArgs = args;
                var item = new DatabaseObject() { Name = "Original" };

                col.Add(item);
                Assert.AreEqual(1, itemColArgs.AddedItems.Count);
                Assert.AreEqual(item, itemColArgs.AddedItems[0]);
                Assert.AreEqual(0, itemColArgs.RemovedItems.Count);

                item.Name = "New";
                col.Update(item);
                Assert.AreEqual(1, itemUpdateArgs.UpdatedItems.Count);
                Assert.AreEqual("Original", itemUpdateArgs.UpdatedItems[0].OldData.Name);
                Assert.AreEqual("New", itemUpdateArgs.UpdatedItems[0].NewData.Name);

                col.Remove(item);
                Assert.AreEqual(0, itemColArgs.AddedItems.Count);
                Assert.AreEqual(1, itemColArgs.RemovedItems.Count);
                Assert.AreEqual(item, itemColArgs.RemovedItems[0]);
            }
        }

        [Test]
        public void EventsArgsBufferedTest()
        {
            using (var temp = TempDirectory.Create())
            using (var col = new ItemCollection<DatabaseObject>(Path.Combine(temp.TempPath, "db"), null))
            {
                ItemCollectionChangedEventArgs<DatabaseObject> itemColArgs = null;
                ItemUpdatedEventArgs<DatabaseObject> itemUpdateArgs = null;
                col.ItemUpdated += (e, args) => itemUpdateArgs = args;
                col.ItemCollectionChanged += (e, args) => itemColArgs = args;
                var item = new DatabaseObject();

                col.BeginBufferUpdate();
                col.Add(item);
                col.Update(item);
                col.Remove(item);
                Assert.IsNull(itemColArgs);
                Assert.IsNull(itemUpdateArgs);

                col.EndBufferUpdate();
                Assert.AreEqual(1, itemColArgs.AddedItems.Count);
                Assert.AreEqual(1, itemColArgs.RemovedItems.Count);
                Assert.AreEqual(1, itemUpdateArgs.UpdatedItems.Count);
            }
        }

        [Test]
        public void NestedBufferTest()
        {
            using (var temp = TempDirectory.Create())
            using (var col = new ItemCollection<DatabaseObject>(Path.Combine(temp.TempPath, "db"), null))
            {
                var colChanges = 0;
                var colUpdates = 0;
                col.ItemUpdated += (e, args) => colUpdates++;
                col.ItemCollectionChanged += (e, args) => colChanges++;
                var item = new DatabaseObject();

                col.BeginBufferUpdate();
                col.BeginBufferUpdate();
                col.BeginBufferUpdate();
                col.Add(item);
                col.Update(item);
                col.EndBufferUpdate();
                Assert.AreEqual(0, colChanges);
                Assert.AreEqual(0, colChanges);
                col.EndBufferUpdate();
                Assert.AreEqual(0, colChanges);
                Assert.AreEqual(0, colChanges);
                col.EndBufferUpdate();
                Assert.AreEqual(1, colUpdates);
                Assert.AreEqual(1, colChanges);

                col.BeginBufferUpdate();
                col.Update(item);
                col.EndBufferUpdate();
                Assert.AreEqual(2, colUpdates);
            }
        }

        [Test]
        public void BufferConsolidationTest()
        {
            using (var temp = TempDirectory.Create())
            using (var col = new ItemCollection<DatabaseObject>(Path.Combine(temp.TempPath, "db"), null))
            {
                ItemCollectionChangedEventArgs<DatabaseObject> colChanges = null;
                ItemUpdatedEventArgs<DatabaseObject> colUpdates = null;
                col.ItemUpdated += (e, args) => colUpdates = args;
                col.ItemCollectionChanged += (e, args) => colChanges = args;

                var item = new DatabaseObject() { Name = "Original" };
                col.Add(item);

                col.BeginBufferUpdate();
                item.Name = "Change1";
                col.Update(item);
                item.Name = "Change2";
                col.Update(item);
                col.EndBufferUpdate();
                Assert.AreEqual(1, colUpdates.UpdatedItems.Count);
                Assert.AreEqual("Original", colUpdates.UpdatedItems[0].OldData.Name);
                Assert.AreEqual("Change2", colUpdates.UpdatedItems[0].NewData.Name);
            }
        }
    }
}
