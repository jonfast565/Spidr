using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MongoDB;
using MongoDB.Driver;
using MongoDB.Bson;

namespace Spidr.Runtime
{
    class MongoDbPersistence : IPersistenceInserter
    {
        public MongoClient Client { get; set; }

        public MongoDbPersistence()
        {
            Client = new MongoClient(new MongoClientSettings { ConnectionMode = ConnectionMode.ReplicaSet, Server = new MongoServerAddress("dt1th0ls1", 27017) });
        }

        public void InsertBinaryFile(BinaryFile f, string type)
        {
            var database = Client.GetDatabase("webdb");
            var collection = database.GetCollection<BsonDocument>("files");
            collection.InsertOne(new BsonDocument
            {
                {"FileId", Guid.NewGuid() },
                {"PageId", f.PageId.ToString() },
                {"Tag", f.Tag },
                {"Path", f.Url.GetFullPath(false) },
                {"Filename", f.Url.Path.Last() },
                {"AggregatedDateTime", DateTime.Now }
                //{"Contents", f.Contents.ToArray() }
            });
        }

        public void InsertLink(LinkTag t)
        {
            var database = Client.GetDatabase("webdb");
            var collection = database.GetCollection<BsonDocument>("links");
            collection.InsertOne(new BsonDocument
            {
                {"LinkId", Guid.NewGuid() },
                {"PageId", t.PageId.ToString() },
                {"Tag", t.Tag },
                {"Path", t.Url.GetFullPath(false) },
                {"AggregatedDateTime", DateTime.Now }
            });
        }

        public void InsertPage(Page p)
        {
            var database = Client.GetDatabase("webdb");
            var collection = database.GetCollection<BsonDocument>("pages");
            collection.InsertOne(new BsonDocument
            {
                {"PageId", p.PageId.ToString() },
                {"Tag", p.Link.GetFullPath(false) },
                {"Title", p.Name },
                //{"Content", Utility.GetBytes(p.Content) }
                {"AggregatedDateTime", DateTime.Now }
            });
        }

        public void PersistData(Page PageItem)
        {
            try
            {
                Console.WriteLine("Persisting: " + PageItem.Link.GetFullPath(false));
                // ascertain the number of total operations
                var numberOfOperations = 0;
                numberOfOperations += PageItem.FileTags.Count;
                numberOfOperations += PageItem.LinkTags.Count;
                numberOfOperations += PageItem.ImageTags.Count;
                numberOfOperations++;

                // wait until the page queue contains no more items
                int numberOfCompletedOperations = 0;
                try
                {
                    // insert the page and related items
                    InsertPage(PageItem);
                    foreach (LinkTag Hyperlink in PageItem.LinkTags)
                    {
                        InsertLink(Hyperlink);
                        numberOfCompletedOperations++;
                    }
                    foreach (BinaryFile Image in PageItem.ImageTags)
                    {
                        InsertBinaryFile(Image, "IMAGE");
                        numberOfCompletedOperations++;
                    }
                    foreach (BinaryFile FileItem in PageItem.FileTags)
                    {
                        InsertBinaryFile(FileItem, "FILE");
                        numberOfCompletedOperations++;
                    }
                }
                catch (Exception e)
                {
                    throw;
                }
                finally
                {
                    numberOfCompletedOperations++;
                }
            }
            catch (Exception e)
            {
                throw;
            }
        }
    }
}
