using Microsoft.SqlServer.TransactSql.ScriptDom;
using System.Collections.Generic;
using System.IO;

namespace TSQL_Inliner.Method
{
    public class TSQLReader
    {
        public TSqlFragment ReadTsql(string LocalAddress)
        {
            var parser = new TSql140Parser(true);
            var fragment = parser.Parse(new StreamReader(LocalAddress), out IList<ParseError> errors);

            MasterVisitor myVisitor = new MasterVisitor();
            fragment.Accept(myVisitor);

            return fragment;
        }
    }
}