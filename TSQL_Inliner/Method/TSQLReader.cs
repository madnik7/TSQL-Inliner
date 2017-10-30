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
            //string ReadSPScript = $@"SELECT	S.name AS SchemaName,
            //                                P.name AS ProcedureName,
            //                                SM.definition AS Script
            //                        FROM sys.procedures AS P
            //                        	INNER JOIN sys.schemas AS S ON S.schema_id = P.schema_id
            //                        	INNER JOIN sys.parameters AS Parameter ON Parameter.object_id = P.object_id
            //                        	INNER JOIN sys.sql_modules AS SM ON SM.object_id = P.object_id
            //                        WHERE	S.name = '{schema}' AND	P.name = '{procedure}'
            //                        GROUP BY S.name, P.name, SM.definition;";

            string ReadSPScript = $@"SELECT definition AS Script
                                    FROM sys.sql_modules  
                                    WHERE object_id = (OBJECT_ID(N'{schema}.{procedure}'));";

            string sqlConnectionString = @"Data Source=localhost;Initial Catalog=test;User ID=mohsen;Password=123123;MultipleActiveResultSets=True;Application Name=EntityFramework";
            string Script = string.Empty;

            var con = new SqlConnection(sqlConnectionString);
            var cmd = new SqlCommand(ReadSPScript, con);
            con.Open();
            using (SqlDataReader reader = cmd.ExecuteReader())
            {
                if (reader.Read())
                {
                    Script = reader["Script"].ToString();
                }
            }

            var parser = new TSql140Parser(true);
            var fragment = parser.Parse(new StringReader(Script), out IList<ParseError> errors);

            MasterVisitor myVisitor = new MasterVisitor();
            fragment.Accept(myVisitor);

            return fragment;
        }
    }
}