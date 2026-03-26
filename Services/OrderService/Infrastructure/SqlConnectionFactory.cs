using Microsoft.Data.SqlClient;

namespace OrderService.Infrastructure;

public sealed class SqlConnectionFactory(string connectionString)
{
    public SqlConnection Create() => new(connectionString);
}
