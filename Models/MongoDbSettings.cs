namespace PSRes.Models
{
    public class MongoDbSettings
    {
        public string ConnectionURI { get; set; } = null!;
        public string DatabaseName { get; set; } = null!;
        public string CollectionNameP { get; set; } = null!;
        public string CollectionNameR { get; set; } = null!;
    }
}
