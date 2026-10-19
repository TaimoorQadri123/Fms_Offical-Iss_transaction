using System;
using System.Collections.Generic;
using System.Text;

namespace FMSMyService.Models
{
    internal class Rules
    {
        public Rules()
        {
            Criterias = new List<Criterias>();
        }
        public long RuleId { get; set; }
        public string RuleName { get; set; }
        public string Description { get; set; }
        public string Status { get; set; }
        public int Frequency { get; set; }
        public int Days { get; set; }
        public int Hours { get; set; }
        public int Minutes { get; set; }
        public bool IsLookBack { get; set; }
        public bool IsCumulative { get; set; }
        public string RuleTye { get; set; }
        public int OrderOfExecution { get; set; }
        public bool AutoClose { get; set; }

        public DateTime? CreatedAt { get; set; }
        public string CreatedBy { get; set; }
        public DateTime? EditedAt {  get; set; }
        public string EditedBy { get; set; }
        public DateTime? DeletedAt { get; set; }
        public bool IsDeleted { get; set; }

        public List<Criterias> Criterias { get; set; }

    }
}
