namespace KUK.ManagementServices.Utilities
{
    public class DatabasesBasicConfiguration
    {
        public string RootOldDbConnectionString { get; set; }
        public string OldDbConnectionString { get; set; }
        public string NewDbConnectionString { get; set; }
        public string OldDbName { get; set; }
        public string NewDbName { get; set; }
        public int OldDbPort { get; set; }
        public int NewDbPort { get; set; }
    }
}
