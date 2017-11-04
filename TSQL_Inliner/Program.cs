using Microsoft.SqlServer.TransactSql.ScriptDom;
using Newtonsoft.Json;
using System;
using System.Linq;
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

            Console.WriteLine("Getting dbo proccedures list");
            var allSPs = tSQLConnection.GetAllStoredProcedures("dbo");
            foreach (var SPName in allSPs)
            {
                Console.Write($"Processing dbo.{SPName}, ");
                try
                {
                    TSqlFragment sqlFragment = tSQLConnection.ReadTsql(out CommentModel commentModel, out string topComments, "dbo", SPName);

                    if (commentModel.IsOptimized || !commentModel.IsOptimizable)
                    {
                        Console.WriteLine(commentModel.IsOptimized ? "Already optimized." : "Non optimizable.");
                        continue;
                    }

                    commentModel.IsOptimized = true;
                    sql140ScriptGenerator.GenerateScript(sqlFragment, out string script);
                    script = $"{topComments}-- #Inliner {JsonConvert.SerializeObject(commentModel)}{Environment.NewLine}{script}";

                    if (commentModel.InlineMode.ToLower() != "none")
                        tSQLConnection.WriteTsql(script);

                    Console.WriteLine("OK.");
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Error! {ex.Message}");
                    Console.ResetColor();
                }
            }
            Console.WriteLine($"{Environment.NewLine}=-=-=-=-=-=-=-=-=-=-={Environment.NewLine}Press any key to exit ...");
            Console.ReadKey();
        }
    }
}