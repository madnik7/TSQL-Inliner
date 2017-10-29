using Microsoft.SqlServer.TransactSql.ScriptDom;
using System;
using TSQL_Inliner.Method;

namespace TSQL_Inliner
{
    class Program
    {
        static void Main(string[] args)
        {
            Sql140ScriptGenerator sql140ScriptGenerator = new Sql140ScriptGenerator();
            TSQLReader tSQLReader = new TSQLReader();
            sql140ScriptGenerator.GenerateScript(tSQLReader.ReadTsql(@"C:\Users\Mohsen Hasani\Desktop\api.Branch_PropsGet1.sql"), out string str);
            Console.WriteLine(str);
            Console.ReadKey();
        }
    }
}