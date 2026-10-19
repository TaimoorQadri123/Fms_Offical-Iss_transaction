using System;
using System.Collections.Generic;
using System.Text;

namespace FMSMyService.Models
{
    internal class ModifyPoints
    {
        public string ModifyType { get; set; } 
        public DateTime ModifiedOn { get; set; }
        public long TrxLogId2 { get; set; }
        public bool IsSeeded { get; set; }
    }
}
