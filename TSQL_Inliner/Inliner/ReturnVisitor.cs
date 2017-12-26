using Microsoft.SqlServer.TransactSql.ScriptDom;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TSQL_Inliner.Model;
using TSQL_Inliner.ProcOptimization;
using TSQL_Inliner.Tree;
using System.Reflection;
using static TSQL_Inliner.ProcOptimization.ProcOptimizer;

namespace TSQL_Inliner.Inliner
{
    public class ReturnVisitor : TSqlFragmentVisitor
    {
        public ProcOptimizer ProcOptimizer { get { return Program.ProcOptimizer; } }

        List<TSqlFragment> newStatements = new List<TSqlFragment>();

        //int VariableCounter = 0;
        TreeModel TreeModel;
        //1
        internal void Process(TSqlFragment sqlFragment)
        {
            TreeModel = FragmentTreeBuilder.CreateTreeFromFragment(sqlFragment);
            sqlFragment.Accept(this);
        }

        #region Visitors

        //Rename "VariableReference"s

        public override void Visit(DeclareVariableElement node)
        {
            node.VariableName.Value = Program.ProcOptimizer.BuildNewName(node.VariableName.Value, ProcOptimizer.VariableCounter);
            base.Visit(node);
        }

        public override void Visit(VariableReference node)
        {
            node.Name = Program.ProcOptimizer.BuildNewName(node.Name, ProcOptimizer.VariableCounter);
            base.Visit(node);
        }
        //2
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
        //3
        /// <summary>
        /// override 'Visit' method for process 'ExecuteStatement' in 'StatementLists'
        /// </summary>
        /// <param name="node"></param>
        public override void Visit(StatementList node)
        {
            //List<DeclareVariableStatement> outDeclareVariableStatement;
            foreach (SetVariableStatement setVariableStatement in node.Statements.Where(a => a is SetVariableStatement).ToList())
            {
                if (setVariableStatement.Expression is FunctionCall functionCall && functionCall.CallTarget != null && functionCall.CallTarget is MultiPartIdentifierCallTarget)
                {
                    setVariableStatement.Expression = ReturnVisitorHandler(functionCall/*, out outDeclareVariableStatement*/);
                    //foreach (var i in outDeclareVariableStatement)
                    //    node.Statements.Insert(node.Statements.IndexOf(setVariableStatement), i);
                }
            }

            foreach (DeclareVariableStatement declareVariableStatement in node.Statements.Where(a => a is DeclareVariableStatement).ToList())
            {
                foreach (var declaration in declareVariableStatement.Declarations)
                    if (declaration.Value is FunctionCall functionCall && functionCall.CallTarget != null && functionCall.CallTarget is MultiPartIdentifierCallTarget)
                    {
                        declaration.Value = ReturnVisitorHandler((FunctionCall)declaration.Value/*, out outDeclareVariableStatement*/);
                        //foreach (var i in outDeclareVariableStatement)
                        //    node.Statements.Insert(node.Statements.IndexOf(declareVariableStatement), i);
                    }
            }

            foreach (ReturnStatement returnStatement in node.Statements.Where(a => a is ReturnStatement).ToList())
            {
                if (returnStatement.Expression is FunctionCall functionCall && functionCall.CallTarget != null && functionCall.CallTarget is MultiPartIdentifierCallTarget)
                {
                    returnStatement.Expression = ReturnVisitorHandler((FunctionCall)returnStatement.Expression/*, out outDeclareVariableStatement*/);
                    //foreach (var i in outDeclareVariableStatement)
                    //    node.Statements.Insert(node.Statements.IndexOf(returnStatement), i);
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
                    booleanComparisonExpression.FirstExpression = ReturnVisitorHandler(functionCall/*, out outDeclareVariableStatement*/);
                    //foreach (var i in outDeclareVariableStatement)
                    //    node.Statements.Insert(node.Statements.IndexOf(ifStatement), i);
                }
            }

            base.Visit(node);
        }

        //public override void Visit(SelectStatement node)
        //{
        //    if (node.QueryExpression is QuerySpecification querySpecification && querySpecification.WhereClause != null)
        //    {
        //        if (querySpecification.WhereClause.SearchCondition is BooleanComparisonExpression)
        //        {
        //            if (((BooleanComparisonExpression)querySpecification.WhereClause.SearchCondition).FirstExpression is FunctionCall firstExpression && firstExpression.CallTarget != null)
        //            {
        //                SpInfo spInfo = new SpInfo
        //                {
        //                    Schema = (((MultiPartIdentifierCallTarget)firstExpression.CallTarget).MultiPartIdentifier.Identifiers).FirstOrDefault().Value,
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
        //            if (((BooleanComparisonExpression)querySpecification.WhereClause.SearchCondition).SecondExpression is FunctionCall secondExpression && secondExpression.CallTarget != null)
        //            {
        //                SpInfo spInfo = new SpInfo
        //                {
        //                    Schema = (((MultiPartIdentifierCallTarget)secondExpression.CallTarget).MultiPartIdentifier.Identifiers).FirstOrDefault().Value,
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
        //            if (selectSetVariable.Expression is FunctionCall functionCall && functionCall.CallTarget != null)
        //            {
        //                SpInfo spInfo = new SpInfo
        //                {
        //                    Schema = (((MultiPartIdentifierCallTarget)functionCall.CallTarget).MultiPartIdentifier.Identifiers).FirstOrDefault().Value,
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
        //        if (((BooleanComparisonExpression)node.UpdateSpecification.WhereClause.SearchCondition).FirstExpression is FunctionCall firstExpression && firstExpression.CallTarget != null)
        //        {
        //            SpInfo spInfo = new SpInfo
        //            {
        //                Schema = (((MultiPartIdentifierCallTarget)firstExpression.CallTarget).MultiPartIdentifier.Identifiers).FirstOrDefault().Value,
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
        //        if (((BooleanComparisonExpression)node.UpdateSpecification.WhereClause.SearchCondition).SecondExpression is FunctionCall secondExpression && secondExpression.CallTarget != null)
        //        {
        //            SpInfo spInfo = new SpInfo
        //            {
        //                Schema = (((MultiPartIdentifierCallTarget)secondExpression.CallTarget).MultiPartIdentifier.Identifiers).FirstOrDefault().Value,
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

        public override void Visit(PrintStatement node)
        {
            if (node.Expression is FunctionCall)
                node.Expression = ReturnVisitorHandler((FunctionCall)node.Expression/*, out List<DeclareVariableStatement> outDeclareVariableStatement*/);
            base.Visit(node);
        }

        public override void ExplicitVisit(BinaryExpression node)
        {
            //if (node.FirstExpression is VariableReference)
            //{
            //    var parameter = ProcOptimizer.ReturnVisitorDictionary.FirstOrDefault(a => a.Key.ProcedureParameter.VariableName.Value ==
            //    Program.ProcOptimizer.BuildNewName(((VariableReference)node.FirstExpression).Name, ProcOptimizer.VariableCounter) &&
            //    a.Key.VariableCounter == ProcOptimizer.VariableCounter);
            //    if (parameter.Value != null)
            //    {
            //        node.FirstExpression = parameter.Value;
            //        ProcOptimizer.ReturnVisitorDictionary.Remove(parameter.Key);
            //    }
            //}
            //if (node.SecondExpression is VariableReference)
            //{
            //    var parameter = ProcOptimizer.ReturnVisitorDictionary.FirstOrDefault(a => a.Key.ProcedureParameter.VariableName.Value ==
            //    Program.ProcOptimizer.BuildNewName(((VariableReference)node.SecondExpression).Name, ProcOptimizer.VariableCounter) &&
            //     a.Key.VariableCounter == ProcOptimizer.VariableCounter);

            //    if (parameter.Value != null)
            //    {
            //        node.SecondExpression = parameter.Value;
            //        ProcOptimizer.ReturnVisitorDictionary.Remove(parameter.Key);
            //    }
            //}
            base.ExplicitVisit(node);
        }
        //4
        public override void ExplicitVisit(BeginEndBlockStatement node)
        {
            //call the base if it the statement has not been generated by this object
            if (!newStatements.Contains(node))
                base.ExplicitVisit(node);
        }

        public override void Visit(ScalarExpression node)
        {
            if (node is VariableReference variableReference)
            {
                //var parameter = ProcOptimizer.ReturnVisitorDictionary.FirstOrDefault(a => a.Key.ProcedureParameter.VariableName.Value == variableReference.Name &&
                //a.Key.VariableCounter == ProcOptimizer.VariableCounter);
                //if (parameter.Value != null)
                //{
                //    node = parameter.Value;
                //    ProcOptimizer.ReturnVisitorDictionary.Remove(parameter.Key);
                //}
            }
            base.Visit(node);
        }

        #endregion



        #region Methods

        private BooleanBinaryExpression SelectHandler(BooleanBinaryExpression booleanComparisonExpression, out List<DeclareVariableStatement> declareVariableStatement)
        {
            declareVariableStatement = new List<DeclareVariableStatement>();
            if (booleanComparisonExpression.FirstExpression is BooleanBinaryExpression)
                SelectHandler((BooleanBinaryExpression)booleanComparisonExpression.FirstExpression, out declareVariableStatement);
            else if (booleanComparisonExpression.FirstExpression is BooleanComparisonExpression)
            {
                if (((BooleanComparisonExpression)booleanComparisonExpression.FirstExpression).FirstExpression is FunctionCall firstExpression)
                    ((BooleanComparisonExpression)booleanComparisonExpression.FirstExpression).FirstExpression = ReturnVisitorHandler(firstExpression/*, out declareVariableStatement*/);
                else if (((BooleanComparisonExpression)booleanComparisonExpression.FirstExpression).SecondExpression is FunctionCall secondExpression)
                    ((BooleanComparisonExpression)booleanComparisonExpression.FirstExpression).SecondExpression = ReturnVisitorHandler(secondExpression/*, out declareVariableStatement*/);
            }

            if (booleanComparisonExpression.SecondExpression is BooleanBinaryExpression)
                SelectHandler((BooleanBinaryExpression)booleanComparisonExpression.SecondExpression, out declareVariableStatement);
            else if (booleanComparisonExpression.SecondExpression is BooleanComparisonExpression)
            {
                if (((BooleanComparisonExpression)booleanComparisonExpression.SecondExpression).FirstExpression is FunctionCall firstExpression)
                    ((BooleanComparisonExpression)booleanComparisonExpression.SecondExpression).FirstExpression = ReturnVisitorHandler(firstExpression/*, out declareVariableStatement*/);
                else if (((BooleanComparisonExpression)booleanComparisonExpression.SecondExpression).SecondExpression is FunctionCall secondExpression)
                    ((BooleanComparisonExpression)booleanComparisonExpression.SecondExpression).SecondExpression = ReturnVisitorHandler(secondExpression/*, out declareVariableStatement*/);
            }

            return booleanComparisonExpression;
        }

        private ScalarExpression ReturnVisitorHandler(FunctionCall functionCall/*, out List<DeclareVariableStatement> declareVariableStatement*/)
        {
            //VariableCounter++;

            //declareVariableStatement = new List<DeclareVariableStatement>();
            foreach (var param in functionCall.Parameters.ToList())
            {
                if (param is FunctionCall paramFunctionCall)
                {
                    functionCall.Parameters.Insert(functionCall.Parameters.IndexOf(param), ReturnVisitorHandler(paramFunctionCall/*, out declareVariableStatement*/));
                    functionCall.Parameters.Remove(param);
                    ReturnVisitor returnVisitor = new ReturnVisitor();
                    paramFunctionCall.Accept(returnVisitor);
                }
            }

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

            ProcModel procModel = ProcOptimizer.GetProcModel(spInfo);
            if (((TSqlScript)procModel.TSqlFragment).Batches.FirstOrDefault().Statements.FirstOrDefault() is CreateFunctionStatement createFunctionStatement)
            {
                BeginEndBlockStatement beginEndBlock = (BeginEndBlockStatement)createFunctionStatement.StatementList.Statements.FirstOrDefault(a => a is BeginEndBlockStatement);
                if (beginEndBlock.StatementList.Statements.Count() == 1 && beginEndBlock.StatementList.Statements.FirstOrDefault() is ReturnStatement returnStatement)
                {
                    for (int i = 0; i < createFunctionStatement.Parameters.Count; i++)
                        ProcOptimizer.ReturnVisitorDictionary.Add(new ReturnVisitorKeyModel
                        {
                            ProcedureParameter = createFunctionStatement.Parameters[i],
                            FunctionCall = functionCall
                        }, functionCall.Parameters[i]);

                    //RenameVariableReference(procModel.TSqlFragment);
                    ReturnVisitor returnVisitor = new ReturnVisitor();

                    //int unnamedValuesCounter = 0;
                    //DeclareVariableStatement declareVariables = new DeclareVariableStatement();
                    //foreach (var parameter in createFunctionStatement.Parameters)
                    //{
                    //    DeclareVariableElement declareVariableElement = new DeclareVariableElement()
                    //    {
                    //        DataType = parameter.DataType,
                    //        Value = functionCall.Parameters[unnamedValuesCounter++],
                    //        Nullable = parameter.Nullable,
                    //        VariableName = parameter.VariableName
                    //    };
                    //    declareVariableElement.VariableName.Value = Program.ProcOptimizer.BuildNewName(declareVariableElement.VariableName.Value, ProcOptimizer.VariableCounter);
                    //    declareVariables.Declarations.Add(declareVariableElement);
                    //}

                    //declareVariableStatement.Add(declareVariables);
                    //beginEndBlock.Accept(returnVisitor);

                    return new ParenthesisExpression()
                    {
                        Expression = returnStatement.Expression
                    };
                }
            }
            return functionCall;
        }

        //public void RenameVariableReference(TSqlFragment script)
        //{
        //    var enumerator = new EnumeratorVisitor();
        //    script.Accept(enumerator);

        //    SetVariableReference(TreeModel.Children);
        //}

        public void SetVariableReference(List<TreeModel> treeModelList)
        {
            foreach (var i in treeModelList)
            {
                if (i.DomObject is VariableReference variableReference)
                {
                    PropertyInfo prop = variableReference.GetType().GetProperty(i.ParentObjectPropertyName, BindingFlags.Public | BindingFlags.Instance);
                    if (prop != null && prop.CanWrite)
                    {
                        var newValue = new ParenthesisExpression()
                        {
                            Expression = variableReference
                        };
                        prop.SetValue(variableReference, newValue, null);
                    }
                }
                if (i.Children != null && i.Children.Any())
                {
                    SetVariableReference(i.Children);
                }
            }
        }

        #endregion
    }
}