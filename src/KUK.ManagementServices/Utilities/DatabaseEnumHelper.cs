namespace KUK.ManagementServices.Utilities
{
    public static class DatabaseEnumHelper
    {
        public static bool TryParseDatabase(string database, out WhichDatabaseEnum dbEnum)
        {
            switch (database.ToLower())
            {
                case "old":
                case "mysql80":
                    dbEnum = WhichDatabaseEnum.OldDatabase;
                    return true;
                case "new":
                case "destinationpostgres":
                    dbEnum = WhichDatabaseEnum.NewDatabase;
                    return true;
                default:
                    dbEnum = default;
                    return false;
            }
        }
    }

}
