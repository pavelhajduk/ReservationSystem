//using AspNetCore.Identity.MongoDbCore.Models;
//using MongoDbGenericRepository.Attributes;
//using System;
using System.Collections.Generic;

namespace IdentityMongo.Models
{
    public class UserOverview
    {
       public UserOverview(List<EditUser> users)
       {
            Users = users;
       }
       public List<EditUser> Users { get; set; }
    }
}
