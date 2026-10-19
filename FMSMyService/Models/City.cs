using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FMSISSTransactionService.Models
{
    public class City
    {
        public int GeoNameId { get; set; }
        public string Name { get; set; }
        public string RawName { get; set; }
        public string BaseName { get; set; }
        public string Admin1Code { get; set; }
        public string FeatureCode { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public int Population { get; set; }
    }
}
