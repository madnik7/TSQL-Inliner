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
        public string[] Schemas { get; set; } = new string[0];
        public string ProcName { get; internal set; }
    }
}
