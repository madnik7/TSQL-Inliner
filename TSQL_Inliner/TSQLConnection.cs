using System.Collections.Generic;
using System.Data.SqlClient;
using TSQL_Inliner.Model;
using System;

namespace TSQL_Inliner
{
    public class TSQLConnection
    {
        protected string sqlConnectionString = @"Integrated Security=SSPI;Initial Catalog=IcUserService;Data Source=localhost;";

        internal string GetScript(SpInfo spInfo)
        {
            string ReadSPScript = $@"SELECT definition AS Script
                                    FROM sys.sql_modules  
                                    WHERE object_id = (OBJECT_ID(N'{spInfo.Schema}.{spInfo.Name}'));";

            SqlConnection sqlConnection = new SqlConnection(sqlConnectionString);
            SqlCommand sqlCommand = new SqlCommand(ReadSPScript, sqlConnection);
            sqlConnection.Open();
            using (SqlDataReader reader = sqlCommand.ExecuteReader())
            {
                if (reader.Read())
                    return reader["Script"].ToString();
            }
            sqlConnection.Close();
            return null;
        }

        public List<string> GetAllStoredProcedures(string schema)
        {
            string ReadSPScript = $@"SELECT	P.name as SPName
                                    FROM sys.procedures AS P
	                                INNER JOIN sys.schemas AS S ON S.schema_id = P.schema_id
                                    WHERE	S.name = '{schema}';";

            List<string> Script = new List<string>();

            SqlConnection sqlConnection = new SqlConnection(sqlConnectionString);
            SqlCommand sqlCommand = new SqlCommand(ReadSPScript, sqlConnection);
            sqlConnection.Open();
            using (SqlDataReader reader = sqlCommand.ExecuteReader())
            {
                while (reader.Read())
                {
                    Script.Add(Convert.ToString(reader["SPName"]));
                }
            }
            sqlConnection.Close();

            return Script;
        }

        public void WriteScript(string script)
        {
            SqlConnection sqlConnection = new SqlConnection(sqlConnectionString);
            SqlCommand sqlCommand = new SqlCommand(script, sqlConnection);
            sqlConnection.Open();
            SqlDataReader reader = sqlCommand.ExecuteReader();
            sqlConnection.Close();
        }
    }
}