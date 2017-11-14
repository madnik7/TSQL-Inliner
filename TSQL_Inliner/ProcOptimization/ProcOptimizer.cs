using Microsoft.SqlServer.TransactSql.ScriptDom;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using TSQL_Inliner.Model;

namespace TSQL_Inliner.ProcOptimization
{
    public class ProcOptimizer
    {
        public TSQLConnection TSQLConnection { get; private set; }
        public int VariableCounter { get; private set; }
        public FunctionReturnType FunctionReturnType { get; set; }
        public string GoToName { get; set; }
        public List<string> ProcessedProcdures { get; set; }

        public ProcOptimizer(TSQLConnection tSQLConnection)
        {
            TSQLConnection = tSQLConnection;
            VariableCounter = TSQLConnection.VariableCounter;
            GoToName = string.Empty;
            ProcessedProcdures = new List<string>();
        }

        public void IncreaseVariableCounter()
        {
            VariableCounter++;
        }

        /// <summary>
        /// set new name for parameters based on level of stored procedure
        /// </summary>
        /// <param name="name"></param>
        /// <returns>New Name</returns>
        public string BuildNewName(string name, int counter)
        {
            if (name.ToLower().Contains("_inliner"))
                return $"{name}_{counter}";
            return $"{name}_inliner{counter}";
        }

        public void Process(SpInfo spInfo)
        {
            string newScript;
            try
            {
                Console.Write($"\nProcessing {spInfo.Schema}.{spInfo.Name}");
                newScript = ProcessScript(spInfo);
                if (newScript != null)
                {
                    TSQLConnection.WriteScript(newScript);
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write($"Error! {ex.Message}");
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
            sql140ScriptGenerator.GenerateScript(procModel.TSqlFragment, out string script, out IList<ParseError> parseError);

            procModel.CommentModel.IsOptimized = true;
            Regex regex = new Regex(@"\bEND\b");
            script = $"{procModel.TopComments}-- #Inliner {JsonConvert.SerializeObject(procModel.CommentModel)}{Environment.NewLine}" +
                $"{regex.Replace(script, "END;").Replace("END; TRY", "END TRY").Replace("END; CATCH", "END CATCH")}";

            return script;
        }

        public ProcModel ProcessScriptImpl(SpInfo spInfo)
        {
            ProcModel procModel = GetProcModel(spInfo);
            if (procModel.TSqlFragment == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write(" Not Found.");
                Console.ResetColor();               
                return null;
            }

            if (procModel.CommentModel.IsOptimizable && !procModel.CommentModel.IsOptimized)
            {
                Console.Write($"... ");

                ExecuteVisitor executeVisitor = new ExecuteVisitor();
                procModel.TSqlFragment.Accept(executeVisitor);

                return procModel;
            }

            Console.Write(procModel.CommentModel.IsOptimized ? ", Already optimized." : ", Not Optimizable.");
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
            if (script != null)
            {
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
            }
            return procModel;
        }
    }
}