namespace KUK.ManagementServices.Utilities
{
    public class DbStatuses
    {
        public DbStatusEnum OldDbStatus { get; set; }
        public DbStatusEnum NewDbStatus { get; set; }
        public CheckStatusEnum OldSchemaExists { get; set; }
        public CheckStatusEnum NewSchemaExists { get; set; }
        public CheckStatusEnum OldSchemaHasData { get; set; }
        public CheckStatusEnum NewSchemaHasData { get; set; }
    }
}
