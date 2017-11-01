﻿using Microsoft.SqlServer.TransactSql.ScriptDom;
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
                foreach (var comment in fragment.ScriptTokenStream.Where(a => a.TokenType == TSqlTokenType.SingleLineComment || a.TokenType == TSqlTokenType.MultilineComment))
                {
                    if (comment.Text.ToLower().Contains("#inliner"))
                    {
                        try
                        {
                            commentModel = JsonConvert.DeserializeObject<CommentModel>(comment.Text.Substring(comment.Text.IndexOf('{'), comment.Text.LastIndexOf('}') - comment.Text.IndexOf('{') + 1));
                        }
                        catch
                        { }
                    }
                    else
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