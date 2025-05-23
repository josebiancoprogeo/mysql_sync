using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace mysql_sync.Class
{
    /// <summary>
    /// Mapeia o resultado de performance_schema.replication_applier_status_by_worker.
    /// </summary>
    public class ApplierStatus
    {
        public int WorkerID { get; set; }
        public string ApplyngTransaction { get; set; }
        public int LastErrorNumber { get; set; }
        public string LastErrorMessage { get; set; }
    }
}
