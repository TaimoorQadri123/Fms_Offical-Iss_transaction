using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FMSMyService
{
    internal class RulesAndCriterias
    {
        public long ParameterId { get; set; }
        public long CriterialId { get; set; }
        public string FieldType { get; set; }
        public string Operator { get; set; }
        public string ParameterName { get; set; }
        public string Value { get; set; }
        public long RuldId { get; set; }
        public string LogicalOperator { get; set; }
        public string CriteriaType { get; set; }

        public bool IsCumulative { get; set; }
        public bool IsDeleted { get; set; }
        public long RuleId { get; internal set; }
        public object CriteriaId { get; internal set; }
    }
}
