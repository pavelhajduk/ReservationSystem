using MongoDB.Driver;
using System;

namespace PSRes.Settings
{
    public class MongoDbConfig
    {
        public string Name { get; init; }
        public string Host { get; init; }
        public int Port { get; init; }
        public string ConnectionURI { get; set; }
        public string ConnectionString => ConnectionURI;//$"mongodb://{Host}:{Port}";
    }
}
