using Microsoft.SqlServer.TransactSql.ScriptDom;
using System;
using System.Collections.Generic;
using System.Linq;
using TSQL_Inliner.Model;
using TSQL_Inliner.Process;

namespace TSQL_Inliner.Visitor
{
    class ExecuteVisitor : TSqlConcreteFragmentVisitor
    {
        Inliner Inliner { get { return Program.Inliner; } }
        Dictionary<ProcedureParameter, DeclareVariableElement> OutputParameters { get; set; }
        StatementVisitor StatementVisitor { get; set; }
        

        bool IsOptimized { get; set; }
        public ExecuteVisitor(bool isOptimized)
        {
            IsOptimized = isOptimized;
            StatementVisitor = new StatementVisitor();
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
            if (!IsOptimized)
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
            var namedValues = executableProcedureReference.Parameters.Where(a => a.Variable != null && !string.IsNullOrEmpty(a.Variable.Name)).ToDictionary(a => a.Variable.Name, a => a.ParameterValue);
            var unnamedValues = executableProcedureReference.Parameters.Where(a => a.Variable == null).Select(a => a.ParameterValue).ToList();

            BeginEndBlockStatement newBody = new BeginEndBlockStatement();
            //optimize the procedure
            if (!Inliner.ProcessedProcdures.Any(a => a == $"{spInfo.Schema}.{spInfo.Name}"))
            {
                Inliner.ProcessedProcdures.Add($"{spInfo.Schema}.{spInfo.Name}");
                Inliner.Process(spInfo);
            }

            newBody = ExecuteStatement(spInfo, namedValues, unnamedValues);
            return newBody;
        }

        /// <summary>
        /// load inline stored procedure and handle that
        /// </summary>
        /// <param name="SPIdentifier">stored procedure identifier</param>
        /// <param name="Param">Variable Reference</param>
        /// <returns></returns>
        public BeginEndBlockStatement ExecuteStatement(SpInfo spInfo, Dictionary<string, ScalarExpression> namedValues, List<ScalarExpression> unnamedValues)
        {
            TSQLConnection tSQLConnection = new TSQLConnection();
            ProcModel procModel = Inliner.GetProcModel(spInfo);
            TSqlFragment tSqlFragment = procModel.TSqlFragment;
            BeginEndBlockStatement beginEndBlockStatement = new BeginEndBlockStatement
            {
                StatementList = new StatementList()
            };

            switch (procModel.CommentModel.InlineMode.ToLower())
            {
                case "inline":
                    var batche = ((TSqlScript)tSqlFragment).Batches.FirstOrDefault(a => a.Statements.Any(b => b is AlterProcedureStatement));
                    if (batche != null)
                    {
                        AlterProcedureStatement alterProcedureStatement = (AlterProcedureStatement)batche.Statements.FirstOrDefault(a => a is AlterProcedureStatement);

                        Parameters(beginEndBlockStatement, alterProcedureStatement.Parameters.ToList(), namedValues, unnamedValues);

                        beginEndBlockStatement.StatementList.Statements.Add(alterProcedureStatement.StatementList.Statements.FirstOrDefault(a => a is BeginEndBlockStatement));
                    }
                    else
                    {
                        batche = ((TSqlScript)tSqlFragment).Batches.FirstOrDefault(a => a.Statements.Any(b => b is CreateProcedureStatement));

                        CreateProcedureStatement createProcedureStatement = (CreateProcedureStatement)batche.Statements.FirstOrDefault(a => a is CreateProcedureStatement);

                        Parameters(beginEndBlockStatement, createProcedureStatement.Parameters.ToList(), namedValues, unnamedValues);

                        beginEndBlockStatement.StatementList.Statements.Add(createProcedureStatement.StatementList.Statements.FirstOrDefault(a => a is BeginEndBlockStatement));
                    }

                    Inliner.GoToName = $"EndOf_{procModel.SpInfo.Schema}_{procModel.SpInfo.Name}";

                    beginEndBlockStatement.StatementList.Statements.FirstOrDefault(a => a is BeginEndBlockStatement).Accept(StatementVisitor);

                    ReturnStatement(beginEndBlockStatement);
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

            if (Inliner.hasReturnStatement)
            {
                //insert goto on end
                beginEndBlockStatement.StatementList.Statements.Add(new LabelStatement()
                {
                    Value = $"{Inliner.GoToName}:"
                });

                //set output parameters
                if (OutputParameters != null && OutputParameters.Any())
                {
                    foreach (var parameter in OutputParameters.Where(a => a.Value.Value != null))
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

        public void Parameters(BeginEndBlockStatement beginEndBlockStatement, List<ProcedureParameter> ProcedureParameters,
            Dictionary<string, ScalarExpression> namedValues, List<ScalarExpression> unnamedValues)
        {
            Inliner.IncreaseVariableCount();

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

                if (unnamedValues != null && unnamedValues.Any() && unnamedValuesCounter < unnamedValues.Count())
                {
                    declareVariableElement.Value = unnamedValues[unnamedValuesCounter++];
                }
                else
                {
                    declareVariableElement.Value = namedValues.Any(a => a.Key == declareVariableElement.VariableName.Value) ?
                    namedValues.FirstOrDefault(a => a.Key == declareVariableElement.VariableName.Value).Value : null;
                }

                declareVariableElement.VariableName.Value = Inliner.NewName(parameter.VariableName.Value);

                declareVariableStatement.Declarations.Add(declareVariableElement);

                if (parameter.Modifier == ParameterModifier.Output)
                {
                    if (OutputParameters == null)
                        OutputParameters = new Dictionary<ProcedureParameter, DeclareVariableElement>();
                    OutputParameters.Add(parameter, declareVariableElement);
                }
            }

            beginEndBlockStatement.StatementList.Statements.Add(declareVariableStatement);
        }

        #endregion
    }
}