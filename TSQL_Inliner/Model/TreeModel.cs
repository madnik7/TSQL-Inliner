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

        //for test
        public TreeModel RemoveObject(TreeModel model)
        {
            if (model.Children != null && model.Children.Any())
                for (int i = 0; i < model.Children.Count; i++)
                    model.Children[i] = RemoveObject(model.Children[i]);
            TreeModel treeModel = new TreeModel()
            {
                Children = model.Children,
                ParentObjectPropertyName = model.ParentObjectPropertyName
            };
            return treeModel;
        }
    }
}