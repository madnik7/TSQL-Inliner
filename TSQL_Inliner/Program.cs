using System;
using System.Linq;
using TSQL_Inliner.Model;
using TSQL_Inliner.Inliner;
using TSQL_Inliner.ProcOptimization;

namespace TSQL_Inliner
{
    public class Program
    {
        public static ProcOptimizer ProcOptimizer { get; set; }

        static void ShowHelp()
        {
            Console.WriteLine("inline TSQL procedures and functions.");
            Console.WriteLine("");
            Console.WriteLine("");
            Console.WriteLine("tsql_inliner /connectionString connectionString /schema schema [/procName procName]");
            Console.WriteLine("");
            Console.WriteLine("  connectionString\t SQL Server connection string.");
            Console.WriteLine("  schema\t\t the schema of the database. Default is dbo.");
            Console.WriteLine("  procName\t\t the name of the procedure or function to be optimized. Default is all objects in the schema.");
        }

        static AppArgument ProcessArgument(string[] args)
        {
            var appArgument = new AppArgument
            {
                Schema = "dbo"
            };

            //process argument
            var lastKey = "";
            foreach (var item in args)
            {
                var key = item.ToLower();

                switch (lastKey)
                {
                    case "/connectionstring": appArgument.ConnectionString = item; break;
                    case "/schema": appArgument.Schema = item; break;
                    case "/procname": appArgument.ProcName = item; break;
                }
                lastKey = key;
            }

            if (appArgument.ConnectionString == null || appArgument.Schema == null)
                return null;

            return appArgument;
        }

        static void Main(string[] args)
        {
            var appArgs = ProcessArgument(args);
            if (appArgs == null)
            {
                ShowHelp();
                return;
            }

            TSQLConnection tSQLConnection = new TSQLConnection(appArgs.ConnectionString);
            ProcOptimizer = new ProcOptimizer(tSQLConnection);

            Console.WriteLine("Getting dbo proccedures list");

            var allSPs = tSQLConnection.GetAllStoredProcedures(appArgs.Schema)
                .Where(a => appArgs.ProcName==null || a.ToLower() == appArgs.ProcName)// just for testing operations
                .Select(a => new SpInfo() { Schema = appArgs.Schema, Name = a });

            foreach (var spInfo in allSPs)
            {
                ProcOptimizer.Process(spInfo);
            }
            Console.WriteLine($"{Environment.NewLine}=-=-=-=-=-=-=-=-=-=-={Environment.NewLine}Press any key to exit ...");
            Console.ReadKey();
        }
    }
}