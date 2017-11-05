using Microsoft.SqlServer.TransactSql.ScriptDom;
using System;
using System.Collections.Generic;
using System.Linq;
using TSQL_Inliner.Model;
using TSQL_Inliner.Inliner;

namespace TSQL_Inliner.ProcOptimization
{
    class ExecuteVisitor : TSqlConcreteFragmentVisitor
    {
        ProcOptimizer ProcOptimizer { get { return Program.ProcOptimizer; } }
        public List<StatementList> StatementListCollection = new List<StatementList>();


        bool IsOptimized { get; set; }
        public ExecuteVisitor(bool isOptimized)
        {
            IsOptimized = isOptimized;
        }

        public override void Visit(TSqlScript node)
        {
            if (node.Batches.Any() && node.Batches.FirstOrDefault().Statements.Any(b => b is CreateProcedureStatement))
            {
                CreateProcedureStatement createProcedureStatement = (CreateProcedureStatement)node.Batches.FirstOrDefault().Statements.FirstOrDefault(b => b is CreateProcedureStatement);

                AlterProcedureStatement alterProcedureStatement = new AlterProcedureStatement()
                {
                    IsForReplication = createProcedureStatement.IsForReplication,
                    MethodSpecifier = createProcedureStatement.MethodSpecifier,
                    ProcedureReference = createProcedureStatement.ProcedureReference,
                    StatementList = createProcedureStatement.StatementList,
                    ScriptTokenStream = createProcedureStatement.ScriptTokenStream
                };
                foreach (var i in createProcedureStatement.Options)
                    alterProcedureStatement.Options.Add(i);
                foreach (var i in createProcedureStatement.Parameters)
                    alterProcedureStatement.Parameters.Add(i);

                node.Batches.FirstOrDefault().Statements[node.Batches.FirstOrDefault().Statements.IndexOf(createProcedureStatement)] = alterProcedureStatement;
            }
            base.Visit(node);
        }

        /// <summary>
        /// override 'Visit' method for process 'ExecuteStatement' in 'StatementLists'
        /// </summary>
        /// <param name="node"></param>
        public override void Visit(StatementList node)
        {
            foreach (var executeStatement in node.Statements.Where(a => a is ExecuteStatement).ToList())
            {
                var executableProcedureReference = (ExecutableProcedureReference)(((ExecuteStatement)executeStatement).ExecuteSpecification.ExecutableEntity);

                var newBody = ExecuteStatement(executableProcedureReference);
                if (newBody.StatementList != null && newBody.StatementList.Statements.Any())
                    node.Statements[node.Statements.IndexOf(executeStatement)] = newBody;
            }
            base.Visit(node);
        }


        #region Methods

        public BeginEndBlockStatement ExecuteStatement(ExecutableProcedureReference executableProcedureReference)
        {
            SpInfo spInfo = new SpInfo
            {
                Schema = executableProcedureReference.ProcedureReference.ProcedureReference.Name.SchemaIdentifier.Value,
                Name = executableProcedureReference.ProcedureReference.ProcedureReference.Name.BaseIdentifier.Value
            };

            BeginEndBlockStatement newBody = new BeginEndBlockStatement();
            //optimize the procedure
            if (!ProcOptimizer.ProcessedProcdures.Any(a => a == $"{spInfo.Schema}.{spInfo.Name}"))
            {
                //ProcOptimizer.ProcessedProcdures.Add($"{spInfo.Schema}.{spInfo.Name}");
                //ProcOptimizer.Process(spInfo);
            }

            ProcInliner executeInliner = new ProcInliner();
            newBody = executeInliner.GetExecuteStatementAsInline(spInfo, executableProcedureReference);
            return newBody;
        }

        #endregion
    }
}