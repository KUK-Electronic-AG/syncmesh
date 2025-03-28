using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Dynamic;

namespace KUK.ChinookCruds.Debezium
{
    public class DebeziumConfigService
    {
        private dynamic _config1;
        private dynamic _config2;
        private readonly List<string> _displayParameters;

        public DebeziumConfigService()
        {
            _config1 = LoadConfig("Assets/debezium-connector-config-1.json");
            _config2 = LoadConfig("Assets/debezium-connector-config-2.json");
            _displayParameters = LoadDisplayParameters("Assets/display-parameters.txt");
        }

        public dynamic GetConfig1() => _config1;
        public dynamic GetConfig2() => _config2;
        public List<string> GetDisplayParameters() => _displayParameters;

        public void UpdateConfig1(dynamic config) => _config1 = config;
        public void UpdateConfig2(dynamic config) => _config2 = config;

        private dynamic LoadConfig(string relativePath)
        {
            var basePath = AppContext.BaseDirectory;
            var fullPath = Path.Combine(basePath, relativePath);
            if (!Path.Exists(fullPath))
            {
                throw new InvalidOperationException($"Cannot load debezium connector config. File '{fullPath}' does nto exist.");
            }
            var json = File.ReadAllText(fullPath);
            if (string.IsNullOrEmpty(json))
            {
                throw new InvalidOperationException($"Cannot load debezium connector config. Please check for mistakes.");
            }
            return JsonConvert.DeserializeObject<ExpandoObject>(json, new ExpandoObjectConverter());
        }

        private List<string> LoadDisplayParameters(string relativePath)
        {
            var basePath = AppContext.BaseDirectory;
            var fullPath = Path.Combine(basePath, relativePath);
            return File.ReadAllLines(fullPath).ToList();
        }
    }
}
