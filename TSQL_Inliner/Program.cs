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
        static void Main(string[] args)
        {
            TSQLConnection tSQLConnection = new TSQLConnection();
            ProcOptimizer = new ProcOptimizer(tSQLConnection);

            Console.WriteLine("Getting dbo proccedures list");

            var allSPs = tSQLConnection.GetAllStoredProcedures("dbo")
                .Where(a => a.ToLower() == "main")// just for testing operations
                .Select(a => new SpInfo() { Schema = "dbo", Name = a });

            foreach (var spInfo in allSPs)
            {
                ProcOptimizer.Process(spInfo);
            }
            //Console.WriteLine($"{Environment.NewLine}=-=-=-=-=-=-=-=-=-=-={Environment.NewLine}Press any key to exit ...");
            Console.ReadKey();
        }
    }
}