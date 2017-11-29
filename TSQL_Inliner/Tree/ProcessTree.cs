using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using TSQL_Inliner.Model;

namespace TSQL_Inliner.Tree
{
    public class ProcessTree
    {
        private static Assembly ScriptDom;
        private int MaxDepth = 10;

        public ProcessTree()
        {
            ScriptDom = Assembly.Load("Microsoft.SqlServer.TransactSql.ScriptDom");
        }
        public List<TreeModel> GetChildren(object node, int depth = 0)
        {
            var items = new List<TreeModel>();

            if (depth++ > MaxDepth || IgnoreType(node))
                return items;

            if (node is IEnumerable<TreeModel>)
            {
                var collectionNode = new TreeModel
                {
                    Node = (TSqlFragment)node
                };
                foreach (var child in node as IEnumerable<object>)
                {
                    var children = GetChildren(child, depth);
                    foreach (var c in children)
                    {
                        collectionNode.Items.Add(c);
                    }
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
                Node = (TSqlFragment)node
            };

            foreach (var p in t.GetProperties())
            {
                var item = new TreeModel
                {
                    Node = TryGetValue(p, node) as TSqlFragment
                };
                newItem.Items.Add(item);
                switch (p.Name)
                {
                    case "ScriptTokenStream":
                        break;

                    default:
                        foreach (var i in GetChildren(TryGetValue(p, node), depth))
                        {
                            item.Items.Add(i);
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
            catch (Exception)
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