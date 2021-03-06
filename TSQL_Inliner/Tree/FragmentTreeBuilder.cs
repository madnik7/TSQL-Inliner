﻿using System.Collections.Generic;
using System.Reflection;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using TSQL_Inliner.Model;
using System.Linq;

namespace TSQL_Inliner.Tree
{
    public class FragmentTreeBuilder
    {
        private static Assembly ScriptDom;
        public Dictionary<object, List<TreeModel>> FragmentDictionary;

        public FragmentTreeBuilder()
        {
            ScriptDom = Assembly.Load("Microsoft.SqlServer.TransactSql.ScriptDom");
            FragmentDictionary = new Dictionary<object, List<TreeModel>>();
        }

        static public TreeModel CreateTreeFromFragment(TSqlFragment sqlFragment)
        {
            EnumeratorVisitor enumeratorVisitor = new EnumeratorVisitor();
            sqlFragment.Accept(enumeratorVisitor);

            TreeModel treeModel = new TreeModel
            {
                DomObject = enumeratorVisitor.StatementList,
                ParentObjectPropertyName = "root"
            };

            FragmentTreeBuilder treeBuilder = new FragmentTreeBuilder();
            foreach (var statement in enumeratorVisitor.StatementList)
            {
                var ParentObjectPropertyName = ((TSqlScript)sqlFragment).Batches.FirstOrDefault().GetType().GetProperty("Statements").Name;
                treeModel.Children.AddRange(treeBuilder.GetChildren(statement, ParentObjectPropertyName));
            }

            var test = Newtonsoft.Json.JsonConvert.SerializeObject(treeModel.RemoveObject(treeModel));
            return treeModel;
        }

        public List<TreeModel> GetChildren(object node, string ParentObjectPropertyName)
        {
            var items = new List<TreeModel>();

            if (IgnoreType(node) || FragmentDictionary.Any(a => a.Value.Any(b => b == node)))
                return null;

            if (node is IEnumerable<object>)
            {
                foreach (var child in node as IEnumerable<object>)
                {
                    var collectionNode = new TreeModel
                    {
                        ParentObjectPropertyName = ParentObjectPropertyName,
                        DomObject = child
                    };

                    var children = GetChildren(child, null);
                    if (children != null)
                    {
                        collectionNode.Children.AddRange(children);
                        if (children.Count > 0)
                            FragmentDictionary.Add(child, children);
                    }
                    items.Add(collectionNode);
                }
                return items;
            }

            var nodeType = node.ToString().Split(' ')[0];
            var t = ScriptDom.GetType(nodeType, false, true);

            if (t == null)
            {
                return null;
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
                        var children = GetChildren(TryGetValue(p, node), node.GetType().GetProperties().FirstOrDefault(a => a == p).Name);
                        if (children != null)
                        {
                            var item = new TreeModel
                            {
                                ParentObjectPropertyName = ParentObjectPropertyName,
                                DomObject = p
                            };
                            item.Children.AddRange(children);
                            items.Add(item);
                        }
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
            catch
            {
                return null;
            }
        }

        private bool IgnoreType(object node)
        {
            if (node == null)
                return true;

            if (node.ToString().Contains("Microsoft.SqlServer.TransactSql.ScriptDom"))
                return false;

            var type = node.GetType();
            return !type.FullName.Contains("Microsoft.SqlServer.TransactSql.ScriptDom");
        }
    }
}