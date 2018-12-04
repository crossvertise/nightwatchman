using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Driver;

namespace Repos
{
    public abstract class AMongoRepo<T>
    {
        internal IMongoClient Client { get; set; }

        internal IMongoDatabase Database { get; set; }

        internal IMongoCollection<T> Collection { get; set; }

        //protected AMongoRepo()
        //{
            
        //    Client = new MongoClient();
        //    Database = Client.GetDatabase("asd");
        //    Collection = Database.GetCollection<T>(nameof(T));
        //}

        protected AMongoRepo(string connectionString, string databaseName)
        {

            Client = new MongoClient(connectionString);
            Database = Client.GetDatabase(databaseName);
            Collection = Database.GetCollection<T>(typeof(T).Name);
        }

        public async Task Create(T item)
        {
            await Collection.InsertOneAsync(item);
        }

        public async Task<T> GetById(string id)
        {
            var filter = Builders<T>.Filter.Eq("id", id);
            var result = await Collection.Find(filter).FirstOrDefaultAsync();
            return result;
        }

        public async Task<ICollection<T>> GetAll()
        {
            var result = await Collection.Find(x => true).ToListAsync();
            return result;
        }
    }
}
