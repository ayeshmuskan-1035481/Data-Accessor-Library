namespace DataAccessor.Library.Core;

public class MultiDatabaseDataAccess : MultiServerDataAccess
{
    public MultiDatabaseDataAccess(IMultiServerConnectionFactory connectionFactory)
        : base(connectionFactory)
    {
    }

    public MultiDatabaseDataAccess(IMultiServerConnectionFactory connectionFactory, DataAccessOptions? options)
        : base(connectionFactory, options)
    {
    }
}
