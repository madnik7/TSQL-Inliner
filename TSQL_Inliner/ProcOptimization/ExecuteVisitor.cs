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

        //public override void Visit(SelectStatement node)
        //{
        //    if (node.QueryExpression is QuerySpecification querySpecification && querySpecification.WhereClause != null)
        //    {
        //        if (querySpecification.WhereClause.SearchCondition is BooleanComparisonExpression)
        //        {
        //            if (((BooleanComparisonExpression)querySpecification.WhereClause.SearchCondition).FirstExpression is FunctionCall firstExpression)
        //            {
        //                SpInfo spInfo = new SpInfo
        //                {
        //                    Schema = firstExpression.CallTarget == null ? "dbo" : (((MultiPartIdentifierCallTarget)firstExpression.CallTarget).MultiPartIdentifier.Identifiers).FirstOrDefault().Value,
        //                    Name = firstExpression.FunctionName.Value
        //                };

        //                //optimize the procedure
        //                if (!ProcOptimizer.ProcessedProcdures.Any(a => a == $"{spInfo.Schema}.{spInfo.Name}"))
        //                {
        //                    ProcOptimizer.ProcessedProcdures.Add($"{spInfo.Schema}.{spInfo.Name}");
        //                    ProcOptimizer.Process(spInfo);
        //                }
        //                ((BooleanComparisonExpression)querySpecification.WhereClause.SearchCondition).FirstExpression = ReturnVisitorHandler(firstExpression);
        //            }
        //            if (((BooleanComparisonExpression)querySpecification.WhereClause.SearchCondition).SecondExpression is FunctionCall secondExpression)
        //            {
        //                SpInfo spInfo = new SpInfo
        //                {
        //                    Schema = secondExpression.CallTarget == null ? "dbo" : (((MultiPartIdentifierCallTarget)secondExpression.CallTarget).MultiPartIdentifier.Identifiers).FirstOrDefault().Value,
        //                    Name = secondExpression.FunctionName.Value
        //                };

        //                //optimize the procedure
        //                if (!ProcOptimizer.ProcessedProcdures.Any(a => a == $"{spInfo.Schema}.{spInfo.Name}"))
        //                {
        //                    ProcOptimizer.ProcessedProcdures.Add($"{spInfo.Schema}.{spInfo.Name}");
        //                    ProcOptimizer.Process(spInfo);
        //                }
        //                ((BooleanComparisonExpression)querySpecification.WhereClause.SearchCondition).FirstExpression = ReturnVisitorHandler(secondExpression);
        //            }
        //        }
        //        else if (querySpecification.WhereClause.SearchCondition is BooleanBinaryExpression booleanBinaryExpression)
        //        {
        //            querySpecification.WhereClause.SearchCondition = SelectHandler(booleanBinaryExpression);
        //        }

        //        foreach (SelectSetVariable selectSetVariable in querySpecification.SelectElements.Where(a => a is SelectSetVariable))
        //        {
        //            if (selectSetVariable.Expression is FunctionCall functionCall)
        //            {
        //                SpInfo spInfo = new SpInfo
        //                {
        //                    Schema = functionCall.CallTarget == null ? "dbo" : (((MultiPartIdentifierCallTarget)functionCall.CallTarget).MultiPartIdentifier.Identifiers).FirstOrDefault().Value,
        //                    Name = functionCall.FunctionName.Value
        //                };

        //                //optimize the procedure
        //                if (!ProcOptimizer.ProcessedProcdures.Any(a => a == $"{spInfo.Schema}.{spInfo.Name}"))
        //                {
        //                    ProcOptimizer.ProcessedProcdures.Add($"{spInfo.Schema}.{spInfo.Name}");
        //                    ProcOptimizer.Process(spInfo);
        //                }
        //                selectSetVariable.Expression = ReturnVisitorHandler((FunctionCall)selectSetVariable.Expression);
        //            }
        //        }
        //    }
        //    base.Visit(node);
        //}

        //public override void Visit(UpdateStatement node)
        //{
        //    if (node.UpdateSpecification.WhereClause != null && node.UpdateSpecification.WhereClause.SearchCondition is BooleanComparisonExpression)
        //    {
        //        if (((BooleanComparisonExpression)node.UpdateSpecification.WhereClause.SearchCondition).FirstExpression is FunctionCall firstExpression)
        //        {
        //            SpInfo spInfo = new SpInfo
        //            {
        //                Schema = firstExpression.CallTarget == null ? "dbo" : (((MultiPartIdentifierCallTarget)firstExpression.CallTarget).MultiPartIdentifier.Identifiers).FirstOrDefault().Value,
        //                Name = firstExpression.FunctionName.Value
        //            };

        //            //optimize the procedure
        //            if (!ProcOptimizer.ProcessedProcdures.Any(a => a == $"{spInfo.Schema}.{spInfo.Name}"))
        //            {
        //                ProcOptimizer.ProcessedProcdures.Add($"{spInfo.Schema}.{spInfo.Name}");
        //                ProcOptimizer.Process(spInfo);
        //            }
        //            ((BooleanComparisonExpression)node.UpdateSpecification.WhereClause.SearchCondition).FirstExpression = ReturnVisitorHandler(firstExpression);
        //        }
        //        if (((BooleanComparisonExpression)node.UpdateSpecification.WhereClause.SearchCondition).SecondExpression is FunctionCall secondExpression)
        //        {
        //            SpInfo spInfo = new SpInfo
        //            {
        //                Schema = secondExpression.CallTarget == null ? "dbo" : (((MultiPartIdentifierCallTarget)secondExpression.CallTarget).MultiPartIdentifier.Identifiers).FirstOrDefault().Value,
        //                Name = secondExpression.FunctionName.Value
        //            };

        //            //optimize the procedure
        //            if (!ProcOptimizer.ProcessedProcdures.Any(a => a == $"{spInfo.Schema}.{spInfo.Name}"))
        //            {
        //                ProcOptimizer.ProcessedProcdures.Add($"{spInfo.Schema}.{spInfo.Name}");
        //                ProcOptimizer.Process(spInfo);
        //            }
        //            ((BooleanComparisonExpression)node.UpdateSpecification.WhereClause.SearchCondition).FirstExpression = ReturnVisitorHandler(secondExpression);
        //        }
        //    }
        //    else if (node.UpdateSpecification.WhereClause != null && node.UpdateSpecification.WhereClause.SearchCondition is BooleanBinaryExpression booleanBinaryExpression)
        //    {
        //        node.UpdateSpecification.WhereClause.SearchCondition = SelectHandler(booleanBinaryExpression);
        //    }
        //    base.Visit(node);
        //}

        //public override void Visit(PrintStatement node)
        //{
        //    if (node.Expression is FunctionCall)
        //        node.Expression = ReturnVisitorHandler((FunctionCall)node.Expression);
        //    base.Visit(node);
        //}

        //public override void Visit(IfStatement node)
        //{
        //    if (node.Predicate is BooleanParenthesisExpression &&
        //               ((BooleanParenthesisExpression)node.Predicate).Expression is BooleanComparisonExpression &&
        //               ((BooleanComparisonExpression)((BooleanParenthesisExpression)node.Predicate).Expression).FirstExpression is FunctionCall functionCall)
        //    {
        //        SpInfo spInfo = new SpInfo
        //        {
        //            Schema = functionCall.CallTarget == null ? "dbo" : (((MultiPartIdentifierCallTarget)functionCall.CallTarget).MultiPartIdentifier.Identifiers).FirstOrDefault().Value,
        //            Name = functionCall.FunctionName.Value
        //        };

        //        //optimize the procedure
        //        if (!ProcOptimizer.ProcessedProcdures.Any(a => a == $"{spInfo.Schema}.{spInfo.Name}"))
        //        {
        //            ProcOptimizer.ProcessedProcdures.Add($"{spInfo.Schema}.{spInfo.Name}");
        //            ProcOptimizer.Process(spInfo);
        //        }

        //        ((BooleanComparisonExpression)((BooleanParenthesisExpression)node.Predicate).Expression).FirstExpression = ReturnVisitorHandler(functionCall);
        //    }
        //    base.Visit(node);
        //}

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
                    if (newBody.StatementList != null && newBody.StatementList.Statements.Any())
                    {
                        node.Statements[node.Statements.IndexOf(executeStatement)] = newBody;
                        newStatements.Add(newBody);
                    }
                }
            }

            foreach (SetVariableStatement setVariableStatement in node.Statements.Where(a => a is SetVariableStatement).ToList())
            {
                if (setVariableStatement.Expression is FunctionCall functionCall)
                {
                    var newBody = ExecuteFunctionStatement(functionCall, setVariableStatement.Variable.Name);
                    if (newBody.StatementList != null && newBody.StatementList.Statements.Any())
                    {
                        node.Statements[node.Statements.IndexOf(setVariableStatement)] = newBody;
                        newStatements.Add(newBody);
                    }
                }
            }

            foreach (DeclareVariableStatement declareVariableStatement in node.Statements.Where(a => a is DeclareVariableStatement).ToList())
            {
                foreach (var declaration in declareVariableStatement.Declarations)
                    if (declaration.Value is FunctionCall functionCall)
                    {
                        var newBody = ExecuteFunctionStatement((FunctionCall)declaration.Value, declaration.VariableName.Value);
                        if (newBody.StatementList != null && newBody.StatementList.Statements.Any())
                        {
                            declaration.Value = new NullLiteral() { Value = null };
                            node.Statements.Insert(node.Statements.IndexOf(declareVariableStatement) + 1, newBody);
                            newStatements.Add(newBody);
                        }
                    }
            }

            foreach (ReturnStatement returnStatement in node.Statements.Where(a => a is ReturnStatement).ToList())
            {
                if (returnStatement.Expression is FunctionCall functionCall)
                {
                    var newBody = ExecuteFunctionStatement((FunctionCall)returnStatement.Expression, isReturn: true);
                    if (newBody.StatementList != null && newBody.StatementList.Statements.Any())
                    {
                        returnStatement.Expression = null;
                        node.Statements[node.Statements.IndexOf(returnStatement)] = newBody;
                        newStatements.Add(newBody);
                    }
                }
            }

            //Handle Function/StoredProcedure call in IfStatement
            foreach (IfStatement ifStatement in node.Statements.Where(a => a is IfStatement).ToList())
            {
                if (ifStatement.ThenStatement is ExecuteStatement executeStatement)
                {
                    var executableProcedureReference = (ExecutableProcedureReference)executeStatement.ExecuteSpecification.ExecutableEntity;
                    if (executableProcedureReference.ProcedureReference.ProcedureReference.Name.DatabaseIdentifier == null)
                    {
                        string setVariableReferenceName = executeStatement.ExecuteSpecification.Variable is VariableReference ? executeStatement.ExecuteSpecification.Variable.Name : null;
                        var newBody = ExecuteStatement(executableProcedureReference, setVariableReferenceName);
                        ifStatement.ThenStatement = newBody;
                        if (newBody.StatementList != null && newBody.StatementList.Statements.Any())
                        {
                            newStatements.Add(newBody);
                        }
                    }
                }
                else if (ifStatement.ThenStatement is SetVariableStatement setVariableStatement)
                {
                    if (setVariableStatement.Expression is FunctionCall functionCall)
                    {
                        var newBody = ExecuteFunctionStatement((FunctionCall)setVariableStatement.Expression, setVariableStatement.Variable.Name);
                        ifStatement.ThenStatement = newBody;
                        if (newBody.StatementList != null && newBody.StatementList.Statements.Any())
                        {
                            newStatements.Add(newBody);
                        }
                    }
                }
                else if (ifStatement.ThenStatement is ReturnStatement returnStatement)
                {
                    if (returnStatement.Expression is FunctionCall functionCall)
                    {
                        var newBody = ExecuteFunctionStatement((FunctionCall)returnStatement.Expression, isReturn: true);
                        ifStatement.ThenStatement = newBody;
                        if (newBody.StatementList != null && newBody.StatementList.Statements.Any())
                        {
                            newStatements.Add(newBody);
                        }
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

        private BooleanBinaryExpression SelectHandler(BooleanBinaryExpression booleanComparisonExpression)
        {
            if (booleanComparisonExpression.FirstExpression is BooleanBinaryExpression)
                SelectHandler((BooleanBinaryExpression)booleanComparisonExpression.FirstExpression);
            else if (booleanComparisonExpression.FirstExpression is BooleanComparisonExpression)
            {
                if (((BooleanComparisonExpression)booleanComparisonExpression.FirstExpression).FirstExpression is FunctionCall firstExpression)
                    ((BooleanComparisonExpression)booleanComparisonExpression.FirstExpression).FirstExpression = ReturnVisitorHandler(firstExpression);
                else if (((BooleanComparisonExpression)booleanComparisonExpression.FirstExpression).SecondExpression is FunctionCall secondExpression)
                    ((BooleanComparisonExpression)booleanComparisonExpression.FirstExpression).SecondExpression = ReturnVisitorHandler(secondExpression);
            }

            if (booleanComparisonExpression.SecondExpression is BooleanBinaryExpression)
                SelectHandler((BooleanBinaryExpression)booleanComparisonExpression.SecondExpression);
            else if (booleanComparisonExpression.SecondExpression is BooleanComparisonExpression)
            {
                if (((BooleanComparisonExpression)booleanComparisonExpression.SecondExpression).FirstExpression is FunctionCall firstExpression)
                    ((BooleanComparisonExpression)booleanComparisonExpression.SecondExpression).FirstExpression = ReturnVisitorHandler(firstExpression);
                else if (((BooleanComparisonExpression)booleanComparisonExpression.SecondExpression).SecondExpression is FunctionCall secondExpression)
                    ((BooleanComparisonExpression)booleanComparisonExpression.SecondExpression).SecondExpression = ReturnVisitorHandler(secondExpression);
            }

            return booleanComparisonExpression;
        }

        private ScalarExpression ReturnVisitorHandler(FunctionCall functionCall)
        {
            SpInfo spInfo = new SpInfo
            {
                Schema = functionCall.CallTarget == null ? "dbo" : (((MultiPartIdentifierCallTarget)functionCall.CallTarget).MultiPartIdentifier.Identifiers).FirstOrDefault().Value,
                Name = functionCall.FunctionName.Value
            };
            ProcModel procModel = ProcOptimizer.GetProcModel(spInfo);
            if (((TSqlScript)procModel.TSqlFragment).Batches.FirstOrDefault().Statements.FirstOrDefault() is CreateFunctionStatement createFunctionStatement)
            {
                BeginEndBlockStatement beginEndBlock = (BeginEndBlockStatement)createFunctionStatement.StatementList.Statements.FirstOrDefault(a => a is BeginEndBlockStatement);
                if (beginEndBlock.StatementList.Statements.Count() == 1 && beginEndBlock.StatementList.Statements.FirstOrDefault() is ReturnStatement returnStatement)
                {
                    ReturnVisitor returnVisitor = new ReturnVisitor();

                    int unnamedValuesCounter = 0;
                    var unnamedValues = functionCall.Parameters;
                    foreach (var parameter in createFunctionStatement.Parameters)
                    {
                        returnVisitor.dictionary.Add(parameter, unnamedValues[unnamedValuesCounter++]);
                    }

                    beginEndBlock.Accept(returnVisitor);

                    return returnStatement.Expression;
                }
            }
            return functionCall;
        }

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