using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Contraption.Core
{
    public static class MachineJson
    {
        private static readonly JsonSerializerSettings Settings = new()
        {
            Formatting = Formatting.Indented,
            ObjectCreationHandling = ObjectCreationHandling.Replace,
            Converters = { new StringEnumConverter() },
        };

        public static string ToJson(MachineDefinition def) => JsonConvert.SerializeObject(def, Settings);

        public static MachineDefinition FromJson(string json) => JsonConvert.DeserializeObject<MachineDefinition>(json, Settings);
    }
}
