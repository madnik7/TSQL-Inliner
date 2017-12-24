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
            Children = new List<TreeModel>();
        }
        public object DomObject { get; set; }

        public string ParentObjectPropertyName { get; set; }

        public List<TreeModel> Children { get; set; }
    }
}