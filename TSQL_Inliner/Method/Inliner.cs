using Microsoft.SqlServer.TransactSql.ScriptDom;
using System.Collections.Generic;
using System.Linq;
using TSQL_Inliner.Model;

namespace TSQL_Inliner.Method
{
    public class Inliner
    {
        /// <summary>
        /// Counter for make variables unique
        /// </summary>
        public static int variableCount = 0;
        public static BeginEndBlockStatement returnStatementPlace;
        public static bool hasReturnStatement = false;
        public static Dictionary<ProcedureParameter, DeclareVariableElement> outputParameters;


        /// <summary>
        /// load inline stored procedure and handle that
        /// </summary>
        /// <param name="SPIdentifier">stored procedure identifier</param>
        /// <param name="Param">Variable Reference</param>
        /// <returns></returns>
        public TSqlStatement ExecuteStatement(string schema, string procedure, Dictionary<string, ScalarExpression> procedureParametersValues)
        {
            TSQLConnection tSQLReader = new TSQLConnection();
            TSqlFragment tSqlFragment = tSQLReader.ReadTsql(out CommentModel commentModel, out string topComments, schema, procedure);
            Sql140ScriptGenerator sql140ScriptGenerator = new Sql140ScriptGenerator();
            BeginEndBlockStatement beginEndBlockStatement = new BeginEndBlockStatement
            {
                StatementList = new StatementList()
            };

            var batche = ((TSqlScript)tSqlFragment).Batches.FirstOrDefault(a => a.Statements.Any(b => b is AlterProcedureStatement));
            if (batche != null)
            {
                AlterProcedureStatement alterProcedureStatement = (AlterProcedureStatement)batche.Statements.FirstOrDefault(a => a is AlterProcedureStatement);

                Parameters(beginEndBlockStatement, alterProcedureStatement.Parameters.ToList(), procedureParametersValues);

                beginEndBlockStatement.StatementList.Statements.Add(alterProcedureStatement.StatementList.Statements.FirstOrDefault(a => a is BeginEndBlockStatement));
            }
            else
            {
                batche = ((TSqlScript)tSqlFragment).Batches.FirstOrDefault(a => a.Statements.Any(b => b is CreateProcedureStatement));

                CreateProcedureStatement createProcedureStatement = (CreateProcedureStatement)batche.Statements.FirstOrDefault(a => a is CreateProcedureStatement);

                Parameters(beginEndBlockStatement, createProcedureStatement.Parameters.ToList(), procedureParametersValues);

                beginEndBlockStatement.StatementList.Statements.Add(createProcedureStatement.StatementList.Statements.FirstOrDefault(a => a is BeginEndBlockStatement));
            }

            if (commentModel.IsOptimizable && !commentModel.IsOptimized)
            {
                VarVisitor varVisitor = new VarVisitor();
                beginEndBlockStatement.StatementList.Statements.FirstOrDefault(a => a is BeginEndBlockStatement).Accept(varVisitor);
            }

            ReturnStatement(beginEndBlockStatement);

            return beginEndBlockStatement;
        }

        public void ReturnStatement(BeginEndBlockStatement beginEndBlockStatement)
        {
            if (returnStatementPlace != null && returnStatementPlace.StatementList != null && returnStatementPlace.StatementList.Statements.Any())
            {
                //declare variables on top
                foreach (var statement in returnStatementPlace.StatementList.Statements)
                {
                    beginEndBlockStatement.StatementList.Statements.Insert(0, statement);
                }
            }

            if (hasReturnStatement)
            {
                //insert goto on end
                beginEndBlockStatement.StatementList.Statements.Add(new LabelStatement()
                {
                    Value = $"GOTO_{variableCount}:"
                });

                //set output parameters
                if (outputParameters != null && outputParameters.Any())
                {
                    foreach (var parameter in outputParameters.Where(a => a.Value.Value != null))
                    {
                        beginEndBlockStatement.StatementList.Statements.Add(new SetVariableStatement()
                        {
                            AssignmentKind = AssignmentKind.Equals,
                            Variable = new VariableReference()
                            {
                                Name = (parameter.Value.Value is VariableReference ?
                                ((VariableReference)parameter.Value.Value).Name :
                                ((IntegerLiteral)parameter.Value.Value).Value)
                            },
                            Expression = new IntegerLiteral()
                            {
                                Value = parameter.Key.VariableName.Value
                            }
                        });
                    }
                }
            }
        }

        public void Parameters(BeginEndBlockStatement beginEndBlockStatement,
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

                if (parameter.Modifier == ParameterModifier.Output)
                {
                    if (outputParameters == null)
                        outputParameters = new Dictionary<ProcedureParameter, DeclareVariableElement>();
                    outputParameters.Add(parameter, declareVariableElement);
                }
            }

            beginEndBlockStatement.StatementList.Statements.Add(declareVariableStatement);
        }
    }
}