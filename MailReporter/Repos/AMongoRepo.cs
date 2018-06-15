using System;
using System.Collections.Generic;
using System.Text;
using MongoDB.Driver;

namespace Repos
{
    public abstract class AMongoRepo<T>
    {
        internal IMongoClient Client { get; set; }

        internal IMongoDatabase Database { get; set; }

        internal IMongoCollection<T> Collection { get; set; }
        protected AMongoRepo()
        {
            
            Client = new MongoClient();
            Database = Client.GetDatabase("asd");
            Collection = Database.GetCollection<T>(nameof(T));
        }

        public void Create(T item)
        {
            Collection.InsertOne(item);
        }
    }
}
