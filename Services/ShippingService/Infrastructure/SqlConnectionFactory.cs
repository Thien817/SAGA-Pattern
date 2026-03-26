using Microsoft.Data.SqlClient;

namespace ShippingService.Infrastructure;

public sealed class SqlConnectionFactory(string connectionString)
{
    public SqlConnection Create() => new(connectionString);
}
