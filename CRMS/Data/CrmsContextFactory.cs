using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System;
using System.Configuration;
using System.IO;
using System.Xml.Linq;

namespace CRMS.Data
{
    public class CrmsContextFactory : IDesignTimeDbContextFactory<CrmsContext>
    {
        public CrmsContext CreateDbContext(string[] args)
        {
            string? connectionString =
                ConfigurationManager.ConnectionStrings["CRMS"]
                    ?.ConnectionString;

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                string[] possiblePaths =
                {
                    Path.Combine(
                        AppContext.BaseDirectory, "CRMS.dll.config"),

                    Path.Combine(
                        AppContext.BaseDirectory, "CRMS.exe.config"),

                    Path.GetFullPath(
                        Path.Combine(
                            AppContext.BaseDirectory,
                            "..", "..", "..", "App.config"))
                };

                foreach (string path in possiblePaths)
                {
                    if (!File.Exists(path))
                        continue;

                    var document = XDocument.Load(path);

                    connectionString = document
                        .Root?
                        .Element("connectionStrings")?
                        .Elements("add")
                        .FirstOrDefault(x =>
                            (string?)x.Attribute("name") == "CRMS")
                        ?.Attribute("connectionString")
                        ?.Value;

                    if (!string.IsNullOrWhiteSpace(connectionString))
                        break;
                }
            }

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "Không tìm thấy connection string CRMS. "
                    + "Hãy kiểm tra App.config và cấu hình build.");
            }

            var options =
                new DbContextOptionsBuilder<CrmsContext>()
                    .UseSqlServer(connectionString)
                    .Options;

            return new CrmsContext(options);
        }

        public static CrmsContext Create()
        {
            return new CrmsContextFactory().CreateDbContext(Array.Empty<string>());
        }
    }
}