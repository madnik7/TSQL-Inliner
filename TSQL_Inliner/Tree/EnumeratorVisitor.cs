﻿using Microsoft.SqlServer.TransactSql.ScriptDom;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TSQL_Inliner.Tree
{
    public class EnumeratorVisitor : TSqlFragmentVisitor
    {
        public List<TSqlStatement> StatementList = new List<TSqlStatement>();

        public override void Visit(TSqlStatement node)
        {
            base.Visit(node);
            if (!StatementList.Any(p => p.StartOffset <= node.StartOffset && p.StartOffset + p.FragmentLength >= node.StartOffset + node.FragmentLength))
            {
                StatementList.Add(node);
            }
        }
    }
}