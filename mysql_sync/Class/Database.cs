using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.Marshalling.IIUnknownCacheStrategy;

namespace mysql_sync.Class
{
    /// <summary>
    /// Representa um schema/banco de dados MySQL.
    /// </summary>
    public class Database
    {
        public string Name { get; set; }
        public ObservableCollection<DatabaseObject> Objects { get; } = new ObservableCollection<DatabaseObject>();

        public Database(string name)
        {
            Name = name;
        }
    }
}
