﻿using Microsoft.SqlServer.TransactSql.ScriptDom;
using System.Linq;

namespace TSQL_Inliner.Method
{
    class MasterVisitor : TSqlConcreteFragmentVisitor
    {
        public MasterVisitor()
        {
            IsFirstCreateProcedureStatement = true;
        }
        //we must change "CreateProcedureStatement" to "AlterProcedureStatement" just for master stored procedure
        private static bool IsFirstCreateProcedureStatement = true;
        public override void Visit(TSqlScript node)
        {
            if (node.Batches.Any() && node.Batches.FirstOrDefault().Statements.Any(b => b is CreateProcedureStatement) && IsFirstCreateProcedureStatement)
            {
                IsFirstCreateProcedureStatement = false;
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
                var schemaIdentifier = executableProcedureReference.ProcedureReference.ProcedureReference.Name.SchemaIdentifier.Value;
                var baseIdentifier = executableProcedureReference.ProcedureReference.ProcedureReference.Name.BaseIdentifier.Value;
                var namedValues = executableProcedureReference.Parameters.Where(a => a.Variable != null && !string.IsNullOrEmpty(a.Variable.Name)).ToDictionary(a => a.Variable.Name, a => a.ParameterValue);
                var unnamedValues = executableProcedureReference.Parameters.Where(a => a.Variable == null).Select(a => a.ParameterValue).ToList();

                Inliner inliner = new Inliner();
                var NewBody = inliner.ExecuteStatement(schemaIdentifier, baseIdentifier, namedValues, unnamedValues);
                if (NewBody.StatementList != null && NewBody.StatementList.Statements.Any())
                    node.Statements[node.Statements.IndexOf(executeStatement)] = NewBody;
            }
            base.Visit(node);
        }
    }

    class VarVisitor : TSqlConcreteFragmentVisitor
    {
        //Rename "VariableReference"s
        public override void Visit(VariableReference node)
        {
            node.Name = Inliner.NewName(node.Name);
            base.Visit(node);
        }

        //Rename "DeclareVariableElement"s
        public override void Visit(DeclareVariableElement node)
        {
            node.VariableName.Value = Inliner.NewName(node.VariableName.Value);
            base.Visit(node);
        }

        //Rename "VariableReference"s of "ExecuteParameter"
        public override void ExplicitVisit(ExecuteParameter node)
        {
            if (node.ParameterValue is VariableReference)
            {
                ((VariableReference)node.ParameterValue).Name = Inliner.NewName(((VariableReference)node.ParameterValue).Name);
            }
        }

        public override void Visit(StatementList node)
        {
            //if we have a "ReturnStatement" inside stored procedures, we need to end up stored procedure code and resume the master file
            //for this purpos, create variables and set "GOTO" lable for jump to that lable
            foreach (var returnStatement in node.Statements.Where(a => a is ReturnStatement).ToList())
            {
                Inliner.hasReturnStatement = true;
                BeginEndBlockStatement returnBeginEndBlockStatement = new BeginEndBlockStatement()
                {
                    StatementList = new StatementList()
                };

                //if "ReturnStatement" has Expression, we process that ...
                //Declate variable and set value, add this variables to "returnStatementPlace" for set in top of stored procedire ...
                if (((ReturnStatement)returnStatement).Expression != null)
                {
                    DeclareVariableStatement declareVariableStatement = new DeclareVariableStatement();

                    declareVariableStatement.Declarations.Add(new DeclareVariableElement()
                    {
                        DataType = new SqlDataTypeReference()
                        {
                            SqlDataTypeOption = SqlDataTypeOption.Int
                        },
                        VariableName = new Identifier() { Value = Inliner.NewName("@ReturnValue") }
                    });

                    if (Inliner.returnStatementPlace == null || Inliner.returnStatementPlace.StatementList == null)
                        Inliner.returnStatementPlace = new BeginEndBlockStatement()
                        {
                            StatementList = new StatementList()
                        };
                    Inliner.returnStatementPlace.StatementList.Statements.Add(declareVariableStatement);

                    returnBeginEndBlockStatement.StatementList.Statements.Add(new SetVariableStatement()
                    {
                        AssignmentKind = AssignmentKind.Equals,
                        Variable = new VariableReference()
                        {
                            Name = $"@ReturnValue",
                        },
                        Expression = new IntegerLiteral()
                        {
                            Value = ((ReturnStatement)returnStatement).Expression is VariableReference ?
                            ((VariableReference)((ReturnStatement)returnStatement).Expression).Name :
                            ((IntegerLiteral)((ReturnStatement)returnStatement).Expression).Value
                        }
                    });
                }

                //set GoToStatement on the end
                returnBeginEndBlockStatement.StatementList.Statements.Add(new GoToStatement()
                {
                    LabelName = new Identifier()
                    {
                        Value = Inliner.GoToName,
                        QuoteType = QuoteType.NotQuoted
                    }
                });
                //Replace "ReturnStatement" by new value ...
                node.Statements[node.Statements.IndexOf(returnStatement)] = returnBeginEndBlockStatement;
            }
            base.Visit(node);
        }

        //for process "IfStatement", we need to append "ThenStatement" in "BeginEndBlockStatement"
        public override void Visit(IfStatement node)
        {
            if (!(node.ThenStatement is BeginEndBlockStatement))
            {
                BeginEndBlockStatement beginEndBlockStatement = new BeginEndBlockStatement()
                {
                    StatementList = new StatementList()
                };
                beginEndBlockStatement.StatementList.Statements.Add(node.ThenStatement);
                node.ThenStatement = beginEndBlockStatement;
            }
            base.Visit(node);
        }
    }
}