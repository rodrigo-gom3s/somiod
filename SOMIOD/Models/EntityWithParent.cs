using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace SOMIOD.Models
{
    public abstract class EntityWithParent : Entity
    {
        public int parent { get; set; }
    }
}