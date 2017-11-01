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
        protected string sqlConnectionString = @"Integrated Security=SSPI;Initial Catalog=IcLoyalty;Data Source=localhost;";

        /// <summary>
        /// //Read stored procedire by schema and name
        /// </summary>
        /// <param name="commentModel">json comment for process</param>
        /// <param name="topComments">all comments</param>
        /// <param name="schema"></param>
        /// <param name="procedure"></param>
        /// <returns></returns>
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
                var firstCreateOrAlterLine = fragment.ScriptTokenStream.FirstOrDefault(a => a.TokenType == TSqlTokenType.Alter || a.TokenType == TSqlTokenType.Create);

                foreach (var comment in fragment.ScriptTokenStream.Where(a => (a.TokenType == TSqlTokenType.SingleLineComment || a.TokenType == TSqlTokenType.MultilineComment) &&
                a.Line < (firstCreateOrAlterLine == null ? 1 : firstCreateOrAlterLine.Line)))
                {
                    if (comment.Text.ToLower().Contains("#inliner"))
                    {
                        try
                        {
                            commentModel = JsonConvert.DeserializeObject<CommentModel>(comment.Text.Substring(comment.Text.IndexOf('{'), comment.Text.LastIndexOf('}') - comment.Text.IndexOf('{') + 1));
                        }
                        catch (Exception ex)
                        {
                            throw new Exception($"could not parse #inliner at: {schema}.{procedure}{Environment.NewLine}", ex);
                        }
                    }
                    else
                    {
                        topComments += $"{comment.Text}{Environment.NewLine}";
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