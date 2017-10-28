﻿using Microsoft.SqlServer.TransactSql.ScriptDom;
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
        /// <summary>
        /// Counter for make variables unique
        /// </summary>
        private static int variableCount = 0;
        class MyVisistor : TSqlConcreteFragmentVisitor
        {
            /// <summary>
            /// override 'Visit' method for process 'StatementLists'
            /// </summary>
            /// <param name="node"></param>
            public override void Visit(StatementList node)
            {
                foreach (var i in node.Statements.Where(a => a is ExecuteStatement).ToList())
                {
                    var executeStatement = (((ExecuteStatement)i).ExecuteSpecification.ExecutableEntity);
                    var schemaIdentifier = ((ExecutableProcedureReference)executeStatement).ProcedureReference.ProcedureReference.Name.SchemaIdentifier.Value;
                    var baseIdentifier = ((ExecutableProcedureReference)executeStatement).ProcedureReference.ProcedureReference.Name.BaseIdentifier.Value;
                    var param = ((ExecutableProcedureReference)executeStatement).Parameters.ToDictionary(a => a.Variable.Name, a => a.ParameterValue);

                    node.Statements[node.Statements.IndexOf(i)] = GetTSqlStatement($"[{schemaIdentifier}].[{baseIdentifier}]", param);
                }

                base.Visit(node);
            }
        }

        /// <summary>
        /// load inline stored procedure and process
        /// </summary>
        /// <param name="SPIdentifier">stored procedure identifier</param>
        /// <param name="Param">Variable Reference</param>
        /// <returns></returns>
        protected static TSqlStatement GetTSqlStatement(string SPIdentifier, Dictionary<string, ScalarExpression> ProcedureParametersValues)
        {
            //TSqlFragment tSqlFragment = ReadTsql($@"C:\Users\Mohsen Hasani\Desktop\{SPIdentifier}.sql");
            TSqlFragment tSqlFragment = ReadTsql($@"C:\Users\Mohsen Hasani\Desktop\dbo.Branch_PropsGet3.sql");
            Sql140ScriptGenerator sql140ScriptGenerator = new Sql140ScriptGenerator();

            MyVisistor myVisistor = new MyVisistor();

            var batche = ((TSqlScript)tSqlFragment).Batches.FirstOrDefault(a => a.Statements.Any(b => b is AlterProcedureStatement));
            AlterProcedureStatement alterProcedureStatement = (AlterProcedureStatement)batche.Statements.FirstOrDefault(a => a is AlterProcedureStatement);

            //get all stored procedure parameters for declare inline
            var alterProcedureStatementParameters = alterProcedureStatement.Parameters.Select(a => a).ToList();

            BeginEndBlockStatement beginEndBlockStatement = new BeginEndBlockStatement
            {
                StatementList = new StatementList()
            };

            ParameterProcessing(beginEndBlockStatement, alterProcedureStatementParameters, ProcedureParametersValues);

            return beginEndBlockStatement;
        }

        protected static void ParameterProcessing(BeginEndBlockStatement beginEndBlockStatement, List<ProcedureParameter> ProcedureParameters, Dictionary<string, ScalarExpression> ProcedureParametersValues)
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
             
                if (ProcedureParametersValues.Any(a => a.Key == declareVariableElement.VariableName.Value))
                {
                    declareVariableElement.Value = ProcedureParametersValues.FirstOrDefault(a => a.Key == declareVariableElement.VariableName.Value).Value;
                }

                declareVariableElement.VariableName.Value = $"{parameter.VariableName.Value}_{variableCount}";
                declareVariableStatement.Declarations.Add(declareVariableElement);
            }

            beginEndBlockStatement.StatementList.Statements.Add(declareVariableStatement);
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