using System.Data;
using System.Data.Common;
using System.Threading.Tasks;

namespace DataAccessor.Library.Core;

public interface IDbConnectionFactory
{
    DbConnection CreateConnection();

    Task<DbConnection> CreateConnectionAsync();

    IDbTransaction BeginTransaction(IDbConnection connection);

    Task<IDbTransaction> BeginTransactionAsync(DbConnection connection);
}
