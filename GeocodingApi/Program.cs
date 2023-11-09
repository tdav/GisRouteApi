using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeocodingApi
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var geo = new Geocoding();
            geo.Encode();
        }
    }
}
