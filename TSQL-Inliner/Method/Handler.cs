using Microsoft.SqlServer.TransactSql.ScriptDom;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TSQL_Inliner.Method
{
    public class Handler
    {
        #region variables

        /// <summary>
        /// Counter for make variables unique
        /// </summary>
        public static int variableCount = 0;
        public static BeginEndBlockStatement returnStatementPlace;
        public static bool hasReturnStatement = false;
        public static Dictionary<ProcedureParameter, DeclareVariableElement> outputParameters;
        #endregion

        #region Handlers
        /// <summary>
        /// load inline stored procedure and handle that
        /// </summary>
        /// <param name="SPIdentifier">stored procedure identifier</param>
        /// <param name="Param">Variable Reference</param>
        /// <returns></returns>
        public TSqlStatement HandleExecuteStatement(string SPIdentifier, Dictionary<string, ScalarExpression> procedureParametersValues)
        {
            TSQLReader tSQLReader = new TSQLReader();
            TSqlFragment tSqlFragment = tSQLReader.ReadTsql($@"C:\Users\Mohsen Hasani\Desktop\dbo.Branch_PropsGet3.sql");
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

            HandleReturnStatement(beginEndBlockStatement);

            return beginEndBlockStatement;
        }

        public void HandleReturnStatement(BeginEndBlockStatement beginEndBlockStatement)
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
                //set goto on end
                beginEndBlockStatement.StatementList.Statements.Add(new LabelStatement()
                {
                    Value = $"GOTO_{variableCount}:"
                });

                //set output parameters
                if (outputParameters.Any())
                {
                    foreach (var parameter in outputParameters)
                    {
                        beginEndBlockStatement.StatementList.Statements.Add(new SetVariableStatement()
                        {
                            AssignmentKind = AssignmentKind.Equals,
                            Variable = new VariableReference()
                            {
                                Name = parameter.Value.Value != null ?
                                (parameter.Value.Value is VariableReference ?
                                ((VariableReference)parameter.Value.Value).Name :
                                ((IntegerLiteral)parameter.Value.Value).Value) :
                                string.Empty
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

        public void HandleParameters(BeginEndBlockStatement beginEndBlockStatement,
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
        #endregion
    }
}
