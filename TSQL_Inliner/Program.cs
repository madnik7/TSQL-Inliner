using Microsoft.SqlServer.TransactSql.ScriptDom;
using Newtonsoft.Json;
using System;
using TSQL_Inliner.Method;
using TSQL_Inliner.Model;

namespace TSQL_Inliner
{
    class Program
    {
        static void Main(string[] args)
        {
            Sql140ScriptGenerator sql140ScriptGenerator = new Sql140ScriptGenerator();
            TSQLConnection tSQLConnection = new TSQLConnection();

            var allSPs = tSQLConnection.GetAllStoredProcedures("dbo");
            foreach (var SPName in allSPs)
            {
                TSqlFragment sqlFragment = tSQLConnection.ReadTsql(out CommentModel commentModel, out string topComments, "dbo", SPName);

                if (!commentModel.IsOptimized && commentModel.IsOptimizable)
                {
                    commentModel.IsOptimized = true;

                    sql140ScriptGenerator.GenerateScript(sqlFragment, out string str);
                    str = $"{topComments}-- #Inliner {JsonConvert.SerializeObject(commentModel)}{Environment.NewLine}{str}";

                    Console.WriteLine(str + Environment.NewLine);
                    Console.WriteLine("=-=-=-=-=-=-=-=-=-=-=-=" + Environment.NewLine + "Execute this script ? [y/n] ");

                    if (Console.ReadKey().KeyChar.ToString().ToLower() == "y")
                    {
                        tSQLConnection.WriteTsql(str);
                    }
                    Console.WriteLine(Environment.NewLine + "=-=-=-=-=-=-=-=-=-=-=-=" + Environment.NewLine);
                }
                else
                {
                    Console.WriteLine("this code has been already optimized, press any key for exit." + Environment.NewLine);
                    Console.ReadKey();
                }
            }
        }
    }
}