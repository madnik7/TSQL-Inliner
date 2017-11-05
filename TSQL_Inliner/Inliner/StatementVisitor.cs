using System.Linq;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace TSQL_Inliner.Inliner
{
    public class StatementVisitor : TSqlConcreteFragmentVisitor
    {
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

        //Rename "DeclareVariableElement"s
        public override void Visit(DeclareVariableElement node)
        {
            node.VariableName.Value = Program.ProcOptimizer.BuildNewName(node.VariableName.Value, VariableCounter);
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
            foreach (var returnStatement in node.Statements.Where(a => a is ReturnStatement).ToList())
            {
                Program.ProcOptimizer.hasReturnStatement = true;
                BeginEndBlockStatement returnBeginEndBlockStatement = new BeginEndBlockStatement()
                {
                    StatementList = new StatementList()
                };

                //if "ReturnStatement" has Expression, we process that ...
                //Declate variable and set value, add this variables to "returnStatementPlace" for set in top of stored procedire ...
                if (((ReturnStatement)returnStatement).Expression != null)
                {
                    DeclareVariableStatement declareVariableStatement = new DeclareVariableStatement();

                    declareVariableStatement.Declarations.Add(new DeclareVariableElement()
                    {
                        DataType = new SqlDataTypeReference()
                        {
                            SqlDataTypeOption = SqlDataTypeOption.Int
                        },
                        VariableName = new Identifier() { Value = Program.ProcOptimizer.BuildNewName("@ReturnValue", VariableCounter) }
                    });

                    if (_returnStatementPlace == null || _returnStatementPlace.StatementList == null)
                        _returnStatementPlace = new BeginEndBlockStatement()
                        {
                            StatementList = new StatementList()
                        };
                    _returnStatementPlace.StatementList.Statements.Add(declareVariableStatement);

                    returnBeginEndBlockStatement.StatementList.Statements.Add(new SetVariableStatement()
                    {
                        AssignmentKind = AssignmentKind.Equals,
                        Variable = new VariableReference()
                        {
                            Name = $"@ReturnValue",
                        },
                        Expression = new IntegerLiteral()
                        {
                            Value = ((ReturnStatement)returnStatement).Expression is VariableReference ?
                            ((VariableReference)((ReturnStatement)returnStatement).Expression).Name :
                            ((IntegerLiteral)((ReturnStatement)returnStatement).Expression).Value
                        }
                    });
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