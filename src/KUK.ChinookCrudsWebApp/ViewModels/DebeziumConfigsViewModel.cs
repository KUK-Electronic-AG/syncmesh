namespace KUK.ChinookCrudsWebApp.ViewModels
{
    public class DebeziumConfigsViewModel
    {
        public dynamic Config1 { get; set; }
        public dynamic Config2 { get; set; }
        public List<string> DisplayParameters { get; set; }
        public bool IsDockerMode { get; set; }
    }
}
