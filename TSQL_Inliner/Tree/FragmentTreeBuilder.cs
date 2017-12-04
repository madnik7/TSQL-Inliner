﻿using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using TSQL_Inliner.Model;
using System.Linq;

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
            EnumeratorVisitor enumeratorVisitor = new EnumeratorVisitor();
            sqlFragment.Accept(enumeratorVisitor);

            TreeModel treeModel = new TreeModel();

            FragmentTreeBuilder treeBuilder = new FragmentTreeBuilder();
            treeModel.DomObject = enumeratorVisitor.StatementList;

            foreach (var statemen in enumeratorVisitor.StatementList)
                treeModel.Children.AddRange(treeBuilder.GetChildren(statemen));

            return treeModel;
        }

        public List<TreeModel> GetChildren(object node, int depth = 0)
        {
            var items = new List<TreeModel>();

            if (depth++ > MaxDepth || IgnoreType(node))
                return items;

            if (node is IEnumerable<object>)
            {
                var collectionNode = new TreeModel
                {
                    DomObject = node
                };
                foreach (var child in node as IEnumerable<object>)
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
                return items;
            }

            foreach (var p in t.GetProperties())
            {
                switch (p.Name)
                {
                    case "ScriptTokenStream":
                    case "StartOffset":
                    case "FragmentLength":
                    case "StartLine":
                    case "StartColumn":
                    case "FirstTokenIndex":
                    case "LastTokenIndex":
                        break;

                    default:
                        var item = new TreeModel
                        {
                            DomObject = p
                        };
                        item.Children.AddRange(GetChildren(TryGetValue(p, node), depth));

                        items.Add(item);
                        break;
                }
            }

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
                return null;
            }
        }

        private bool IgnoreType(object node)
        {
            if (node == null)
                return true;

            var type = node.GetType();

            if (node.ToString().Contains("Microsoft.SqlServer.TransactSql.ScriptDom"))
            {
                return false;
            }

            return !type.FullName.Contains("Microsoft.SqlServer.TransactSql.ScriptDom");
        }
    }
}