﻿using Microsoft.SqlServer.TransactSql.ScriptDom;
using System.Collections.Generic;
using System.Linq;
using TSQL_Inliner.Model;
using TSQL_Inliner.Inliner;

namespace TSQL_Inliner.ProcOptimization
{
    class ExecuteVisitor : TSqlConcreteFragmentVisitor
    {
        ProcOptimizer ProcOptimizer { get { return Program.ProcOptimizer; } }
        List<TSqlFragment> newStatements = new List<TSqlFragment>();

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
                ProcOptimizer.FunctionReturnType = null;

                foreach (var i in createProcedureStatement.Options)
                    alterProcedureStatement.Options.Add(i);
                foreach (var i in createProcedureStatement.Parameters)
                    alterProcedureStatement.Parameters.Add(i);

                node.Batches.FirstOrDefault().Statements[node.Batches.FirstOrDefault().Statements.IndexOf(createProcedureStatement)] = alterProcedureStatement;
            }

            if (node.Batches.Any() && node.Batches.FirstOrDefault().Statements.Any(b => b is CreateFunctionStatement))
            {
                CreateFunctionStatement createFunctionStatement = (CreateFunctionStatement)node.Batches.FirstOrDefault().Statements.FirstOrDefault(b => b is CreateFunctionStatement);

                AlterFunctionStatement alterFunctionStatement = new AlterFunctionStatement()
                {
                    MethodSpecifier = createFunctionStatement.MethodSpecifier,
                    StatementList = createFunctionStatement.StatementList,
                    ScriptTokenStream = createFunctionStatement.ScriptTokenStream,
                    Name = createFunctionStatement.Name,
                    OrderHint = createFunctionStatement.OrderHint,
                    ReturnType = createFunctionStatement.ReturnType
                };
                ProcOptimizer.FunctionReturnType = createFunctionStatement.ReturnType;

                foreach (var i in createFunctionStatement.Options)
                    alterFunctionStatement.Options.Add(i);
                foreach (var i in createFunctionStatement.Parameters)
                    alterFunctionStatement.Parameters.Add(i);

                node.Batches.FirstOrDefault().Statements[node.Batches.FirstOrDefault().Statements.IndexOf(createFunctionStatement)] = alterFunctionStatement;
            }
            base.Visit(node);
        }

        /// <summary>
        /// override 'Visit' method for process 'ExecuteStatement' in 'StatementLists'
        /// </summary>
        /// <param name="node"></param>
        public override void Visit(StatementList node)
        {
            foreach (ExecuteStatement executeStatement in node.Statements.Where(a => a is ExecuteStatement).ToList())
            {
                var executableProcedureReference = (ExecutableProcedureReference)((executeStatement).ExecuteSpecification.ExecutableEntity);
                if (executableProcedureReference.ProcedureReference.ProcedureReference.Name.DatabaseIdentifier == null)
                {
                    var newBody = ExecuteStatement(executableProcedureReference);
                    if (newBody.StatementList != null && newBody.StatementList.Statements.Any())
                    {
                        node.Statements[node.Statements.IndexOf(executeStatement)] = newBody;
                        newStatements.Add(newBody);
                    }
                }
            }

            foreach (SetVariableStatement setVariableStatement in node.Statements.Where(a => a is SetVariableStatement).ToList())
            {
                if (setVariableStatement.Expression is FunctionCall)
                {
                    var newBody = ExecuteFunctionStatement((FunctionCall)setVariableStatement.Expression, setVariableStatement.Variable);
                    if (newBody.StatementList != null && newBody.StatementList.Statements.Any())
                    {
                        node.Statements[node.Statements.IndexOf(setVariableStatement)] = newBody;
                        newStatements.Add(newBody);
                    }
                }
            }

            base.Visit(node);
        }

        public override void ExplicitVisit(BeginEndBlockStatement node)
        {
            //call the base if it the statement has not been generated by this object
            if (!newStatements.Contains(node))
                base.ExplicitVisit(node);
        }

        #region Methods

        private BeginEndBlockStatement ExecuteStatement(ExecutableProcedureReference executableProcedureReference)
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
                ProcOptimizer.ProcessedProcdures.Add($"{spInfo.Schema}.{spInfo.Name}");
                ProcOptimizer.Process(spInfo);
            }

            var namedValues = executableProcedureReference.Parameters.Where(a => a.Variable != null && !string.IsNullOrEmpty(a.Variable.Name))
               .ToDictionary(a => a.Variable.Name, a => a.ParameterValue);
            var unnamedValues = executableProcedureReference.Parameters.Where(a => a.Variable == null).Select(a => a.ParameterValue).ToList();

            ExecuteInliner executeInliner = new ExecuteInliner();
            newBody = executeInliner.GetStatementAsInline(spInfo, unnamedValues, namedValues);
            return newBody;
        }

        private BeginEndBlockStatement ExecuteFunctionStatement(FunctionCall functionCall, VariableReference setVariableReference)
        {
            SpInfo spInfo = new SpInfo
            {
                Schema = (((MultiPartIdentifierCallTarget)functionCall.CallTarget).MultiPartIdentifier.Identifiers).FirstOrDefault().Value,
                Name = functionCall.FunctionName.Value
            };

            BeginEndBlockStatement newBody = new BeginEndBlockStatement();

            //optimize the procedure
            if (!ProcOptimizer.ProcessedProcdures.Any(a => a == $"{spInfo.Schema}.{spInfo.Name}"))
            {
                ProcOptimizer.ProcessedProcdures.Add($"{spInfo.Schema}.{spInfo.Name}");
                ProcOptimizer.Process(spInfo);
            }

            ExecuteInliner executeInliner = new ExecuteInliner();
            newBody = executeInliner.GetStatementAsInline(spInfo, functionCall.Parameters.ToList(), null, setVariableReference);
            return newBody;
        }

        #endregion
    }
}