using Microsoft.SqlServer.TransactSql.ScriptDom;
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
                    string setVariableReferenceName = executeStatement.ExecuteSpecification.Variable is VariableReference ? executeStatement.ExecuteSpecification.Variable.Name : null;
                    var newBody = ExecuteStatement(executableProcedureReference, setVariableReferenceName);
                    node.Statements[node.Statements.IndexOf(executeStatement)] = newBody;
                    newStatements.Add(newBody);
                }
            }

            foreach (SetVariableStatement setVariableStatement in node.Statements.Where(a => a is SetVariableStatement).ToList())
            {
                if (setVariableStatement.Expression is FunctionCall functionCall && functionCall.CallTarget != null && functionCall.CallTarget is MultiPartIdentifierCallTarget)
                {
                    var newBody = ExecuteFunctionStatement(functionCall, setVariableStatement.Variable.Name);
                    node.Statements[node.Statements.IndexOf(setVariableStatement)] = newBody;
                    newStatements.Add(newBody);
                }
            }

            foreach (DeclareVariableStatement declareVariableStatement in node.Statements.Where(a => a is DeclareVariableStatement).ToList())
            {
                foreach (var declaration in declareVariableStatement.Declarations)
                    if (declaration.Value is FunctionCall functionCall && functionCall.CallTarget != null && functionCall.CallTarget is MultiPartIdentifierCallTarget)
                    {
                        var newBody = ExecuteFunctionStatement((FunctionCall)declaration.Value, declaration.VariableName.Value);
                        declaration.Value = new NullLiteral() { Value = null };
                        node.Statements.Insert(node.Statements.IndexOf(declareVariableStatement) + 1, newBody);
                        newStatements.Add(newBody);
                    }
            }

            foreach (ReturnStatement returnStatement in node.Statements.Where(a => a is ReturnStatement).ToList())
            {
                if (returnStatement.Expression is FunctionCall functionCall && functionCall.CallTarget != null && functionCall.CallTarget is MultiPartIdentifierCallTarget)
                {
                    var newBody = ExecuteFunctionStatement((FunctionCall)returnStatement.Expression, isReturn: true);
                    returnStatement.Expression = null;
                    node.Statements[node.Statements.IndexOf(returnStatement)] = newBody;
                    newStatements.Add(newBody);
                }
            }

            //Handle Function/StoredProcedure call in IfStatement
            foreach (IfStatement ifStatement in node.Statements.Where(a => a is IfStatement).ToList())
            {
                if (ifStatement.Predicate is BooleanParenthesisExpression &&
                    ((BooleanParenthesisExpression)ifStatement.Predicate).Expression is BooleanComparisonExpression booleanComparisonExpression &&
                    booleanComparisonExpression.FirstExpression is FunctionCall functionCall &&
                    functionCall.CallTarget != null && functionCall.CallTarget is MultiPartIdentifierCallTarget)
                {
                    var newBody = ExecuteFunctionStatement(functionCall);
                    node.Statements.Insert(node.Statements.IndexOf(ifStatement), newBody);
                    newStatements.Add(newBody);

                    ((BooleanComparisonExpression)((BooleanParenthesisExpression)ifStatement.Predicate).Expression).FirstExpression = new VariableReference()
                    {
                        Name = Program.ProcOptimizer.BuildNewName("@ReturnValue", ProcOptimizer.VariableCounter)
                    };
                }

                if (ifStatement.ThenStatement is ExecuteStatement executeStatement)
                {
                    var executableProcedureReference = (ExecutableProcedureReference)executeStatement.ExecuteSpecification.ExecutableEntity;
                    if (executableProcedureReference.ProcedureReference.ProcedureReference.Name.DatabaseIdentifier == null)
                    {
                        string setVariableReferenceName = executeStatement.ExecuteSpecification.Variable is VariableReference ? executeStatement.ExecuteSpecification.Variable.Name : null;
                        var newBody = ExecuteStatement(executableProcedureReference, setVariableReferenceName);
                        ifStatement.ThenStatement = newBody;
                        newStatements.Add(newBody);
                    }
                }
                else if (ifStatement.ThenStatement is SetVariableStatement setVariableStatement)
                {
                    if (setVariableStatement.Expression is FunctionCall functionCallExpression && functionCallExpression.CallTarget != null && functionCallExpression.CallTarget is MultiPartIdentifierCallTarget)
                    {
                        var newBody = ExecuteFunctionStatement((FunctionCall)setVariableStatement.Expression, setVariableStatement.Variable.Name);
                        ifStatement.ThenStatement = newBody;
                        newStatements.Add(newBody);
                    }
                }
                else if (ifStatement.ThenStatement is ReturnStatement returnStatement)
                {
                    if (returnStatement.Expression is FunctionCall functionCallExpression && functionCallExpression.CallTarget != null && functionCallExpression.CallTarget is MultiPartIdentifierCallTarget)
                    {
                        var newBody = ExecuteFunctionStatement((FunctionCall)returnStatement.Expression, isReturn: true);
                        ifStatement.ThenStatement = newBody;
                        newStatements.Add(newBody);
                    }
                }
            }

            //set 'DeleteStatement' after any 'DeclareTableVariableStatement'
            foreach (DeclareTableVariableStatement declareTableVariableStatement in node.Statements.Where(a => a is DeclareTableVariableStatement).ToList())
            {
                BeginEndBlockStatement deleteBeginEndBlockStatement = new BeginEndBlockStatement()
                {
                    StatementList = new StatementList()
                };

                DeleteStatement deleteStatement = new DeleteStatement()
                {
                    DeleteSpecification = new DeleteSpecification()
                    {
                        Target = new VariableTableReference()
                        {
                            Variable = new VariableReference()
                            {
                                Name = declareTableVariableStatement.Body.VariableName.Value
                            }
                        }
                    }
                };

                deleteBeginEndBlockStatement.StatementList.Statements.Add(deleteStatement);
                node.Statements.Insert(node.Statements.IndexOf(declareTableVariableStatement) + 1, deleteBeginEndBlockStatement);
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

        private BeginEndBlockStatement ExecuteStatement(ExecutableProcedureReference executableProcedureReference, string setVariableReferenceName = null)
        {
            SpInfo spInfo = new SpInfo
            {
                Schema = executableProcedureReference.ProcedureReference.ProcedureReference.Name.SchemaIdentifier == null ?
                "dbo" : executableProcedureReference.ProcedureReference.ProcedureReference.Name.SchemaIdentifier.Value,
                Name = executableProcedureReference.ProcedureReference.ProcedureReference.Name.BaseIdentifier.Value
            };

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
            BeginEndBlockStatement newBody = executeInliner.GetStatementAsInline(spInfo, unnamedValues, namedValues, setVariableReferenceName);
            return newBody;
        }

        private BeginEndBlockStatement ExecuteFunctionStatement(FunctionCall functionCall, string setVariableReferenceName = null, bool isReturn = false)
        {
            SpInfo spInfo = new SpInfo
            {
                Schema = functionCall.CallTarget == null ? "dbo" : (((MultiPartIdentifierCallTarget)functionCall.CallTarget).MultiPartIdentifier.Identifiers).FirstOrDefault().Value,
                Name = functionCall.FunctionName.Value
            };

            //optimize the procedure
            if (!ProcOptimizer.ProcessedProcdures.Any(a => a == $"{spInfo.Schema}.{spInfo.Name}"))
            {
                ProcOptimizer.ProcessedProcdures.Add($"{spInfo.Schema}.{spInfo.Name}");
                ProcOptimizer.Process(spInfo);
            }

            ExecuteInliner executeInliner = new ExecuteInliner();
            setVariableReferenceName = setVariableReferenceName ?? ProcOptimizer.BuildNewName("@ReturnValue", ProcOptimizer.VariableCounter);
            BeginEndBlockStatement newBody = executeInliner.GetStatementAsInline(spInfo, functionCall.Parameters.ToList(), null, setVariableReferenceName);
            if (isReturn)
                newBody.StatementList.Statements.Add(new ReturnStatement()
                {
                    Expression = new VariableReference()
                    {
                        Name = ProcOptimizer.BuildNewName("@ReturnValue", ProcOptimizer.VariableCounter)
                    }
                });
            return newBody;
        }

        #endregion
    }
}