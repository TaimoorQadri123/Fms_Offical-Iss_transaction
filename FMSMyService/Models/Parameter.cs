using System;
using System.Collections.Generic;
using System.Text;

namespace FMSMyService.Models
{
    internal class Parameter
    {
        public long ParameterId { get; set; }
        public string ParameterName { get; set; } = "";
        public string FieldType { get; set; } = "";
        public string DataType { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string CreatedBy { get; set; }
        public DateTime? EditedAt {  get; set; }
        public string  EditedBy { get; set; }
        public DateTime? DeletedAt { get; set; }
    }
}
