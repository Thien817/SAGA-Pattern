using Microsoft.Data.SqlClient;

namespace CartService.Infrastructure;

public sealed class SqlConnectionFactory(string connectionString)
{
    public SqlConnection Create() => new(connectionString);
}
