using Microsoft.Data.SqlClient;

namespace InventoryService.Infrastructure;

public sealed class SqlConnectionFactory(string connectionString)
{
    public SqlConnection Create() => new(connectionString);
}
