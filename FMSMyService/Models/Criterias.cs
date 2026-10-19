using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FMSMyService.Models
{
    internal class Criterias
    {
        public long CritrialId { get; set; }
        public string Operator { get; set; }
        public string LogicalOperator { get; set; }
        public string Value { get; set; }
        public string CriteriaType { get; set; }
        public bool IsCumulative { get; set; }
        public long RuleId { get; set; }
        public Rules Rule { get; set; }
        public long Parameter { get; set; }
        public string ParameterName { get; set; }
        public string DisplayName { get; set; }
        public string FieldType { get; set; }
        public bool IsWatchList { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string CreatedBy { get; set; }
        public DateTime? EditedAt { get; set; }
        public string EditedBy { get; set; }
        public DateTime? DeletedAt { get; set; }
        public bool IsDeleted { get; set; }
        public object CriteriaId { get; internal set; }
    }
}
