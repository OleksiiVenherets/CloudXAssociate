using Newtonsoft.Json;
using System.Collections.Generic;

namespace DeliveryOrderProcessor;

public class Order
{
    [JsonProperty("id")]
    public string Id { get; set; }

    public object ShippingAddress { get; set; }

    public List<object> Items { get; set; }

    public double FinalPrice { get; set; }
}
