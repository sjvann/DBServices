namespace DbServices.Core.Session
{
    public enum QueryOpcode
    {
        Auto = 0,
        Query = 1,
        Call = 2,
        Utility = 3
    }

    public enum QueryKind
    {
        Rows = 0,
        NonQuery = 1,
        Catalog = 2,
        Error = 3
    }

    public enum ResultFormat
    {
        Table = 0,
        Json = 1,
        Csv = 2
    }
}
