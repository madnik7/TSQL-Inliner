using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace TSQL_Inliner.Model
{
    public class ProcModel
    {
        public CommentModel CommentModel { get; set; } = new CommentModel();
        public string TopComments { get; set; } = string.Empty;
        public SpInfo SpInfo { get; set; }
        public string Script { get; set; }
        public TSqlFragment TSqlFragment { get; set; }
    }
}
