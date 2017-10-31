using Microsoft.SqlServer.TransactSql.ScriptDom;
using System.Collections.Generic;
using System.Data.SqlClient;
using Microsoft.SqlServer.Server;
using Microsoft.SqlServer;
using System.IO;
using System;

namespace TSQL_Inliner.Method
{
    public class TSQLReader
    {
        public TSqlFragment ReadTsql(string schema, string procedure)
        {
            string sqlConnectionString = @"Data Source=localhost;Initial Catalog=test;User ID=mohsen;Password=123123;MultipleActiveResultSets=True;Application Name=EntityFramework";
            string ReadSPScript = $@"SELECT definition AS Script
                                    FROM sys.sql_modules  
                                    WHERE object_id = (OBJECT_ID(N'{schema}.{procedure}'));";

            string Script = string.Empty;

            SqlConnection sqlConnection = new SqlConnection(sqlConnectionString);
            SqlCommand sqlCommand = new SqlCommand(ReadSPScript, sqlConnection);
            sqlConnection.Open();
            using (SqlDataReader reader = sqlCommand.ExecuteReader())
            {
                if (reader.Read())
                {
                    Script = reader["Script"].ToString();
                }
            }
            sqlConnection.Close();

            var parser = new TSql140Parser(true);
            var fragment = parser.Parse(new StringReader(Script), out IList<ParseError> errors);

            MasterVisitor myVisitor = new MasterVisitor();
            fragment.Accept(myVisitor);

            return fragment;
        }
    }
}