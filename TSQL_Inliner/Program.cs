using System;
using System.Linq;
using TSQL_Inliner.Model;
using TSQL_Inliner.Process;

namespace TSQL_Inliner
{
    public class Program
    {
        public static Inliner Inliner { get; set; }
        static void Main(string[] args)
        {
            TSQLConnection tSQLConnection = new TSQLConnection();
            Inliner = new Inliner(tSQLConnection);

            Console.WriteLine("Getting dbo proccedures list");

            var allSPs = tSQLConnection.GetAllStoredProcedures("dbo")
                .Where(a => a.ToLower() == "main")// just for testing operations
                .Select(a => new SpInfo() { Schema = "dbo", Name = a });

            foreach (var spInfo in allSPs)
            {
                Inliner.Process(spInfo);
            }
            Console.WriteLine($"{Environment.NewLine}=-=-=-=-=-=-=-=-=-=-={Environment.NewLine}Press any key to exit ...");
            Console.ReadKey();
        }
    }
}