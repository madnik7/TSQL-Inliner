using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TSQL_Inliner.Model
{
    public class CommentModel
    {
        public CommentModel()
        {
            this.InlineMode = "Inline";               
            this.IsOptimizable = true;
            this.IsOptimized = false;
        }

        /// <summary>
        /// Does this code require processing or remove this or do nothing?
        /// Inline|Remove|None
        /// </summary>
        public string InlineMode { get; set; }

        /// <summary>
        /// Can process this code?
        /// </summary>
        public bool IsOptimizable { get; set; }

        /// <summary>
        /// Has this code been processed?
        /// </summary>
        public bool IsOptimized { get; set; }
    }
}
