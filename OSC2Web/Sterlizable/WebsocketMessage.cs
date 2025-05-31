using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace OSC2Web.Sterlizable
{
    internal class WebsocketMessage
    {

        [JsonProperty("type")]
        public string MessageType { get; set; } = "message";
        [JsonProperty("data")]
        public object Data { get; set; }
    }
}
