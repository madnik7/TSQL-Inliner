using Microsoft.SqlServer.TransactSql.ScriptDom;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TSQL_Inliner
{
    class Program
    {
        class MyVisistor : TSqlFragmentVisitor
        {
            public override void Visit(ExecuteStatement node)
            {
                //node.ExecuteSpecification
                ((ExecutableProcedureReference)node.ExecuteSpecification.ExecutableEntity).ProcedureReference.ProcedureReference.Name.BaseIdentifier.Value = "zzzzz";
                base.Visit(node);
            }
        }

        static void Main(string[] args)
        {
            var parser = new TSql140Parser(true);
            var fragment = parser.Parse(new StreamReader(@"C:\Users\madnik7\Desktop\api.Bank_AllGet.sql"), out IList<ParseError> errors);
            //ProcessScript(fragment as TSqlScript);

            var myVisitor = new MyVisistor();
            fragment.Accept(myVisitor);


            var sgr = new Sql140ScriptGenerator();
            string str;
            sgr.GenerateScript(fragment, out str);
            Console.WriteLine(str);

        }

        static void ProcessScript(TSqlScript sql)
        {
            foreach (var item in sql.Batches)
                ProceesStatements(item.Statements);
        }

        static void ProceesStatements(IList<TSqlStatement> statements)
        {
            foreach (var item in statements)
            {
                //Console.WriteLine("{0} -- {1}", item, item.GetType());
                ProceesFragment(item);
            }
        }

        static void ProceesExecuteStatement(ExecuteStatement executeStatement)
        {
            //Console.WriteLine(executeStatement.ExecuteSpecification);
        }


        static void ProceesFragment(TSqlFragment fragment)
        {
            if (fragment is TSqlBatch)
            {
                ProceesStatements((fragment as TSqlBatch).Statements);
                return;
            };

            if (fragment is CreateProcedureStatement)
            {
                ProceesStatements((fragment as CreateProcedureStatement).StatementList.Statements);
                return;
            };

            if (fragment is BeginEndBlockStatement)
            {
                ProceesStatements((fragment as BeginEndBlockStatement).StatementList.Statements);
                return;
            };

            if (fragment is ExecuteStatement)
            {
                ProceesExecuteStatement((ExecuteStatement)fragment);
                return;
            }

            foreach (var token in fragment.ScriptTokenStream)
            {
                ProcessToken(token);
            }

        }

        static void ProcessToken(TSqlParserToken token)
        {
            //Console.WriteLine("{0}", token.ToString());
        }
    }
}
