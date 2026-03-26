using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAccessLayer.Models
{
    public sealed record ProductDetailRecord(
      Guid ProductId,
      string ProductName,
      decimal Price,
      int Stock
  );
}
