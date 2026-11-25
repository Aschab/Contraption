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

        public static string Serialize<T>(T value) => JsonConvert.SerializeObject(value, Settings);

        public static T Deserialize<T>(string json) => JsonConvert.DeserializeObject<T>(json, Settings);
    }
}
