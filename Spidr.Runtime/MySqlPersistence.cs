using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using MySql.Data.MySqlClient;
using log4net;

namespace Spidr.Runtime
{
    public class MySqlPersistence : PersistenceInserter
    {
        private static ILog log = LogManager.GetLogger(typeof(MySqlPersistence));
        private MySqlConnection client;

        public MySqlPersistence()
        {
            client = new MySqlConnection("Server=localhost;Port=6000;Database=webdb;Uid=root;Pwd=none;");
            client.Open();
        }

        public void PersistData(Page PageItem)
        {
            try
            {
                // ascertain the number of total operations
                int NumberOfOperations = 0;
                NumberOfOperations += PageItem.FileTags.Count();
                NumberOfOperations += PageItem.LinkTags.Count();
                NumberOfOperations += PageItem.ImageTags.Count();
                NumberOfOperations++;

                // wait until the page queue contains no more items
                int NumberOfCompletedOperations = 0;
                try
                {
                    // insert the page and related items
                    InsertPage(PageItem);
                    foreach (LinkTag Hyperlink in PageItem.LinkTags)
                    {
                        InsertLink(Hyperlink);
                        NumberOfCompletedOperations++;
                    }
                    foreach (BinaryFile Image in PageItem.ImageTags)
                    {
                        InsertBinaryFile(Image, "IMAGE");
                        NumberOfCompletedOperations++;
                    }
                    foreach (BinaryFile FileItem in PageItem.FileTags)
                    {
                        InsertBinaryFile(FileItem, "FILE");
                        NumberOfCompletedOperations++;
                    }
                }
                catch (Exception e)
                {
                    log.Error(e);
                    throw;
                }
                finally
                {
                    NumberOfCompletedOperations++;
                }
            }
            catch (Exception e)
            {
                log.Error(e);
                // throw;
            }
        }

        public void InsertBinaryFile(BinaryFile f, string type)
        {
            string sql = "INSERT INTO FileTable (FileId, PageId, Tag, Path, Filename, TypeDesc, FileContents) "
                + " VALUES (@FileId, @PageId, @Tag, @Path, @Filename, @TypeDesc, @FileContents);";
            try
            {
                using (MySqlCommand c = new MySqlCommand(sql, client))
                {
                    c.Parameters.AddWithValue("@FileId", Guid.NewGuid());
                    c.Parameters.AddWithValue("@PageId", f.PageId.ToString());
                    c.Parameters.AddWithValue("@Tag", f.Tag);
                    c.Parameters.AddWithValue("@Path", f.Url.GetFullPath(false));
                    c.Parameters.AddWithValue("@Filename", f.Url.Path.Last());
                    c.Parameters.AddWithValue("@TypeDesc", type);
                    f.Contents.Position = 0;
                    c.Parameters.AddWithValue("@FileContents", f.Contents.ToArray());
                    c.ExecuteNonQuery();
                }
                Console.WriteLine("File persisted: " + f.Url.GetFullPath(false));
            }
            catch (Exception e)
            {
                log.Error("Inserting file failed", e);
                // throw;
            }
        }

        public void InsertLink(LinkTag t)
        {
            string sql = "INSERT INTO LinkTable (LinkId, PageId, Tag, Path) VALUES (@LinkId, @PageId, @Tag, @Path);";
            try
            {
                using (MySqlCommand c = new MySqlCommand(sql, client))
                {
                    c.Parameters.AddWithValue("@LinkId", Guid.NewGuid());
                    c.Parameters.AddWithValue("@PageId", t.PageId.ToString());
                    c.Parameters.AddWithValue("@Tag", t.Tag);
                    c.Parameters.AddWithValue("@Path", t.Url.GetFullPath(false));
                    c.ExecuteNonQuery();
                }
                Console.WriteLine("Link persisted: " + t.Url.GetFullPath(false));
            }
            catch (Exception e)
            {
                log.Error("Inserting link failed", e);
                // throw;
            }
        }

        public void InsertPage(Page p)
        {
            string sql = "INSERT INTO PageTable (PageId, Tag, Title, Content) VALUES (@PageId, @Tag, @Title, @Content);";
            try
            {
                using (MySqlCommand c = new MySqlCommand(sql, client))
                {
                    c.Parameters.AddWithValue("@PageId", p.PageId.ToString());
                    c.Parameters.AddWithValue("@Tag", p.Link.GetFullPath(false));
                    c.Parameters.AddWithValue("@Title", p.Name);
                    c.Parameters.AddWithValue("@Content", Utility.GetBytes(p.Content));
                    c.ExecuteNonQuery();
                }
                Console.WriteLine("Page persisted: " + p.Link.GetFullPath(false));
            }
            catch (Exception e)
            {
                log.Error("Inserting page failed", e);
                throw;
            }
        }
    }
}
