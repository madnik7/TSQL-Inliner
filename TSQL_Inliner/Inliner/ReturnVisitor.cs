using Microsoft.SqlServer.TransactSql.ScriptDom;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TSQL_Inliner.Inliner
{
    public class ReturnVisitor : TSqlFragmentVisitor
    {
        public Dictionary<ProcedureParameter, ScalarExpression> dictionary = new Dictionary<ProcedureParameter, ScalarExpression>();
        public override void ExplicitVisit(BinaryExpression node)
        {
            if (node.FirstExpression is VariableReference)
            {
                   var parameter = dictionary.FirstOrDefault(a => a.Key.VariableName.Value == ((VariableReference)node.FirstExpression).Name);
                if (parameter.Value is VariableReference)
                {
                    node.FirstExpression = (VariableReference)parameter.Value;
                }
                else if(parameter.Value is Literal)
                {
                    node.FirstExpression = (Literal)parameter.Value;
                }
            }
            if (node.SecondExpression is VariableReference)
            {
                var parameter = dictionary.FirstOrDefault(a => a.Key.VariableName.Value == ((VariableReference)node.SecondExpression).Name);
                if (parameter.Value is VariableReference)
                {
                    node.SecondExpression = (VariableReference)parameter.Value;
                }
                else if (parameter.Value is Literal)
                {
                    node.SecondExpression = (Literal)parameter.Value;
                }
            }
            base.ExplicitVisit(node);
        }
    }
}
