using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json;

namespace OSC2Web.Sterlizable
{
    internal class JsonContractResolvers
    {
        public class StripInputOutputContractResolver : DefaultContractResolver
        {
            protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
            {
                var props = base.CreateProperties(type, memberSerialization);

                // Check for the VRCParamList type and strip "Input" and "Output"
                if (type == typeof(VRCParamList))
                {
                    props = props
                        .Where(p => !string.Equals(p.PropertyName, "Input", StringComparison.OrdinalIgnoreCase)
                                 && !string.Equals(p.PropertyName, "Output", StringComparison.OrdinalIgnoreCase))
                        .ToList();
                }

                return props;
            }
        }
    }
}
