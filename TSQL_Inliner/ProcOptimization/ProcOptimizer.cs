using Microsoft.SqlServer.TransactSql.ScriptDom;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TSQL_Inliner.Model;

namespace TSQL_Inliner.ProcOptimization
{
    public class ProcOptimizer
    {
        public TSQLConnection TSQLConnection { get; private set; }
        public int VariableCounter { get; private set; }
        public bool hasReturnStatement { get; set; }
        public string GoToName { get; set; }
        public List<string> ProcessedProcdures { get; set; }

        public ProcOptimizer(TSQLConnection tSQLConnection)
        {
            VariableCounter = 0;
            TSQLConnection = tSQLConnection;
            GoToName = string.Empty;
            hasReturnStatement = false;
            ProcessedProcdures = new List<string>();
        }

        public void IncreaseVariableCounter()
        {
            VariableCounter++;
        }

        /// <summary>
        /// set new name for parameters based on level of stored procedure
        /// </summary>
        /// <param name="Name"></param>
        /// <returns>New Name</returns>
        public string NewName(string Name)
        {
            if (Name.ToLower().Contains("_inliner"))
                return $"{Name}_{ Program.ProcOptimizer.VariableCounter}";
            return $"{Name}_inliner{Program.ProcOptimizer.VariableCounter}";
        }

        public void Process(SpInfo spInfo)
        {
            try
            {
                Console.Write($"\nProcessing {spInfo.Schema}.{spInfo.Name}");

                var newScript = ProcessScript(spInfo);
                if (newScript != null)
                {
                    TSQLConnection.WriteScript(newScript);
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error! {ex.Message}");
                Console.ResetColor();
            }
        }

        string ProcessScript(SpInfo spInfo)
        {
            ProcModel procModel = ProcessScriptImpl(spInfo);
            if (procModel == null)
                return null;

            //Generate new script from new fragment
            Sql140ScriptGenerator sql140ScriptGenerator = new Sql140ScriptGenerator();
            sql140ScriptGenerator.GenerateScript(procModel.TSqlFragment, out string script);

            procModel.CommentModel.IsOptimized = true;
            script = $"{procModel.TopComments}-- #Inliner {JsonConvert.SerializeObject(procModel.CommentModel)}{Environment.NewLine}{script}";

            return script;
        }

        public ProcModel ProcessScriptImpl(SpInfo spInfo)
        {
            ProcModel procModel = GetProcModel(spInfo);
            if (procModel.CommentModel.IsOptimizable && !procModel.CommentModel.IsOptimized)
            {
                Console.Write($"... ");

                ExecuteVisitor executeVisitor = new ExecuteVisitor();
                procModel.TSqlFragment.Accept(executeVisitor);

                return procModel;
            }

            Console.WriteLine(procModel.CommentModel.IsOptimized ? ", Already optimized." : ", Not Optimizable.");
            return null;
        }

        public ProcModel GetProcModel(SpInfo spInfo)
        {
            var parser = new TSql140Parser(true);
            var script = TSQLConnection.GetScript(spInfo);
            ProcModel procModel = new ProcModel()
            {
                SpInfo = spInfo
            };
            var fragment = parser.Parse(new StringReader(script), out IList<ParseError> errors);
            if (fragment.ScriptTokenStream != null)
            {
                //Read all comment befor the first "Create" or "Alter"
                var firstCreateOrAlterLine = fragment.ScriptTokenStream.FirstOrDefault(a => a.TokenType == TSqlTokenType.Alter || a.TokenType == TSqlTokenType.Create);

                foreach (var comment in fragment.ScriptTokenStream.Where(a => (a.TokenType == TSqlTokenType.SingleLineComment || a.TokenType == TSqlTokenType.MultilineComment) &&
                a.Line < (firstCreateOrAlterLine == null ? 1 : firstCreateOrAlterLine.Line)))
                {
                    if (comment.Text.ToLower().Contains("#inliner"))
                    {
                        try
                        {
                            procModel.CommentModel = JsonConvert.DeserializeObject<CommentModel>(comment.Text.Substring(comment.Text.IndexOf('{'), comment.Text.LastIndexOf('}') - comment.Text.IndexOf('{') + 1));
                        }
                        catch (Exception ex)
                        {
                            throw new Exception($"could not parse #inliner at: {spInfo.Schema}.{spInfo.Name}{Environment.NewLine}", ex);
                        }
                    }
                    else
                    {
                        procModel.TopComments += $"{comment.Text}{Environment.NewLine}";
                    }
                }
                procModel.TSqlFragment = fragment;
            }

            return procModel;
        }
    }
}