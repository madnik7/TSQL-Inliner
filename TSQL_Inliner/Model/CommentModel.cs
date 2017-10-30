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
            this.InlineMode = "None";               
            this.IsOptimizable = true;
            this.IsOptimized = false;
        }

        /// <summary>
        /// Inline|Remove|None
        /// </summary>
        public string InlineMode { get; set; }

        /// <summary>
        /// Does this code require processing?
        /// </summary>
        public bool IsOptimizable { get; set; }

        /// <summary>
        /// Has this code been processed?
        /// </summary>
        public bool IsOptimized { get; set; }
    }
}
