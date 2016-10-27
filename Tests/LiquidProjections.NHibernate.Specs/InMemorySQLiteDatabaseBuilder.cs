using System;
using System.Configuration;
using System.Data.SQLite;

using FluentNHibernate.Cfg;
using FluentNHibernate.Cfg.Db;

using NHibernate;
using NHibernate.Tool.hbm2ddl;

namespace LiquidProjections.NHibernate.Specs
{
    internal sealed class InMemorySQLiteDatabaseBuilder
    {
        public InMemorySQLiteDatabase Build()
        {
            Guid databaseId = Guid.NewGuid();

            var connectionStringSettings = new ConnectionStringSettings(
                "in-memory", $"FullUri=file:{databaseId}.db?mode=memory&cache=shared", "sqlite");

            var connection = new SQLiteConnection(connectionStringSettings.ConnectionString);
            connection.Open();

            ISessionFactory sessionFactory = Fluently.Configure()
                .Database(SQLiteConfiguration.Standard.InMemory().ConnectionString(connectionStringSettings.ConnectionString))
                .Mappings(configuration => configuration.FluentMappings.AddFromAssemblyOf<InMemorySQLiteDatabaseBuilder>())
                .ExposeConfiguration(configuration => new SchemaExport(configuration)
                    .Execute(useStdOut: true, execute: true, justDrop: false))
                .BuildSessionFactory();

            return new InMemorySQLiteDatabase(connection, sessionFactory);
        }
    }

    internal sealed class InMemorySQLiteDatabase : IDisposable
    {
        private readonly SQLiteConnection connection;

        public InMemorySQLiteDatabase(SQLiteConnection connection, ISessionFactory sessionFactory)
        {
            this.connection = connection;
            SessionFactory = sessionFactory;
        }

        public ISessionFactory SessionFactory { get; }

        public void Dispose()
        {
            connection?.Dispose();
        }
    }
}