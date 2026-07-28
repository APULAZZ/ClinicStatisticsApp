using FirebirdSql.Data.FirebirdClient;
using System.Globalization;

namespace ClinicStatisticsApp.Integrations.Firebird;

/// <summary>Read-only reader for the CRM analytics warehouse.</summary>
public sealed class FirebirdAnalyticsReader(FirebirdClinicConnectionOptions options)
{
    public async Task<(IReadOnlyList<FirebirdPaymentRow> Payments, IReadOnlyList<FirebirdAppointmentRow> Appointments)> ReadAsync(DateTime from, DateTime to, CancellationToken token = default, bool includePayments = true)
    {
        await using var connection = new FbConnection(new FbConnectionStringBuilder
        {
            DataSource = options.Server, Port = options.Port, Database = options.DatabasePath, UserID = options.User, Password = options.Password,
            Charset = options.Charset, Dialect = 3, Pooling = false, ConnectionTimeout = options.ConnectionTimeoutSeconds
        }.ToString());
        await connection.OpenAsync(token);
        var payments = new List<FirebirdPaymentRow>();
        if (includePayments) await using (var command = new FbCommand("select ID_IND2, IDPAC_IND2, DATE_IND2, SUMMA_IND2, TEXT_IND2, NOMKASSA_IND2 from INDEXF_2 where DATE_IND2 between @from and @to", connection))
        {
            command.Parameters.AddWithValue("@from", from.Date); command.Parameters.AddWithValue("@to", to.Date);
            await using var reader = await command.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token)) payments.Add(new FirebirdPaymentRow(reader.GetInt64(0), reader.GetInt64(1), reader.GetDateTime(2), reader.IsDBNull(3) ? 0m : Convert.ToDecimal(reader.GetValue(3), CultureInfo.InvariantCulture), reader.IsDBNull(4) ? null : reader.GetString(4).Trim(), reader.IsDBNull(5) ? null : Convert.ToString(reader.GetValue(5))));
        }
        var appointments = new List<FirebirdAppointmentRow>();
        await using (var command = new FbCommand("select s.ID_SETKA, s.ID_PAC, s.DATEPR, s.TIMEPR, s.ID_DOC, trim(coalesce(d.FAM_SOTR, '')) || ' ' || trim(coalesce(d.NAME_SOTR, '')) || ' ' || trim(coalesce(d.OTCH_SOTR, '')), trim(coalesce(p.FAM, '')) || ' ' || trim(coalesce(p.IME, '')) || ' ' || trim(coalesce(p.OTCH, '')), s.TYP_NAZ, s.ID_KABINET, s.NOTCOMING, s.DOP_INFO, trim(coalesce(a.FAM_SOTR, '')) || ' ' || trim(coalesce(a.NAME_SOTR, '')) || ' ' || trim(coalesce(a.OTCH_SOTR, '')), p.PRICH_UHODA from SETKA s left join SOTRUDNIKITEMP d on d.ID_SOTR = s.ID_DOC left join PACIENT p on p.ID = s.ID_PAC left join SOTRUDNIKITEMP a on a.ID_SOTR = p.IDADM_CARD where s.DATEPR between @from and @to", connection))
        {
            command.Parameters.AddWithValue("@from", from.Date); command.Parameters.AddWithValue("@to", to.Date);
            await using var reader = await command.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token))
            {
                var date = reader.GetDateTime(2).Date;
                var time = reader.IsDBNull(3) ? TimeSpan.Zero : (TimeSpan)reader.GetValue(3);
                appointments.Add(new FirebirdAppointmentRow(reader.GetInt64(0), reader.GetInt64(1), date.Add(time), reader.IsDBNull(4) ? null : Convert.ToInt64(reader.GetValue(4)), reader.IsDBNull(5) ? null : reader.GetString(5).Trim(), reader.IsDBNull(6) ? null : reader.GetString(6).Trim(), reader.IsDBNull(7) ? null : Convert.ToString(reader.GetValue(7))?.Trim(), reader.IsDBNull(8) ? null : Convert.ToString(reader.GetValue(8)), !reader.IsDBNull(9) && IsNonZero(reader.GetValue(9)), false, reader.IsDBNull(10) ? null : Convert.ToString(reader.GetValue(10))?.Trim(), reader.IsDBNull(11) ? null : reader.GetString(11).Trim(), reader.IsDBNull(12) ? null : ReadOptionalInteger(reader.GetValue(12)), 30));
            }
        }
        // Cancelled schedule entries are retained by MedM in SETKA_LOG and no
        // longer exist in SETKA. Store them separately in CRM analytics so a
        // cancellation is never presented as a no-show.
        await using (var command = new FbCommand("select l.ID_SETKALOG, l.ID_PAC, l.DATEPR, l.ID_DOC, trim(coalesce(d.FAM_SOTR, '')) || ' ' || trim(coalesce(d.NAME_SOTR, '')) || ' ' || trim(coalesce(d.OTCH_SOTR, '')), trim(coalesce(p.FAM, '')) || ' ' || trim(coalesce(p.IME, '')) || ' ' || trim(coalesce(p.OTCH, '')), l.TYP_NAZ, l.ID_KABINET, l.DOP_INFO, trim(coalesce(a.FAM_SOTR, '')) || ' ' || trim(coalesce(a.NAME_SOTR, '')) || ' ' || trim(coalesce(a.OTCH_SOTR, '')), p.PRICH_UHODA from SETKA_LOG l left join SOTRUDNIKITEMP d on d.ID_SOTR = l.ID_DOC left join PACIENT p on p.ID = l.ID_PAC left join SOTRUDNIKITEMP a on a.ID_SOTR = p.IDADM_CARD where l.DATEPR between @from and @to and l.TYP_NAZ = 62", connection))
        {
            command.Parameters.AddWithValue("@from", from.Date); command.Parameters.AddWithValue("@to", to.Date);
            await using var reader = await command.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token)) appointments.Add(new FirebirdAppointmentRow(-reader.GetInt64(0), reader.GetInt64(1), reader.GetDateTime(2), reader.IsDBNull(3) ? null : Convert.ToInt64(reader.GetValue(3)), reader.IsDBNull(4) ? null : reader.GetString(4).Trim(), reader.IsDBNull(5) ? null : reader.GetString(5).Trim(), reader.IsDBNull(6) ? null : Convert.ToString(reader.GetValue(6))?.Trim(), reader.IsDBNull(7) ? null : Convert.ToString(reader.GetValue(7)), false, true, reader.IsDBNull(8) ? null : Convert.ToString(reader.GetValue(8))?.Trim(), reader.IsDBNull(9) ? null : reader.GetString(9).Trim(), reader.IsDBNull(10) ? null : ReadOptionalInteger(reader.GetValue(10)), 30));
        }
        return (payments, ConsolidateAdjacentSlots(appointments));
    }

    private static IReadOnlyList<FirebirdAppointmentRow> ConsolidateAdjacentSlots(IReadOnlyList<FirebirdAppointmentRow> appointments)
    {
        // Native MedM slots are safer than an unverified merge: reserve rows can
        // otherwise be accidentally glued to a patient's appointment.
        return appointments.Select(x => x with { DurationMinutes = 30 }).ToList();
    }

    private static bool IsNonZero(object value)
    {
        if (value is null or DBNull) return false;
        if (value is string text)
            return decimal.TryParse(text.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) && parsed != 0m;
        return Convert.ToDecimal(value, CultureInfo.InvariantCulture) != 0m;
    }

    private static int? ReadOptionalInteger(object value)
    {
        if (value is null or DBNull) return null;
        if (value is int number) return number;
        var text = Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim();
        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)) return parsed;
        // Several legacy sources contain decimal values here; they are not valid
        // departure-reason codes and must not abort analytics import.
        return null;
    }
}

public sealed record FirebirdPaymentRow(long Id, long PatientId, DateTime Date, decimal Amount, string? Description, string? CashDesk);
public sealed record FirebirdAppointmentRow(long Id, long PatientId, DateTime Date, long? DoctorId, string? Doctor, string? PatientName, string? AppointmentType, string? Room, bool IsNoShow, bool IsCancelled, string? Info, string? Administrator, int? DepartureReasonCode, int DurationMinutes);
