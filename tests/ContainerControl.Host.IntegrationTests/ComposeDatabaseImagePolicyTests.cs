using ContainerControl.Modules.Delivery.Compose;

namespace ContainerControl.Host.IntegrationTests;

public class ComposeDatabaseImagePolicyTests
{
    public static TheoryData<string> RejectedImages => new()
    {
        "postgres:17-alpine",
        "postgres:16@sha256:abc123",
        " postgres:16 ",
        "library/postgres:16",
        "localhost/postgres:16",
        "localhost:5000/bitnami/postgresql:16",
        "Bitnami/PostgreSQL:16",
        "bitnami/postgresql-repmgr:16",
        "bitnamilegacy/postgresql:16",
        "postgis/postgis:16-3.4",
        "pgvector/pgvector:pg16",
        "tensorchord/pgvecto-rs:latest",
        "tensorchord/pgvecto_rs:latest",
        "timescale/timescaledb:latest-pg16",
        "timescale/timescaledb-ha:pg16",
        "citusdata/citus:12.1",
        "ghcr.io/cloudnative-pg/postgresql:16",
        "crunchydata/crunchy-postgres:ubi8-16",
        "mysql:8.4",
        "mysql/mysql-server:8.0",
        "bitnami/mariadb:11",
        "bitnami/mariadb-galera:11",
        "percona/percona-server:8.0",
        "percona/percona-xtradb-cluster:8.0",
        "mongo:7",
        "mongodb/mongodb-community-server:7.0",
        "bitnami/mongodb:7",
        "mongo-express:1",
        "mcr.microsoft.com/mssql/server:2022-latest",
        "mcr.microsoft.com/azure-sql-edge:1.0.7",
        "microsoft/mssql-server-linux:2019-latest",
        "mycompany/sql-server:2019",
        "gvenzl/oracle-xe:21-slim",
        "gvenzl/oracle-free:23-slim",
        "container-registry.oracle.com/database/express:21.3.0-xe",
        "container-registry.oracle.com/database/enterprise:21.3.0.0",
        "container-registry.oracle.com/database/free:latest",
        "container-registry.oracle.com:443/database/standard:latest",
        "store/oracle/database-enterprise:12.2.0.1",
        "cassandra:5",
        "scylladb/scylla:6.2",
        "cockroachdb/cockroach:v24.1.0",
        "cockroachdb/cockroachdb:latest",
        "clickhouse/clickhouse-server:24.8",
        "docker.elastic.co/elasticsearch/elasticsearch:8.15.0",
        "opensearchproject/opensearch:2.16.0",
        "opensearchproject/opensearch-dashboards:2.16.0",
        "influxdb:2.7",
        "neo4j:5",
        "couchdb:3.3",
        "apache/couchdb:3.3",
        "couchbase/server:7.6.3",
        "quay.io/prometheuscommunity/postgres-exporter:v0.15.0"
    };

    public static TheoryData<string> AllowedImages => new()
    {
        "redis:7-alpine",
        "bitnami/redis:7.4",
        "redis/redis-stack-server:7.4.0",
        "valkey/valkey:8",
        "bitnami/valkey:8",
        "memcached:1.6-alpine",
        "bitnami/memcached:1",
        "nginx:1.27",
        "busybox:1.36.1",
        "mcr.microsoft.com/dotnet/aspnet:10.0",
        "traefik:v3.1",
        "rabbitmq:3.13",
        "postgrest/postgrest:v12.2.3",
        "phpmyadmin:5",
        "dpage/pgadmin4:8",
        "adminer:4",
        "mongosh:2",
        "edoburu/pgbouncer:1.23.1",
        "oraclelinux:9",
        "container-registry.oracle.com/os/oraclelinux:9",
        "oracle/instantclient:21",
        "container-registry.oracle.com/database/instantclient:21",
        "ghcr.io/cloudnative-pg/cloudnative-pg:1.24.0",
        "docker.elastic.co/kibana/kibana:8.15.0",
        "influxdata/influx-cli:2.7.5",
        "gcr.io/cloud-sql-connectors/cloud-sql-proxy:2",
        "localhost:5000/team/orders-api:1.2.3",
        "myregistry.example.com:5000/web:2"
    };

    [Theory]
    [MemberData(nameof(RejectedImages))]
    public void Database_images_are_rejected_on_single_image_and_compose(string image)
    {
        Assert.True(ComposePolicy.IsDatabaseImage(image));

        var single = ComposePolicy.FromImage(image, null, false, null);
        Assert.False(single.Accepted);
        Assert.Empty(single.Services);
        Assert.Contains($"Image '{image}' is a database image. Use the data tier.", single.Errors);

        var compose = ComposePolicy.Parse(ServiceYaml("db", image));
        Assert.False(compose.Accepted);
        Assert.Empty(compose.Services);
        Assert.Contains($"Service 'db' uses database image '{image}'. Use the data tier.", compose.Errors);
    }

    [Theory]
    [MemberData(nameof(AllowedImages))]
    public void Caches_and_app_images_stay_allowed_on_single_image_and_compose(string image)
    {
        Assert.False(ComposePolicy.IsDatabaseImage(image));

        var single = ComposePolicy.FromImage(image, null, false, null);
        Assert.True(single.Accepted);
        Assert.Empty(single.Errors);
        var planned = Assert.Single(single.Services);
        Assert.Equal("app", planned.Name);
        Assert.Equal(image, planned.Image);

        var compose = ComposePolicy.Parse(ServiceYaml("app", image));
        Assert.True(compose.Accepted);
        Assert.Empty(compose.Errors);
        var service = Assert.Single(compose.Services);
        Assert.Equal("app", service.Name);
        Assert.Equal(image, service.Image);
    }

    [Fact]
    public void Compose_allows_an_app_beside_redis_valkey_and_memcached()
    {
        var plan = ComposePolicy.Parse("""
            services:
              web:
                image: nginx:1.27
              cache:
                image: redis:7-alpine
              kv:
                image: valkey/valkey:8
              sessions:
                image: memcached:1.6-alpine
            """);

        Assert.True(plan.Accepted);
        Assert.Equal(["web", "cache", "kv", "sessions"], plan.Services.Select(service => service.Name).ToArray());
    }

    [Fact]
    public void Compose_rejects_when_any_service_is_a_database()
    {
        var plan = ComposePolicy.Parse("""
            services:
              cache:
                image: redis:7-alpine
              db:
                image: bitnami/postgresql:16
            """);

        Assert.False(plan.Accepted);
        Assert.Empty(plan.Services);
        Assert.Contains("Service 'db' uses database image 'bitnami/postgresql:16'. Use the data tier.", plan.Errors);
        Assert.DoesNotContain(plan.Errors, error => error.Contains("redis", StringComparison.OrdinalIgnoreCase));
    }

    private static string ServiceYaml(string name, string image) =>
        $"services:\n  {name}:\n    image: \"{image}\"\n";
}
