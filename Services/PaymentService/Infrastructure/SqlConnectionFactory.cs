using Microsoft.Data.SqlClient;

namespace PaymentService.Infrastructure;

public sealed class SqlConnectionFactory(string connectionString)
{
    public SqlConnection Create() => new(connectionString);
}