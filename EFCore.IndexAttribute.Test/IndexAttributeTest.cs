using System;
using System.Collections.Generic;
using EntityFrameworkCore.IndexAttributeTest.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Debug;
using Xunit;

namespace EntityFrameworkCore.IndexAttributeTest
{
    public class IndexAttributeTest
    {
        private static MyDbContextBase CreateMyDbContext(bool enableSqlServerFeature)
        {
            var dbName = Guid.NewGuid().ToString("N");

            using (var connToMaster = new SqlConnection("Server=(localdb)\\mssqllocaldb;Database=master;Trusted_Connection=True;"))
            using (var cmd = new SqlCommand($"CREATE DATABASE [{dbName}]", connToMaster))
            {
                connToMaster.Open();
                cmd.ExecuteNonQuery();
            }

            var loggerFactory = new LoggerFactory(
                new[] { new DebugLoggerProvider() },
                new LoggerFilterOptions
                {
                    Rules = { new LoggerFilterRule("Debug", DbLoggerCategory.Database.Command.Name, LogLevel.Information, (n, c, l) => true) }
                });

            var connStr = $"Server=(localdb)\\mssqllocaldb;Database={dbName};Trusted_Connection=True;MultipleActiveResultSets=True;";
            var option = new DbContextOptionsBuilder<MyDbContextBase>()
                .UseSqlServer(connStr)
                .UseLoggerFactory(loggerFactory)
                .Options;

            return enableSqlServerFeature ?
                new MyDbContextForSqlServer(option) as MyDbContextBase :
                new MyDbContext(option);
        }

        [Fact(DisplayName = "CreateDb with Indexes on MSSQL LocalDb")]
        public void CreateDb_with_Indexes_Test()
        {
            var dump = CreateDbAndDumpIndexes(enableSqlServerFeature: false);
            dump.Is(
                "People|IX_Country|Address_Country|False|NONCLUSTERED",
                "People|IX_Lines|Line1|False|NONCLUSTERED",
                "People|IX_Lines|Line2|False|NONCLUSTERED",
                "People|IX_People_FaxNumber_CountryCode|FaxNumber_CountryCode|False|NONCLUSTERED",
                "People|IX_People_Name|Name|True|NONCLUSTERED",
                "People|IX_People_PhoneNumber_CountryCode|PhoneNumber_CountryCode|False|NONCLUSTERED",
                "SNSAccounts|Ix_Provider_and_Account|Provider|True|NONCLUSTERED",
                "SNSAccounts|Ix_Provider_and_Account|AccountName|True|NONCLUSTERED",
                "SNSAccounts|IX_SNSAccounts_PersonId|PersonId|False|NONCLUSTERED",
                "SNSAccounts|IX_SNSAccounts_Provider|Provider|False|NONCLUSTERED"
            );
        }

        [Fact(DisplayName = "CreateDb with Indexes for SQL Server on MSSQL LocalDb")]
        public void CreateDb_with_IndexesForSqlServer_Test()
        {
            var dump = CreateDbAndDumpIndexes(enableSqlServerFeature: true);
            dump.Is(
                "People|IX_Country|Address_Country|False|NONCLUSTERED",
                "People|IX_Lines|Line1|False|NONCLUSTERED",
                "People|IX_Lines|Line2|False|NONCLUSTERED",
                "People|IX_People_FaxNumber_CountryCode|FaxNumber_CountryCode|False|NONCLUSTERED",
                "People|IX_People_Name|Name|True|NONCLUSTERED",
                "People|IX_People_PhoneNumber_CountryCode|PhoneNumber_CountryCode|False|NONCLUSTERED",
                "SNSAccounts|Ix_Provider_and_Account|Provider|True|CLUSTERED",
                "SNSAccounts|Ix_Provider_and_Account|AccountName|True|CLUSTERED",
                "SNSAccounts|IX_SNSAccounts_PersonId|PersonId|False|NONCLUSTERED",
                "SNSAccounts|IX_SNSAccounts_Provider|Provider|False|NONCLUSTERED"
            );
        }

        private IEnumerable<string> CreateDbAndDumpIndexes(bool enableSqlServerFeature)
        {
            using (var db = CreateMyDbContext(enableSqlServerFeature))
            {
                // Create database.
                db.Database.OpenConnection();
                db.Database.EnsureCreated();

                try
                {
                    // Validate database indexes.
                    var conn = db.Database.GetDbConnection() as SqlConnection;
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                        SELECT [Table] = t.name, [Index] = ind.name, [Column] = col.name, [IsUnique] = ind.is_unique, [Type] = ind.type_desc
                        FROM sys.indexes ind 
                        INNER JOIN sys.index_columns ic ON  ind.object_id = ic.object_id and ind.index_id = ic.index_id 
                        INNER JOIN sys.columns col ON ic.object_id = col.object_id and ic.column_id = col.column_id 
                        INNER JOIN sys.tables t ON ind.object_id = t.object_id 
                        WHERE ind.is_primary_key = 0 AND t.is_ms_shipped = 0 
                        ORDER BY t.name, ind.name, ind.index_id, ic.key_ordinal;";
                        var dump = new List<string>();
                        var r = cmd.ExecuteReader();
                        try { while (r.Read()) dump.Add($"{r["Table"]}|{r["Index"]}|{r["Column"]}|{r["IsUnique"]}|{r["Type"]}"); }
                        finally { r.Close(); }
                        return dump;
                    }
                }
                finally { db.Database.EnsureDeleted(); }
            }
        }
    }
}
