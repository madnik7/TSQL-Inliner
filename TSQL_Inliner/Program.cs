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

            Console.WriteLine("Getting dbo proccedures list");
            var allSPs = tSQLConnection.GetAllStoredProcedures("dbo");
            foreach (var SPName in allSPs)
            {
                Console.Write($"Processing dbo.{SPName}, ");
                string str = string.Empty;
                try
                {
                    TSqlFragment sqlFragment = tSQLConnection.ReadTsql(out CommentModel commentModel, out string topComments, "dbo", SPName);

                    if (commentModel.IsOptimized || !commentModel.IsOptimizable)
                    {
                        Console.WriteLine(commentModel.IsOptimized ? "AlreadyOptimised." : "Non Optimizable.");
                        continue;
                    }

                    commentModel.IsOptimized = true;
                    str = string.Empty;
                    sql140ScriptGenerator.GenerateScript(sqlFragment, out str);
                    str = $"{topComments}-- #Inliner {JsonConvert.SerializeObject(commentModel)}{Environment.NewLine}{str}";

                    tSQLConnection.WriteTsql(str);

                    Console.WriteLine("OK.");
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }

        }
    }
}