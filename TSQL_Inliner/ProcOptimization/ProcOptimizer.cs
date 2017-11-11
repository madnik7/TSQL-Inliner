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
                if (spInfo.Name == "GetSystemContext")
                {

                }
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
                Console.Write(" Not Found.");
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

        public SqlDataTypeOption GetSqlDataTypeOption(ReturnStatement returnStatement)
        {
            if (FunctionReturnType == null)
                return SqlDataTypeOption.Int;
            else
                switch (((ScalarFunctionReturnType)FunctionReturnType).DataType.Name.BaseIdentifier.Value.ToLower())
                {
                    case "int":
                        return SqlDataTypeOption.Int;
                    case "bigint":
                        return SqlDataTypeOption.BigInt;
                    case "float":
                        return SqlDataTypeOption.Float;
                    case "binary":
                        return SqlDataTypeOption.Binary;
                    case "datetime":
                        return SqlDataTypeOption.DateTime;
                    case "datetime2":
                        return SqlDataTypeOption.DateTime2;
                    case "date":
                        return SqlDataTypeOption.Date;
                    case "bit":
                        return SqlDataTypeOption.Bit;
                    case "char":
                        return SqlDataTypeOption.Char;
                    case "nvarchar":
                        return SqlDataTypeOption.NVarChar;
                    case "decimal":
                        return SqlDataTypeOption.Decimal;
                    case "image":
                        return SqlDataTypeOption.Image;
                    case "money":
                        return SqlDataTypeOption.Money;
                    case "nchar":
                        return SqlDataTypeOption.NChar;
                    case "ntext":
                        return SqlDataTypeOption.NText;
                    case "numeric":
                        return SqlDataTypeOption.Numeric;
                    case "real":
                        return SqlDataTypeOption.Real;
                    case "smalldatetime":
                        return SqlDataTypeOption.SmallDateTime;
                    case "smallint":
                        return SqlDataTypeOption.SmallInt;
                    case "smallmoney":
                        return SqlDataTypeOption.SmallMoney;
                    case "table":
                        return SqlDataTypeOption.Table;
                    case "text":
                        return SqlDataTypeOption.Text;
                    case "time":
                        return SqlDataTypeOption.Time;
                    case "varchar":
                        return SqlDataTypeOption.VarChar;

                    default:
                        return SqlDataTypeOption.None;
                }
        }
    }
}