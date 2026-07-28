using FirebirdSql.Data.FirebirdClient;

namespace ClinicStatisticsApp.Integrations.Firebird;

/// <summary>Read-only reader for the MedM «Clients / statistics» visit counters.</summary>
public sealed class FirebirdVisitFunnelReader(FirebirdClinicConnectionOptions options)
{
    public async Task<IReadOnlyList<FirebirdVisitFunnelRow>> ReadAsync(DateTime from, DateTime to, CancellationToken token = default)
    {
        await using var connection = new FbConnection(new FbConnectionStringBuilder
        {
            DataSource = options.Server, Port = options.Port, Database = options.DatabasePath,
            UserID = options.User, Password = options.Password, Charset = options.Charset,
            Dialect = 3, Pooling = false, ConnectionTimeout = options.ConnectionTimeoutSeconds
        }.ToString());
        await connection.OpenAsync(token);

        var result = new List<FirebirdVisitFunnelRow>();
        // MedM's «В услугах» statistics counts one visit per patient and work date,
        // not each service line or an INDEXF document. This exactly mirrors the
        // «Цифры» popup in the Clients / specialist statistics screen.
        await using var command = new FbCommand("select NUM_PACIENT, DATEWORK from WORKPACIENT where DATEWORK between @from and @to and NUM_PACIENT <> 0 group by NUM_PACIENT, DATEWORK", connection);
        command.Parameters.AddWithValue("@from", from.Date);
        command.Parameters.AddWithValue("@to", to.Date);
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token))
        {
            var patientId = reader.GetInt64(0);
            var date = reader.GetDateTime(1).Date;
            // ID_WORK is not consistently numeric across legacy Firebird bases.
            // A patient may have only one visit per calendar day in this report,
            // so this deterministic key is unique and stable for the SQL snapshot.
            var stableVisitId = checked(patientId * 1_000_000L + date.Year * 366L + date.DayOfYear);
            result.Add(new FirebirdVisitFunnelRow(stableVisitId, patientId, date));
        }
        return result;
    }
}

public sealed record FirebirdVisitFunnelRow(long Id, long PatientId, DateTime Date);
