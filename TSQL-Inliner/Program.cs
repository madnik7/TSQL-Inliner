using Microsoft.SqlServer.TransactSql.ScriptDom;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace TSQL_Inliner
{
    class Program
    {
        private static int variableCount = 0;
        class MyVisistor : TSqlConcreteFragmentVisitor
        {
            public override void Visit(StatementList node)
            {
                foreach (var i in node.Statements.Where(a => a is ExecuteStatement).ToList())
                {
                    var executeStatement = (((ExecuteStatement)i).ExecuteSpecification.ExecutableEntity);
                    var schemaIdentifier = ((ExecutableProcedureReference)executeStatement).ProcedureReference.ProcedureReference.Name.SchemaIdentifier.Value;
                    var baseIdentifier = ((ExecutableProcedureReference)executeStatement).ProcedureReference.ProcedureReference.Name.BaseIdentifier.Value;
                    var param = ((ExecutableProcedureReference)executeStatement).Parameters.ToDictionary(a => a.Variable == null ? ((VariableReference)a.ParameterValue).Name : a.Variable.Name, a => ((VariableReference)a.ParameterValue).Name);

                    node.Statements[node.Statements.IndexOf(i)] = GetTSqlStatement($"[{schemaIdentifier}].[{baseIdentifier}]", param);
                }

                base.Visit(node);
            }
        }

        protected static TSqlStatement GetTSqlStatement(string SPIdentifier, Dictionary<string, string> Param)
        {
            //TSqlFragment tSqlFragment = ReadTsql($@"C:\Users\Mohsen Hasani\Desktop\{SPIdentifier}.sql");
            TSqlFragment tSqlFragment = ReadTsql($@"C:\Users\Mohsen Hasani\Desktop\dbo.Branch_PropsGet3.sql");
            Sql140ScriptGenerator sql140ScriptGenerator = new Sql140ScriptGenerator();

            MyVisistor myVisistor = new MyVisistor();

            var batche = ((TSqlScript)tSqlFragment).Batches.FirstOrDefault(a => a.Statements.Any(b => b is AlterProcedureStatement));
            AlterProcedureStatement alterProcedureStatement = (AlterProcedureStatement)batche.Statements.FirstOrDefault(a => a is AlterProcedureStatement);

            var alterProcedureStatementParameters = alterProcedureStatement.Parameters.Select(a => a).ToList();

            return ParameterProcessing(alterProcedureStatementParameters);
        }

        protected static BeginEndBlockStatement ParameterProcessing(List<ProcedureParameter> ProcedureParameters)
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
                declareVariableElement.VariableName.Value = $"{parameter.VariableName}_{variableCount}";
                declareVariableStatement.Declarations.Add(declareVariableElement);
            }

            BeginEndBlockStatement beginEndBlockStatement = new BeginEndBlockStatement
            {
                StatementList = new StatementList()
            };
            beginEndBlockStatement.StatementList.Statements.Add(declareVariableStatement);

            return beginEndBlockStatement;
        }

        protected static TSqlFragment ReadTsql(string LocalAddress)
        {
            var parser = new TSql140Parser(true);
            var fragment = parser.Parse(new StreamReader(LocalAddress), out IList<ParseError> errors);

            MyVisistor myVisitor = new MyVisistor();
            fragment.Accept(myVisitor);

            return fragment;
        }

        static void Main(string[] args)
        {
            Sql140ScriptGenerator sql140ScriptGenerator = new Sql140ScriptGenerator();
            sql140ScriptGenerator.GenerateScript(ReadTsql(@"C:\Users\Mohsen Hasani\Desktop\api.Branch_PropsGet1.sql"), out string str);
            Console.WriteLine(str);
            Console.ReadKey();
        }
    }
}