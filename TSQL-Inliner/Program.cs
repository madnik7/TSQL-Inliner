using Microsoft.SqlServer.TransactSql.ScriptDom;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace TSQL_Inliner
{
    class Program
    {
        #region variables
        /// <summary>
        /// Counter for make variables unique
        /// </summary>
        private static int variableCount = 0;
        public static BeginEndBlockStatement returnStatementPlace;
        #endregion

        #region Visit Methods
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

                    node.Statements[node.Statements.IndexOf(executeStatement)] = HandleExecuteStatement($"[{schemaIdentifier}].[{baseIdentifier}]", param);

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
                node.Name = $"{node.Name}_inliner{variableCount}";
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
                    ((VariableReference)node.ParameterValue).Name = $"{((VariableReference)node.ParameterValue).Name}_inliner{variableCount}";
                }
            }

            public override void Visit(StatementList node)
            {
                if (returnStatementPlace is null || returnStatementPlace.StatementList is null)
                    returnStatementPlace = new BeginEndBlockStatement()
                    {
                        StatementList = new StatementList()
                    };

                foreach (var returnStatement in node.Statements.Where(a => a is ReturnStatement).ToList())
                {
                    DeclareVariableStatement declareVariableStatement = new DeclareVariableStatement();

                    declareVariableStatement.Declarations.Add(new DeclareVariableElement()
                    {
                        DataType = new SqlDataTypeReference()
                        {
                            SqlDataTypeOption = SqlDataTypeOption.Int
                        },
                        VariableName = new Identifier() { Value = $"@ReturnValue_inliner{variableCount}" }
                    });
                    returnStatementPlace.StatementList.Statements.Add(declareVariableStatement);

                    //////////////
                    BeginEndBlockStatement returnBeginEndBlockStatement = new BeginEndBlockStatement()
                    {
                        StatementList = new StatementList()
                    };
                    returnBeginEndBlockStatement.StatementList.Statements.Add(new SetVariableStatement()
                    {
                        Variable = new VariableReference()
                        {
                            Name = $"@ReturnValue",
                            Collation = new Identifier()
                            {
                                Value = "123",
                                QuoteType = QuoteType.NotQuoted
                            }
                        },
                        AssignmentKind = AssignmentKind.Equals
                    });

                    returnBeginEndBlockStatement.StatementList.Statements.Add(new GoToStatement()
                    {
                        LabelName = new Identifier()
                        {
                            Value = $"GOTO_{variableCount}",
                            QuoteType = QuoteType.NotQuoted
                        }
                    });
                    node.Statements[node.Statements.IndexOf(returnStatement)] = returnBeginEndBlockStatement;
                }

                base.Visit(node);
            }
        }
        #endregion

        #region Main
        static void Main(string[] args)
        {
            Sql140ScriptGenerator sql140ScriptGenerator = new Sql140ScriptGenerator();
            sql140ScriptGenerator.GenerateScript(ReadTsql(@"C:\Users\Mohsen Hasani\Desktop\api.Branch_PropsGet1.sql"), out string str);
            Console.WriteLine(str);
            Console.ReadKey();
        }
        #endregion

        #region get sql code
        protected static TSqlFragment ReadTsql(string LocalAddress)
        {
            var parser = new TSql140Parser(true);
            var fragment = parser.Parse(new StreamReader(LocalAddress), out IList<ParseError> errors);

            MasterVisitor myVisitor = new MasterVisitor();
            fragment.Accept(myVisitor);

            return fragment;
        }
        #endregion

        #region Handlers
        /// <summary>
        /// load inline stored procedure and handle that
        /// </summary>
        /// <param name="SPIdentifier">stored procedure identifier</param>
        /// <param name="Param">Variable Reference</param>
        /// <returns></returns>
        protected static TSqlStatement HandleExecuteStatement(string SPIdentifier, Dictionary<string, ScalarExpression> procedureParametersValues)
        {
            //TSqlFragment tSqlFragment = ReadTsql($@"C:\Users\Mohsen Hasani\Desktop\{SPIdentifier}.sql");
            TSqlFragment tSqlFragment = ReadTsql($@"C:\Users\Mohsen Hasani\Desktop\dbo.Branch_PropsGet3.sql");
            Sql140ScriptGenerator sql140ScriptGenerator = new Sql140ScriptGenerator();

            var batche = ((TSqlScript)tSqlFragment).Batches.FirstOrDefault(a => a.Statements.Any(b => b is AlterProcedureStatement));
            AlterProcedureStatement alterProcedureStatement = (AlterProcedureStatement)batche.Statements.FirstOrDefault(a => a is AlterProcedureStatement);

            //get all stored procedure parameters for declare inline
            var alterProcedureStatementParameters = alterProcedureStatement.Parameters.Select(a => a).ToList();

            BeginEndBlockStatement beginEndBlockStatement = new BeginEndBlockStatement
            {
                StatementList = new StatementList()
            };

            HandleParameters(beginEndBlockStatement, alterProcedureStatementParameters, procedureParametersValues);

            beginEndBlockStatement.StatementList.Statements.Add(alterProcedureStatement.StatementList.Statements.FirstOrDefault(a => a is BeginEndBlockStatement));

            VarVisitor varVisitor = new VarVisitor();
            beginEndBlockStatement.StatementList.Statements.FirstOrDefault(a => a is BeginEndBlockStatement).Accept(varVisitor);

            #region Return values 
            if (returnStatementPlace.StatementList.Statements.Any())
            {
                //declare variables on top
                foreach (var statement in returnStatementPlace.StatementList.Statements)
                {
                    beginEndBlockStatement.StatementList.Statements.Insert(0, statement);
                }

                //set goto on end
                beginEndBlockStatement.StatementList.Statements.Add(new LabelStatement()
                {
                    Value = $"GOTO_{variableCount}:"
                });
            }
            #endregion

            return beginEndBlockStatement;
        }

        //protected static void HandleReturnStatement(List<TSqlStatement> sqlStatements, BeginEndBlockStatement returnStatementPlace)
        //{
        //    foreach (var sqlStatement in sqlStatements)
        //    {
        //        if (sqlStatement is BeginEndBlockStatement)
        //        {
        //            HandleReturnStatement(((BeginEndBlockStatement)sqlStatement).StatementList.Statements.ToList(), returnStatementPlace);
        //        }
        //        if (sqlStatement is IfStatement && ((IfStatement)sqlStatement).ThenStatement is BeginEndBlockStatement)
        //        {
        //            HandleReturnStatement(((BeginEndBlockStatement)((IfStatement)sqlStatement).ThenStatement).StatementList.Statements.ToList(), returnStatementPlace);
        //        }
        //    }
        //    if (sqlStatements.Any(a => a is ReturnStatement))
        //    {
        //        foreach (var returnStatement in sqlStatements.Where(a => a is ReturnStatement).ToList())
        //        {
        //            DeclareVariableStatement declareVariableStatement = new DeclareVariableStatement();
        //            declareVariableStatement.Declarations.Add(new DeclareVariableElement()
        //            {
        //                Value = ((ReturnStatement)returnStatement).Expression,
        //                VariableName = new Identifier() { Value = $"@ReturnValue_inliner{variableCount}" }
        //            });
        //            sqlStatements.Remove(returnStatement);
        //            returnStatementPlace.StatementList.Statements.Insert(0, declareVariableStatement);
        //        }
        //    }
        //}

        protected static void HandleParameters(BeginEndBlockStatement beginEndBlockStatement,
            List<ProcedureParameter> ProcedureParameters,
            Dictionary<string, ScalarExpression> ProcedureParametersValues)
        {
            variableCount++;
            DeclareVariableStatement declareVariableStatement = new DeclareVariableStatement();
            foreach (var parameter in ProcedureParameters)
            {
                DeclareVariableElement declareVariableElement = new DeclareVariableElement()
                {
                    DataType = parameter.DataType,
                    VariableName = parameter.VariableName,
                    Nullable = parameter.Nullable,
                    Value = parameter.Value
                };

                if (ProcedureParametersValues.Any(a => a.Key == declareVariableElement.VariableName.Value))
                {
                    declareVariableElement.Value = ProcedureParametersValues.FirstOrDefault(a => a.Key == declareVariableElement.VariableName.Value).Value;
                }
                else
                {
                    declareVariableElement.Value = null;
                }

                declareVariableElement.VariableName.Value = $"{parameter.VariableName.Value}_inliner{variableCount}";
                declareVariableStatement.Declarations.Add(declareVariableElement);
            }

            beginEndBlockStatement.StatementList.Statements.Add(declareVariableStatement);
        }
        #endregion
    }
}