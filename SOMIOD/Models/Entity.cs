using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Web;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;
using System.Xml.Serialization;

namespace SOMIOD.Models
{
    public abstract class Entity
    {

        public int id { get; set; } 

 
        public string name { get; set; }

        public DateTime creation_datetime { get; set; }
    }
}