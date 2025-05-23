using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace mysql_sync.Class
{
    /// <summary>
    /// Representa um trigger no banco.
    /// </summary>
    public class Trigger : DatabaseObject
    {
        public string Timing { get; set; }      // BEFORE | AFTER
        public string Event { get; set; }       // INSERT | UPDATE | DELETE
        public string Body { get; set; }        // Corpo/definição SQL do trigger

        public Trigger(string name, string timing, string @event, string body)
            : base(name)
        {
            Timing = timing;
            Event = @event;
            Body = body;
        }
    }
}
