using Microsoft.Data.SqlClient;

namespace AuthService.Infrastructure;

public sealed class SqlConnectionFactory(string connectionString)
{
    public SqlConnection Create() => new(connectionString);
}
