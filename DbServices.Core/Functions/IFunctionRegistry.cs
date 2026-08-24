using DbServices.Core.Session;

namespace DbServices.Core.Functions
{
    public delegate QueryResult DbFunctionHandler(IReadOnlyDictionary<string, object?> arguments);

    public sealed class DbFunctionDescriptor
    {
        public required string Name { get; init; }
        public required string Description { get; init; }
        public required DbFunctionHandler Handler { get; init; }
    }

    public interface IFunctionRegistry
    {
        void Register(DbFunctionDescriptor descriptor);
        bool TryGet(string name, out DbFunctionDescriptor? descriptor);
        IReadOnlyList<DbFunctionDescriptor> List();
    }
}
