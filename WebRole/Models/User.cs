using System.Collections.Generic;
using MongoDB.Bson;

namespace WebRole.Models
{
    public class User
    {
        public string Id { get; set; }

        public string Name { get; set; }

        public string Username { get; set; }

        private BsonValue _postsBson;
        
        public BsonValue PostsBson
        {
            get { return _postsBson ?? new BsonArray(); }
            set { _postsBson = value;  }
        }
    }
}