using System.Linq;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using TSQL_Inliner.ProcOptimization;

namespace TSQL_Inliner.Inliner
{
    public class StatementVisitor : TSqlConcreteFragmentVisitor
    {
        ProcOptimizer ProcOptimizer { get { return Program.ProcOptimizer; } }
        public BeginEndBlockStatement _returnStatementPlace { get; set; }
        int VariableCounter;

        public StatementVisitor(int variableCounter)
        {
            VariableCounter = variableCounter;
            _returnStatementPlace = new BeginEndBlockStatement();
        }

        //Rename "VariableReference"s
        public override void Visit(VariableReference node)
        {
            node.Name = Program.ProcOptimizer.BuildNewName(node.Name, VariableCounter);
            base.Visit(node);
        }

        public override void Visit(LabelStatement node)
        {
            node.Value = Program.ProcOptimizer.BuildNewName(node.Value.Remove(node.Value.Length - 1, 1), VariableCounter) + ":";
            base.Visit(node);
        }

        public override void Visit(GoToStatement node)
        {
            node.LabelName.Value = Program.ProcOptimizer.BuildNewName(node.LabelName.Value, VariableCounter);
            base.Visit(node);
        }

        //Rename "DeclareVariableElement"s
        public override void Visit(DeclareVariableElement node)
        {
            node.VariableName.Value = Program.ProcOptimizer.BuildNewName(node.VariableName.Value, VariableCounter);
            base.Visit(node);
        }

        public override void Visit(DeclareTableVariableStatement node)
        {
            node.Body.VariableName.Value = Program.ProcOptimizer.BuildNewName(node.Body.VariableName.Value, VariableCounter);
            base.Visit(node);
        }

        //Rename "VariableReference"s of "ExecuteParameter"
        public override void ExplicitVisit(ExecuteParameter node)
        {
            if (node.ParameterValue is VariableReference)
                ((VariableReference)node.ParameterValue).Name = Program.ProcOptimizer.BuildNewName(((VariableReference)node.ParameterValue).Name, VariableCounter);
        }

        public override void Visit(StatementList node)
        {
            //if we have a "ReturnStatement" inside stored procedures, we need to end up stored procedure code and resume the master file
            //for this purpos, create variables and set "GOTO" lable for jump to that lable
            foreach (ReturnStatement returnStatement in node.Statements.Where(a => a is ReturnStatement).ToList())
            {
                //Program.ProcOptimizer.hasReturnStatement = true;
                BeginEndBlockStatement returnBeginEndBlockStatement = new BeginEndBlockStatement()
                {
                    StatementList = new StatementList()
                };

                //if "ReturnStatement" has Expression, we process that ...
                //Declate variable and set value, add this variables to "returnStatementPlace" for set in top of stored procedire ...
                if (returnStatement.Expression != null)
                {
                    DeclareVariableStatement declareVariableStatement = new DeclareVariableStatement();

                    declareVariableStatement.Declarations.Add(new DeclareVariableElement()
                    {
                        DataType = ((ScalarFunctionReturnType)Program.ProcOptimizer.FunctionReturnType).DataType,
                        VariableName = new Identifier() { Value = Program.ProcOptimizer.BuildNewName("@ReturnValue", VariableCounter) }
                    });

                    if (_returnStatementPlace == null || _returnStatementPlace.StatementList == null)
                        _returnStatementPlace = new BeginEndBlockStatement()
                        {
                            StatementList = new StatementList()
                        };

                    if (!_returnStatementPlace.StatementList.Statements.Where(a => a is DeclareVariableStatement)
                        .Any(a => ((DeclareVariableStatement)a).Declarations.Any(b => b.VariableName.Value == declareVariableStatement.Declarations.FirstOrDefault().VariableName.Value)))
                        _returnStatementPlace.StatementList.Statements.Add(declareVariableStatement);

                    SetVariableStatement setVariableStatement = new SetVariableStatement()
                    {
                        AssignmentKind = AssignmentKind.Equals,
                        Variable = new VariableReference()
                        {
                            Name = $"@ReturnValue"
                        },
                        Expression = returnStatement.Expression
                    };

                    returnBeginEndBlockStatement.StatementList.Statements.Add(setVariableStatement);
                }

                //set GoToStatement on the end
                returnBeginEndBlockStatement.StatementList.Statements.Add(new GoToStatement()
                {
                    LabelName = new Identifier()
                    {
                        Value = Program.ProcOptimizer.GoToName,
                        QuoteType = QuoteType.NotQuoted
                    }
                });
                //Replace "ReturnStatement" by new value ...
                node.Statements[node.Statements.IndexOf(returnStatement)] = returnBeginEndBlockStatement;
            }
            base.Visit(node);
        }

        //for process "IfStatement", we need to append "ThenStatement" in "BeginEndBlockStatement"
        public override void Visit(IfStatement node)
        {
            if (!(node.ThenStatement is BeginEndBlockStatement))
            {
                BeginEndBlockStatement beginEndBlockStatement = new BeginEndBlockStatement()
                {
                    StatementList = new StatementList()
                };
                beginEndBlockStatement.StatementList.Statements.Add(node.ThenStatement);
                node.ThenStatement = beginEndBlockStatement;
            }
            base.Visit(node);
        }
    }
}