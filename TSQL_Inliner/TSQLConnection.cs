using System.Collections.Generic;
using System.Data.SqlClient;
using TSQL_Inliner.Model;
using System;

namespace TSQL_Inliner
{
    public class TSQLConnection
    {
        //public string SqlConnectionString = @"Integrated Security=SSPI;Initial Catalog=IcUserService;Data Source=localhost;";

        public string ConnectionString { get; private set; }

        public TSQLConnection(string connectionSting)
        {
            ConnectionString = connectionSting;
        }

        internal string GetScript(SpInfo spInfo)
        {
            string ReadSPScript = $@"SELECT definition AS Script
                                    FROM sys.sql_modules  
                                    WHERE object_id = (OBJECT_ID(N'{spInfo.Schema}.{spInfo.Name}'));";

            SqlConnection sqlConnection = new SqlConnection(ConnectionString);
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

        public SpInfo[] GetAllStoredProcedures(string schema)
        {
            List<SpInfo> spInfos = new List<SpInfo>();
            string ReadSPScript = $@"SELECT	P.name as SPName, S.name as SPSchema
                                    FROM sys.procedures AS P
	                                INNER JOIN sys.schemas AS S ON S.schema_id = P.schema_id
                                    WHERE	S.name = '{schema}';";

            SqlConnection sqlConnection = new SqlConnection(ConnectionString);
            SqlCommand sqlCommand = new SqlCommand(ReadSPScript, sqlConnection);
            sqlConnection.Open();
            using (SqlDataReader reader = sqlCommand.ExecuteReader())
            {
                while (reader.Read())
                {
                    spInfos.Add(new SpInfo()
                    {
                        Name = Convert.ToString(reader["SPName"]),
                        Schema = Convert.ToString(reader["SPSchema"])
                    });
                }
            }
            sqlConnection.Close();

            return spInfos.ToArray();
        }

        public void WriteScript(string script)
        {
            SqlConnection sqlConnection = new SqlConnection(ConnectionString);
            SqlCommand sqlCommand = new SqlCommand(script, sqlConnection);
            sqlConnection.Open();
            SqlDataReader reader = sqlCommand.ExecuteReader();
            sqlConnection.Close();
        }
    }
}