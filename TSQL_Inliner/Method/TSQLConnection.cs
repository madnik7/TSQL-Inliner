using Microsoft.SqlServer.TransactSql.ScriptDom;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using TSQL_Inliner.Model;
using Newtonsoft.Json;
using System.Linq;
using System;

namespace TSQL_Inliner.Method
{
    public class TSQLConnection
    {
        string sqlConnectionString = @"Data Source=localhost;Initial Catalog=test;User ID=mohsen;Password=123123;MultipleActiveResultSets=True;Application Name=EntityFramework";
        public TSqlFragment ReadTsql(out CommentModel commentModel, out string topComments, string schema, string procedure)
        {
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

            commentModel = new CommentModel();
            topComments = string.Empty;
            if (fragment.ScriptTokenStream != null)
            {
                foreach (var comment in fragment.ScriptTokenStream.Where(a => a.TokenType == TSqlTokenType.SingleLineComment))
                {
                    try
                    {
                        commentModel = JsonConvert.DeserializeObject<CommentModel>(comment.Text.Substring(comment.Text.IndexOf('{'), comment.Text.LastIndexOf('}') - comment.Text.IndexOf('{') + 1));
                    }
                    catch
                    {
                        topComments += $"-- {comment}{Environment.NewLine}";
                    }
                }
            }

            if (commentModel.IsOptimizable && !commentModel.IsOptimized)
            {
                MasterVisitor myVisitor = new MasterVisitor();
                fragment.Accept(myVisitor);
            }

            return fragment;
        }

        public void WriteTsql(string script)
        {
            SqlConnection sqlConnection = new SqlConnection(sqlConnectionString);
            SqlCommand sqlCommand = new SqlCommand(script, sqlConnection);
            sqlConnection.Open();
            SqlDataReader reader = sqlCommand.ExecuteReader();
            sqlConnection.Close();
        }
    }
}