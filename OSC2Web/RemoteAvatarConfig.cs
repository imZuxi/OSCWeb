using Newtonsoft.Json;

namespace OSC2Web
{
    internal class AvatarConfig
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public List<VRCParamList> parameters { get; set; } = new(); 
    }


    internal class VRCOSCConfig
    {
        public string id;
        public string name;
        public List<VRCParamList> parameters;
    }

    internal class VRCParamList
    {

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("input")]
        public OscParameterIO Input { get; set; }

        [JsonProperty("output")]
        public OscParameterIO Output { get; set; }

        public object Value { get; set; }
    }

    public class OscParameterIO
    {
        [JsonProperty("address")]
        public string Address { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }
    }


    internal class VRCAvatarConfig
    {
        public List<VRCAvatarParam> animationParameters;
    }

    internal class VRCAvatarParam
    {
        public string name;
        public object value;
    }
}
