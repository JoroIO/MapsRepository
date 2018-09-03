using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WeeplaceMAPS
{
    public class CheckIn
    {
        public string UserId { get; set; }

        public string PlaceId { get; set; }

        public DateTime DateTime { get; set; }
        
        public double Latitude { get; set; }

        public double Longitude { get; set; }

        public string City { get; set; }

        public string Category { get; set; }
    }
}
