using System.Data;
using System.Data.Common;
using System.Threading.Tasks;

namespace DataAccessor.Library.Core;

public interface IMultiServerConnectionFactory
{
    Task<DbConnection> CreateConnectionAsync(string connectionName);
    DbConnection CreateConnection(string connectionName);
    Task<IDbTransaction> BeginTransactionAsync(DbConnection connection);
}
