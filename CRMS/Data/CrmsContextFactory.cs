using Microsoft.EntityFrameworkCore;
using System.Configuration;

namespace CRMS.Data
{
    public static class CrmsContextFactory
    {
        public static CrmsContext Create()
        {
            string connectionString =
                ConfigurationManager.ConnectionStrings["CRMS"]
                    ?.ConnectionString
                ?? throw new InvalidOperationException(
                    "Connection string CRMS not found.");

            var options = new DbContextOptionsBuilder<CrmsContext>()
                .UseSqlServer(connectionString)
                .Options;

            return new CrmsContext(options);
        }
    }
}