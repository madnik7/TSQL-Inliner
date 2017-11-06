using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TSQL_Inliner.Model
{
    class AppArgument
    {
        public string ConnectionString { get; set; }
        public string Schema { get; set; }
        public string ProcName { get; internal set; }
    }
}
