using Microsoft.SqlServer.TransactSql.ScriptDom;
using System;
using System.Collections.Generic;
using System.Linq;
using TSQL_Inliner.Model;
using TSQL_Inliner.ProcOptimization;

namespace TSQL_Inliner.Inliner
{
    public class ExecuteInliner
    {
        Dictionary<ProcedureParameter, DeclareVariableElement> OutputParameters { get; set; }
        StatementVisitor StatementVisitor { get; set; }
        ProcOptimizer ProcOptimizer { get { return Program.ProcOptimizer; } }
        int VariableCounter;
        bool IsFunction { get; set; }

        public ExecuteInliner(bool isFunction = false)
        {
            Program.ProcOptimizer.IncreaseVariableCounter();
            VariableCounter = Program.ProcOptimizer.VariableCounter;
            StatementVisitor = new StatementVisitor(VariableCounter);
            IsFunction = isFunction;
        }

        public BeginEndBlockStatement GetStatementAsInline(SpInfo spInfo, List<ScalarExpression> unnamedValues, Dictionary<string, ScalarExpression> namedValues = null, string setVariableReferenceName = null)
        {
            BeginEndBlockStatement beginEndBlockStatement = new BeginEndBlockStatement
            {
                StatementList = new StatementList()
            };
            ProcModel procModel = ProcOptimizer.GetProcModel(spInfo);
            if (procModel.TSqlFragment != null)
            {
                TSqlFragment tSqlFragment = procModel.TSqlFragment;

                switch (procModel.CommentModel.InlineMode.ToLower())
                {
                    case "inline":

                        TSqlBatch batche = ((TSqlScript)tSqlFragment).Batches.FirstOrDefault(a => a.Statements.Any(b => b is CreateProcedureStatement));
                        if (batche != null)
                        {
                            CreateProcedureStatement createProcedureStatement = (CreateProcedureStatement)batche.Statements.FirstOrDefault(a => a is CreateProcedureStatement);

                            if (createProcedureStatement.Parameters.Any(a => a.DataType is UserDataTypeReference))
                                goto DoNothing;

                            ProcOptimizer.FunctionReturnType = null;

                            Parameters(beginEndBlockStatement, createProcedureStatement.Parameters.ToList(), unnamedValues, namedValues);

                            beginEndBlockStatement.StatementList.Statements.Add(createProcedureStatement.StatementList.Statements.FirstOrDefault(a => a is BeginEndBlockStatement));
                        }
                        else
                        {
                            batche = ((TSqlScript)tSqlFragment).Batches.FirstOrDefault(a => a.Statements.Any(b => b is CreateFunctionStatement));

                            CreateFunctionStatement createFunctionStatement = (CreateFunctionStatement)batche.Statements.FirstOrDefault(a => a is CreateFunctionStatement);

                            if (createFunctionStatement.Parameters.Any(a => a.DataType is UserDataTypeReference))
                                goto DoNothing;

                            if (createFunctionStatement.ReturnType is ScalarFunctionReturnType &&
                                ((ScalarFunctionReturnType)createFunctionStatement.ReturnType).DataType is UserDataTypeReference)
                                goto DoNothing;

                            ProcOptimizer.FunctionReturnType = createFunctionStatement.ReturnType;

                            Parameters(beginEndBlockStatement, createFunctionStatement.Parameters.ToList(), unnamedValues, namedValues);

                            beginEndBlockStatement.StatementList.Statements.Add(createFunctionStatement.StatementList.Statements.FirstOrDefault(a => a is BeginEndBlockStatement));
                        }

                        ProcOptimizer.GoToName = ProcOptimizer.BuildNewName($"EndOf_{spInfo.Schema}_{spInfo.Name}", VariableCounter);

                        beginEndBlockStatement.StatementList.Statements.FirstOrDefault(a => a is BeginEndBlockStatement).Accept(StatementVisitor);

                        ReturnStatement(beginEndBlockStatement, setVariableReferenceName);
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
                        DoNothing:
                        break;
                }
            }
            return beginEndBlockStatement;
        }

        public void ReturnStatement(BeginEndBlockStatement beginEndBlockStatement, string setVariableReferenceName = null)
        {
            if (StatementVisitor._returnStatementPlace != null &&
                StatementVisitor._returnStatementPlace.StatementList != null &&
                StatementVisitor._returnStatementPlace.StatementList.Statements.Any())
            {
                //declare variables on top
                foreach (var statement in StatementVisitor._returnStatementPlace.StatementList.Statements)
                {
                    beginEndBlockStatement.StatementList.Statements.Insert(0, statement);
                }
            }

            //insert goto on end
            beginEndBlockStatement.StatementList.Statements.Add(new LabelStatement()
            {
                Value = $"{ProcOptimizer.GoToName}:"
            });

            beginEndBlockStatement.StatementList.Statements.FirstOrDefault(a => a is LabelStatement).Accept(StatementVisitor);

            //set output parameters
            if (OutputParameters != null && OutputParameters.Any())
            {
                foreach (var parameter in OutputParameters.Where(a => a.Value.Value != null && (a.Value.Value is VariableReference || a.Value.Value is IntegerLiteral)))
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

            if (!string.IsNullOrEmpty(setVariableReferenceName))
            {
                beginEndBlockStatement.StatementList.Statements.Add(new SetVariableStatement()
                {
                    AssignmentKind = AssignmentKind.Equals,
                    Variable = new VariableReference()
                    {
                        Name = setVariableReferenceName
                    },
                    Expression = new IntegerLiteral()
                    {
                        Value = Program.ProcOptimizer.BuildNewName("@ReturnValue", VariableCounter)
                    }
                });
            }
        }

        public void Parameters(BeginEndBlockStatement beginEndBlockStatement, List<ProcedureParameter> ProcedureParameters,
             List<ScalarExpression> unnamedValues, Dictionary<string, ScalarExpression> namedValues = null)
        {
            int unnamedValuesCounter = 0;
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

                declareVariableElement.VariableName.Value = ProcOptimizer.BuildNewName(parameter.VariableName.Value, VariableCounter);

                if (unnamedValues != null && unnamedValues.Any() && unnamedValuesCounter < unnamedValues.Count())
                {
                    declareVariableElement.Value = unnamedValues[unnamedValuesCounter++];
                }
                else if (namedValues != null && namedValues.Any(a => a.Key == declareVariableElement.VariableName.Value.Substring(0, declareVariableElement.VariableName.Value.IndexOf("_inliner"))))
                {
                    declareVariableElement.Value = namedValues.FirstOrDefault(a => a.Key == declareVariableElement.VariableName.Value.Substring(0, declareVariableElement.VariableName.Value.IndexOf("_inliner"))).Value;
                }

                declareVariableStatement.Declarations.Add(declareVariableElement);

                if (parameter.Modifier == ParameterModifier.Output)
                {
                    if (OutputParameters == null)
                        OutputParameters = new Dictionary<ProcedureParameter, DeclareVariableElement>();
                    OutputParameters.Add(parameter, declareVariableElement);
                }
            }

            if (declareVariableStatement.Declarations != null && declareVariableStatement.Declarations.Any())
                beginEndBlockStatement.StatementList.Statements.Add(declareVariableStatement);
        }
    }
}