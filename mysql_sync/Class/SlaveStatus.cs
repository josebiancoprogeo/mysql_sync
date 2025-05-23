using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace mysql_sync.Class
{
    /// <summary>
    /// Mapeia o resultado básico de SHOW SLAVE STATUS.
    /// </summary>
    public class SlaveStatus
    {
        public string ChannelName { get; set; }
        public string SlaveIORunning { get; set; }
        public string SlaveSQLRunning { get; set; }
        public long? SecondsBehindMaster { get; set; }
    }
}
