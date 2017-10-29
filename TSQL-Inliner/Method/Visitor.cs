using Microsoft.SqlServer.TransactSql.ScriptDom;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TSQL_Inliner.Method
{
    class MasterVisitor : TSqlConcreteFragmentVisitor
    {
        /// <summary>
        /// override 'Visit' method for process 'StatementLists'
        /// </summary>
        /// <param name="node"></param>
        public override void Visit(StatementList node)
        {
            foreach (var executeStatement in node.Statements.Where(a => a is ExecuteStatement).ToList())
            {
                var executableProcedureReference = (((ExecuteStatement)executeStatement).ExecuteSpecification.ExecutableEntity);
                var schemaIdentifier = ((ExecutableProcedureReference)executableProcedureReference).ProcedureReference.ProcedureReference.Name.SchemaIdentifier.Value;
                var baseIdentifier = ((ExecutableProcedureReference)executableProcedureReference).ProcedureReference.ProcedureReference.Name.BaseIdentifier.Value;
                var param = ((ExecutableProcedureReference)executableProcedureReference).Parameters.ToDictionary(a => a.Variable.Name, a => a.ParameterValue);

                //node.Statements[node.Statements.IndexOf(executeStatement)].ScriptTokenStream.Insert(0, new TSqlParserToken()
                //{
                //    TokenType = TSqlTokenType.SingleLineComment,
                //    Text = "=-=-=-=-= Start =-=-=-=-="
                //});
                Handler handler = new Handler();
                node.Statements[node.Statements.IndexOf(executeStatement)] = handler.HandleExecuteStatement($"[{schemaIdentifier}].[{baseIdentifier}]", param);

                //beginEndBlockStatement.ScriptTokenStream.Add(new TSqlParserToken()
                //{
                //    TokenType = TSqlTokenType.SingleLineComment,
                //    Text = "=-=-=-=-= End =-=-=-=-="
                //});
            }

            base.Visit(node);
        }
    }

    class VarVisitor : TSqlConcreteFragmentVisitor
    {
        public override void Visit(VariableReference node)
        {
            node.Name = $"{node.Name}_inliner{Handler.variableCount}";
            base.Visit(node);
        }

        /// <summary>
        /// if body has Execute Parameter, we must rename Variable References
        /// </summary>
        /// <param name="node"></param>
        public override void ExplicitVisit(ExecuteParameter node)
        {
            if (node.ParameterValue is VariableReference)
            {
                ((VariableReference)node.ParameterValue).Name = $"{((VariableReference)node.ParameterValue).Name}_inliner{Handler.variableCount}";
            }
        }

        public override void Visit(StatementList node)
        {
            foreach (var returnStatement in node.Statements.Where(a => a is ReturnStatement).ToList())
            {
                Handler.hasReturnStatement = true;
                BeginEndBlockStatement returnBeginEndBlockStatement = new BeginEndBlockStatement()
                {
                    StatementList = new StatementList()
                };

                if (((ReturnStatement)returnStatement).Expression != null)
                {
                    DeclareVariableStatement declareVariableStatement = new DeclareVariableStatement();

                    declareVariableStatement.Declarations.Add(new DeclareVariableElement()
                    {
                        DataType = new SqlDataTypeReference()
                        {
                            SqlDataTypeOption = SqlDataTypeOption.Int
                        },
                        VariableName = new Identifier() { Value = $"@ReturnValue_inliner{Handler.variableCount}" }
                    });

                    Handler.returnStatementPlace = new BeginEndBlockStatement()
                    {
                        StatementList = new StatementList()
                    };
                    Handler.returnStatementPlace.StatementList.Statements.Add(declareVariableStatement);


                    returnBeginEndBlockStatement.StatementList.Statements.Add(new SetVariableStatement()
                    {
                        AssignmentKind = AssignmentKind.Equals,
                        Variable = new VariableReference()
                        {
                            Name = $"@ReturnValue",
                        },
                        Expression = new IntegerLiteral()
                        {
                            Value = ((ReturnStatement)returnStatement).Expression is VariableReference ? ((VariableReference)((ReturnStatement)returnStatement).Expression).Name :
                            ((IntegerLiteral)((ReturnStatement)returnStatement).Expression).Value
                        }
                    });
                }
                returnBeginEndBlockStatement.StatementList.Statements.Add(new GoToStatement()
                {
                    LabelName = new Identifier()
                    {
                        Value = $"GOTO_{Handler.variableCount}",
                        QuoteType = QuoteType.NotQuoted
                    }
                });
                node.Statements[node.Statements.IndexOf(returnStatement)] = returnBeginEndBlockStatement;
            }
            base.Visit(node);
        }
    }
}