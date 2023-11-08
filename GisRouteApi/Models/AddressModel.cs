namespace GisRouteApi.Models
{
    public class AddressModel
    {
        public int place_id { get; set; }
        public string licence { get; set; }
        public string osm_type { get; set; }
        public int osm_id { get; set; }
        public string lat { get; set; }
        public string lon { get; set; }
        public string _class { get; set; }
        public string type { get; set; }
        public int place_rank { get; set; }
        public float importance { get; set; }
        public string addresstype { get; set; }
        public string name { get; set; }
        public string display_name { get; set; }
        public Address address { get; set; }
        public string[] boundingbox { get; set; }
    }

    public class Address
    {
        public string house_number { get; set; }
        public string road { get; set; }
        public string neighbourhood { get; set; }
        public string county { get; set; }
        public string city { get; set; }
        public string ISO31662lvl4 { get; set; }
        public string postcode { get; set; }
        public string country { get; set; }
        public string country_code { get; set; }
        public string state { get; set; }
    }
}
