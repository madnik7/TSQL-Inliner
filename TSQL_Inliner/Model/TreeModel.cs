using Microsoft.SqlServer.TransactSql.ScriptDom;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TSQL_Inliner.Model
{
    public class TreeModel
    {
        public TreeModel()
        {
            Items = new List<TreeModel>();
        }
        public TSqlFragment Node { get; set; }
        public List<TreeModel> Items { get; set; }
    }
}