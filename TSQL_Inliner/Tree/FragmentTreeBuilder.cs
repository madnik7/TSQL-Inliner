using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using TSQL_Inliner.Model;

namespace TSQL_Inliner.Tree
{
    public class FragmentTreeBuilder
    {
        private static Assembly ScriptDom;
        private int MaxDepth = 10;

        public FragmentTreeBuilder()
        {
            ScriptDom = Assembly.Load("Microsoft.SqlServer.TransactSql.ScriptDom");
        }

        static public TreeModel CreateTreeFromFragment(TSqlFragment sqlFragment)
        {
            TreeModel treeModel = new TreeModel();

            FragmentTreeBuilder treeBuilder = new FragmentTreeBuilder();
            treeModel.DomObject = sqlFragment;
            treeModel.Children = treeBuilder.GetChildren(sqlFragment);
            return treeModel;
        }


        public List<TreeModel> GetChildren(object node, int depth = 0)
        {
            var items = new List<TreeModel>();

            if (depth++ > MaxDepth || IgnoreType(node))
                return items;

            if (node is IEnumerable<object>)
            {
                var collectionNode = new TreeModel();
                collectionNode.DomObject = node;
                foreach (var child in node as IEnumerable<TreeModel>)
                {
                    var children = GetChildren(child, depth);
                    collectionNode.Children.AddRange(children);
                }
                items.Add(collectionNode);
                return items;
            }

            var nodeType = node.ToString().Split(' ')[0];
            var t = ScriptDom.GetType(nodeType, false, true);

            if (t == null)
            {
                var item = new TreeModel();
                items.Add(item);
                return items;
            }

            var newItem = new TreeModel
            {
                DomObject = (TSqlFragment)node
            };

            foreach (var p in t.GetProperties())
            {
                var item = new TreeModel
                {
                    DomObject = TryGetValue(p, node) as TSqlFragment
                };
                newItem.Children.Add(item);
                switch (p.Name)
                {
                    case "ScriptTokenStream":
                        break;

                    default:
                        foreach (var i in GetChildren(TryGetValue(p, node), depth))
                        {
                            item.Children.Add(i);
                        }

                        break;
                }
            }

            items.Add(newItem);
            return items;
        }

        private object TryGetValue(PropertyInfo propertyInfo, object node)
        {
            try
            {
                return propertyInfo.GetValue(node);
            }
            catch (Exception ex)
            {
                return "";
            }
        }

        private bool IgnoreType(object node)
        {
            if (node == null)
                return true;

            var type = node.GetType();
            Console.WriteLine(type);

            if (node.ToString().Contains("Microsoft.SqlServer.TransactSql.ScriptDom"))
            {
                return false;
            }

            return !type.FullName.Contains("Microsoft.SqlServer.TransactSql.ScriptDom");
        }
    }
}