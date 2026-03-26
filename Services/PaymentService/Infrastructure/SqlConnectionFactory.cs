using System.Data;
using System.Data.SqlClient;

namespace PaymentService.Infrastructure
{
    public class SqlConnectionFactory
    {
        private readonly string _connectionString;

        public SqlConnectionFactory(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        public IDbConnection Create()
        {
            return new SqlConnection(_connectionString);
        }
    }
}