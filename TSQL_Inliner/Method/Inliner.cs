using Microsoft.SqlServer.TransactSql.ScriptDom;
using System;
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
        public static string GoToName = string.Empty;

        /// <summary>
        /// set new name for parameters based on level of srored procedure
        /// </summary>
        /// <param name="Name"></param>
        /// <returns>New Name</returns>
        public static string NewName(string Name)
        {
            return $"{Name}_inliner{variableCount}";
        }

        /// <summary>
        /// load inline stored procedure and handle that
        /// </summary>
        /// <param name="SPIdentifier">stored procedure identifier</param>
        /// <param name="Param">Variable Reference</param>
        /// <returns></returns>
        public BeginEndBlockStatement ExecuteStatement(string schema, string procedure, Dictionary<string, ScalarExpression> procedureParametersValues)
        {
            TSQLConnection tSQLConnection = new TSQLConnection();
            TSqlFragment tSqlFragment = tSQLConnection.ReadTsql(out CommentModel commentModel, out string topComments, schema, procedure);
            BeginEndBlockStatement beginEndBlockStatement = new BeginEndBlockStatement
            {
                StatementList = new StatementList()
            };

            switch (commentModel.InlineMode.ToLower())
            {
                case "inline":
                    if (commentModel.IsOptimizable && !commentModel.IsOptimized)
                    {
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
                        GoToName = $"EndOf_{schema}_{procedure}";
                        VarVisitor varVisitor = new VarVisitor();
                        beginEndBlockStatement.StatementList.Statements.FirstOrDefault(a => a is BeginEndBlockStatement).Accept(varVisitor);

                        ReturnStatement(beginEndBlockStatement);
                    }
                    break;

                case "remove":
                    DeclareVariableStatement declareVariableStatement = new DeclareVariableStatement();
                    declareVariableStatement.Declarations.Add(new DeclareVariableElement()
                    {
                        DataType = new SqlDataTypeReference()
                        {
                            SqlDataTypeOption = SqlDataTypeOption.Int
                        },
                        VariableName = new Identifier() { Value = "@Inliner_DoNothing_" + Guid.NewGuid().ToString().Replace("-", string.Empty) }
                    });
                    beginEndBlockStatement.StatementList.Statements.Add(declareVariableStatement);
                    break;

                case "none":
                    break;
            }

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
                    Value = $"{GoToName}:"
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

                declareVariableElement.VariableName.Value = Inliner.NewName(parameter.VariableName.Value);
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