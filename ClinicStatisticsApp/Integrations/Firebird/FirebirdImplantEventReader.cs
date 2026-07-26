using FirebirdSql.Data.FirebirdClient;

namespace ClinicStatisticsApp.Integrations.Firebird;

/// <summary>Reads MedM appointment-creation events without changing Firebird.</summary>
public sealed class FirebirdImplantEventReader(FirebirdClinicConnectionOptions options)
{
    public async Task<IReadOnlyList<FirebirdImplantEventRow>> ReadAppointmentEventsAsync(DateTime from, DateTime to, CancellationToken token = default)
    {
        await using var connection = new FbConnection(new FbConnectionStringBuilder
        {
            DataSource = options.Server, Port = options.Port, Database = options.DatabasePath, UserID = options.User, Password = options.Password,
            // Event IDs, dates and type codes are sufficient for matching. NONE is
            // accepted by older MedM Firebird installations even when their legacy
            // WIN1251 alias is not exposed to the client.
            Charset = "NONE", Dialect = 3, Pooling = false, ConnectionTimeout = options.ConnectionTimeoutSeconds
        }.ToString());
        await connection.OpenAsync(token);
        const string sql = """
select e.ID_LE, e.IDPAC_LE, e.DATE_LE, e.TIME_LE, e.IDSOTRADM_LE,
 trim(coalesce(s.FAM_SOTR, '')) || ' ' || trim(coalesce(s.NAME_SOTR, '')) || ' ' || trim(coalesce(s.OTCH_SOTR, '')),
 e.COMPNAME_LE, e.TYPE_LE, trim(coalesce(t.TEXT_TLE, '')), trim(coalesce(e.TEXT_LE, ''))
from LOG_EVENTS e
left join SOTRUDNIKITEMP s on s.ID_SOTR = e.IDSOTRADM_LE
left join TYPE_LOGEVENTS t on t.ID_TLE = e.TYPE_LE
where e.DATE_LE between @from and @to and e.TYPE_LE in (12, 22)
order by e.DATE_LE, e.TIME_LE, e.ID_LE
""";
        var result = new List<FirebirdImplantEventRow>();
        await using var command = new FbCommand(sql, connection);
        command.Parameters.AddWithValue("@from", from.Date);
        command.Parameters.AddWithValue("@to", to.Date);
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token))
        {
            var date = reader.GetDateTime(2).Date;
            var time = reader.IsDBNull(3) ? TimeSpan.Zero : (TimeSpan)reader.GetValue(3);
            result.Add(new FirebirdImplantEventRow(
                reader.GetInt64(0), reader.GetInt64(1), date.Add(time),
                reader.IsDBNull(4) ? null : Convert.ToInt64(reader.GetValue(4)),
                reader.IsDBNull(5) ? null : reader.GetString(5).Trim(),
                reader.IsDBNull(6) ? null : reader.GetString(6).Trim(),
                reader.IsDBNull(7) ? null : Convert.ToInt32(reader.GetValue(7)),
                reader.IsDBNull(8) ? null : reader.GetString(8).Trim(),
                reader.IsDBNull(9) ? null : reader.GetString(9).Trim()));
        }
        return result;
    }
}

public sealed record FirebirdImplantEventRow(long SourceEventId, long SourcePatientId, DateTime OccurredAt, long? MedmUserId, string? MedmUserName, string? ComputerName, int? EventTypeCode, string? EventTypeName, string? EventText);
