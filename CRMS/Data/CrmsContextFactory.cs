using System.Linq;
using Microsoft.EntityFrameworkCore;
using System;
using System.Configuration;
using System.IO;
using System.Xml.Linq;


namespace CRMS.Data
{
    public static class CrmsContextFactory
    {
        public static CrmsContext Create()
        {
            string? connectionString =
                ConfigurationManager.ConnectionStrings["CRMS"]
                    ?.ConnectionString;

            // Doc App.config tu thu muc chua project khi
            // ConfigurationManager khong tim thay cau hinh.
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
    }
}
