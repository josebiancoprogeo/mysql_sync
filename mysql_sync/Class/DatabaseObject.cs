using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace mysql_sync.Class
{
    /// <summary>
    /// Base para qualquer objeto de banco: tables, triggers, views, etc.
    /// </summary>
    public abstract class DatabaseObject
    {
        public string Name { get; set; }

        protected DatabaseObject(string name)
        {
            Name = name;
        }
    }
}
