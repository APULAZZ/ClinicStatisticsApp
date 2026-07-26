using FirebirdSql.Data.FirebirdClient;

namespace ClinicStatisticsApp.Integrations.Firebird;

/// <summary>
/// Read-only access to the patient directory. No INSERT, UPDATE or DELETE is used.
/// </summary>
public sealed class FirebirdPatientReader
{
    private readonly FirebirdClinicConnectionOptions _options;

    public FirebirdPatientReader(FirebirdClinicConnectionOptions options)
    {
        _options = options;
    }

    public async Task<IReadOnlyList<FirebirdPatientSnapshot>> ReadPatientsAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            select
                ID,
                NUM_CARD,
                FAM,
                IME,
                OTCH,
                DATE_BORN,
                TEL_MOB,
                TEL_R,
                TEL_D,
                EMAIL
            from PACIENT
            order by ID
            """;

        var result = new List<FirebirdPatientSnapshot>();
        await using var connection = new FbConnection(BuildConnectionString());
        await connection.OpenAsync(cancellationToken);
        await using var command = new FbCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new FirebirdPatientSnapshot
            {
                SourcePatientId = Convert.ToInt64(reader["ID"]),
                SourceCardNumber = ReadString(reader, "NUM_CARD"),
                LastName = ReadString(reader, "FAM") ?? string.Empty,
                FirstName = ReadString(reader, "IME") ?? string.Empty,
                MiddleName = ReadString(reader, "OTCH"),
                DateOfBirth = ReadDate(reader, "DATE_BORN"),
                MobilePhone = ReadString(reader, "TEL_MOB"),
                WorkPhone = ReadString(reader, "TEL_R"),
                HomePhone = ReadString(reader, "TEL_D"),
                Email = ReadString(reader, "EMAIL")
            });
        }

        return result;
    }

    private string BuildConnectionString()
    {
        var builder = new FbConnectionStringBuilder
        {
            DataSource = _options.Server,
            Port = _options.Port,
            Database = _options.DatabasePath,
            UserID = _options.User,
            Password = _options.Password,
            // Older MedM Firebird installations used by the clinics do not
            // expose the legacy WIN1251 alias to the client.  NONE is accepted
            // by them and is sufficient for the read-only directory copy.
            Charset = "NONE",
            Dialect = 3,
            Pooling = false,
            ConnectionTimeout = _options.ConnectionTimeoutSeconds
        };

        return builder.ToString();
    }

    private static string? ReadString(FbDataReader reader, string columnName)
    {
        var value = reader[columnName];
        return value is DBNull ? null : Convert.ToString(value)?.Trim();
    }

    private static DateTime? ReadDate(FbDataReader reader, string columnName)
    {
        var value = reader[columnName];
        return value is DBNull ? null : Convert.ToDateTime(value);
    }
}
