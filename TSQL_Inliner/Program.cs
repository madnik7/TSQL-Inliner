using Microsoft.SqlServer.TransactSql.ScriptDom;
using Newtonsoft.Json;
using System;
using System.Linq;
using TSQL_Inliner.Method;
using TSQL_Inliner.Model;

namespace TSQL_Inliner
{
    class Program
    {
        static void Main(string[] args)
        {
            Sql140ScriptGenerator sql140ScriptGenerator = new Sql140ScriptGenerator();
            TSQLReader tSQLReader = new TSQLReader();
            TSqlFragment sqlFragment = tSQLReader.ReadTsql("dbo", "Main");

            CommentModel commentModel = new CommentModel();
            if (sqlFragment.ScriptTokenStream != null)
            {
                var comment = sqlFragment.ScriptTokenStream.FirstOrDefault(a => a.TokenType == TSqlTokenType.SingleLineComment);
                if (comment != null)
                {
                    try
                    {
                        commentModel = JsonConvert.DeserializeObject<CommentModel>(comment.Text.Substring(comment.Text.IndexOf('{'), comment.Text.LastIndexOf('}') - comment.Text.IndexOf('{') + 1));
                    }
                    catch { }

                    comment.Text = $"#InlinerStart {JsonConvert.SerializeObject(commentModel)} #InlinerEnd New Comment";
                }
            }

            sql140ScriptGenerator.GenerateScript(sqlFragment, out string str);
            str = $"-- #InlinerStart {JsonConvert.SerializeObject(commentModel)} #InlinerEnd {Environment.NewLine} {str}";
            Console.WriteLine(str);
            Console.ReadKey();
        }
    }
}