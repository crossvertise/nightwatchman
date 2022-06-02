namespace Repos
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using Microsoft.Extensions.Configuration;

    using MongoDB.Driver;

    public abstract class AMongoRepo<T>
    {
        protected IMongoClient Client { get; set; }

        protected IMongoDatabase Database { get; set; }

        protected IMongoCollection<T> Collection { get; set; }

        protected abstract Func<T, string> IdProperty { get; }

        protected AMongoRepo(string connectionString, string databaseName)
        {

            Client = new MongoClient(connectionString);
            Database = Client.GetDatabase(databaseName);
            Collection = Database.GetCollection<T>(typeof(T).Name);
        }

        protected AMongoRepo(IConfiguration configuration)
            : this(configuration["MongoDbConnectionString"], configuration["MongoDbDatabaseName"])
        {
        }

        public async Task Create(T item) => await Collection.InsertOneAsync(item);

        public async Task CreateMany(IEnumerable<T> items) => await Collection.InsertManyAsync(items);

        public async Task Update(T item)
        {
            var id = IdProperty(item);
            var filter = Builders<T>.Filter.Eq("_id", id);
            await Collection.ReplaceOneAsync(filter, item);
        }

        public async Task Delete(string id)
        {
            var filter = Builders<T>.Filter.Eq("_id", id);
            await Collection.DeleteOneAsync(filter);
        }

        public async Task<T> GetById(string id)
        {
            var filter = Builders<T>.Filter.Eq("_id", id);
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
