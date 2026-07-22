using FirebirdSql.Data.FirebirdClient;
using System.Globalization;

namespace ClinicStatisticsApp.Integrations.Firebird;

/// <summary>Read-only reader for the CRM analytics warehouse.</summary>
public sealed class FirebirdAnalyticsReader(FirebirdClinicConnectionOptions options)
{
    public async Task<(IReadOnlyList<FirebirdPaymentRow> Payments, IReadOnlyList<FirebirdAppointmentRow> Appointments)> ReadAsync(DateTime from, DateTime to, CancellationToken token = default)
    {
        await using var connection = new FbConnection(new FbConnectionStringBuilder
        {
            DataSource = options.Server, Port = options.Port, Database = options.DatabasePath, UserID = options.User, Password = options.Password,
            Charset = options.Charset, Dialect = 3, Pooling = false, ConnectionTimeout = options.ConnectionTimeoutSeconds
        }.ToString());
        await connection.OpenAsync(token);
        var payments = new List<FirebirdPaymentRow>();
        await using (var command = new FbCommand("select ID_IND2, IDPAC_IND2, DATE_IND2, SUMMA_IND2, TEXT_IND2, NOMKASSA_IND2 from INDEXF_2 where DATE_IND2 between @from and @to", connection))
        {
            command.Parameters.AddWithValue("@from", from.Date); command.Parameters.AddWithValue("@to", to.Date);
            await using var reader = await command.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token)) payments.Add(new FirebirdPaymentRow(reader.GetInt64(0), reader.GetInt64(1), reader.GetDateTime(2), reader.IsDBNull(3) ? 0m : Convert.ToDecimal(reader.GetValue(3), CultureInfo.InvariantCulture), reader.IsDBNull(4) ? null : reader.GetString(4).Trim(), reader.IsDBNull(5) ? null : Convert.ToString(reader.GetValue(5))));
        }
        var appointments = new List<FirebirdAppointmentRow>();
        await using (var command = new FbCommand("select s.ID_SETKA, s.ID_PAC, s.DATEPR, trim(coalesce(d.FAM_SOTR, '')) || ' ' || trim(coalesce(d.NAME_SOTR, '')) || ' ' || trim(coalesce(d.OTCH_SOTR, '')), s.ID_KABINET, s.NOTCOMING, s.DOP_INFO from SETKA s left join SOTRUDNIKITEMP d on d.ID_SOTR = s.ID_DOC where s.DATEPR between @from and @to", connection))
        {
            command.Parameters.AddWithValue("@from", from.Date); command.Parameters.AddWithValue("@to", to.Date);
            await using var reader = await command.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token)) appointments.Add(new FirebirdAppointmentRow(reader.GetInt64(0), reader.GetInt64(1), reader.GetDateTime(2), reader.IsDBNull(3) ? null : reader.GetString(3).Trim(), reader.IsDBNull(4) ? null : Convert.ToString(reader.GetValue(4)), !reader.IsDBNull(5) && Convert.ToInt32(reader.GetValue(5)) != 0, reader.IsDBNull(6) ? null : Convert.ToString(reader.GetValue(6))?.Trim()));
        }
        return (payments, appointments);
    }
}

public sealed record FirebirdPaymentRow(long Id, long PatientId, DateTime Date, decimal Amount, string? Description, string? CashDesk);
public sealed record FirebirdAppointmentRow(long Id, long PatientId, DateTime Date, string? Doctor, string? Room, bool IsNoShow, string? Info);
