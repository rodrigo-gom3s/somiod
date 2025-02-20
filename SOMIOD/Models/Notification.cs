using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Xml.Serialization;

namespace SOMIOD.Models
{
    [XmlRoot("Notification")]
    public class Notification : EntityWithParent
    {
        public int @event { get; set; }
        public string endpoint { get; set; }    
        public bool enabled { get; set; }   
    }
}