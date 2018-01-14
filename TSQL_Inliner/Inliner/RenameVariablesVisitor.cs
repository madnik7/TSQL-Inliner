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
    public class RenameVariablesVisitor : TSqlFragmentVisitor
    {
        public Dictionary<ProcedureParameter, ScalarExpression> ReturnVisitorDictionary = new Dictionary<ProcedureParameter, ScalarExpression>();

        public override void ExplicitVisit(BinaryExpression node)
        {
            if (node.FirstExpression is VariableReference VariableReference)
            {
                var parameter = ReturnVisitorDictionary.FirstOrDefault(a => a.Key.VariableName.Value ==
                VariableReference.Name);

                if (parameter.Value != null)
                {
                    node.FirstExpression = parameter.Value;
                }
            }
            if (node.SecondExpression is VariableReference)
            {
                var parameter = ReturnVisitorDictionary.FirstOrDefault(a => a.Key.VariableName.Value ==
                ((VariableReference)node.SecondExpression).Name);

                if (parameter.Value != null)
                {
                    node.SecondExpression = parameter.Value;
                }
            }
            base.ExplicitVisit(node);
        }

        public override void Visit(ScalarExpression node)
        {
            if (node is VariableReference variableReference)
            {
                var parameter = ReturnVisitorDictionary.FirstOrDefault(a => a.Key.VariableName.Value == variableReference.Name);
                if (parameter.Value != null)
                {
                    node = parameter.Value;
                }
            }
            else
            if (node is CastCall castCall && castCall.Parameter is VariableReference ParameterVariableReference)
            {
                var parameter = ReturnVisitorDictionary.FirstOrDefault(a => a.Key.VariableName.Value == ParameterVariableReference.Name);
                if (parameter.Value != null)
                {
                    castCall.Parameter = parameter.Value;
                }
            }
            else
            if (node is FunctionCall functionCall)
            {
                foreach (VariableReference VariableReference in functionCall.Parameters.Where(a => a is VariableReference).ToList())
                {
                    var parameter = ReturnVisitorDictionary.FirstOrDefault(a => a.Key.VariableName.Value == VariableReference.Name);
                    if (parameter.Value != null)
                    {
                        functionCall.Parameters[functionCall.Parameters.IndexOf(VariableReference)] = parameter.Value;
                    }
                }
            }
            else
            {

            }
            base.Visit(node);
        }
    }
}
