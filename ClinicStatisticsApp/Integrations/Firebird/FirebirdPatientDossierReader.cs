using FirebirdSql.Data.FirebirdClient;

namespace ClinicStatisticsApp.Integrations.Firebird;

/// <summary>Strictly read-only detailed view of one Firebird patient card.</summary>
public sealed class FirebirdPatientDossierReader(FirebirdClinicConnectionOptions options)
{
    public async Task<FirebirdPatientDossier> ReadAsync(long patientId, CancellationToken token = default)
    {
        await using var connection = new FbConnection(BuildConnectionString());
        await connection.OpenAsync(token);
        var profile = await ReadRowsAsync(connection, """select ID, NUM_CARD, FAM, IME, OTCH, DATE_BORN, POL, TEL_R, TEL_D, TEL_MOB, EMAIL, ADRESS, GOROD, STREET, DOM, KORP, FLAT, POSTINDEX, PLACE_WORK, DOLZNOST, EDUCATION, NUM_POLICE, DATE_POLICE, SKIDKA, BONUS, MAIN_DIAGNOZ from PACIENT where ID = @P_ID""", patientId, token);
        var visits = await ReadRowsAsync(connection, """select ID_IND, DATE_IND, TIME_IND, SUM_IND, NC_IND, INF_IND, MODE_IND, TYPE_IND from INDEXF where IDPAC_IND = @P_ID order by DATE_IND desc, TIME_IND desc""", patientId, token);
        var works = await ReadRowsAsync(connection, """select ID_WORK, DATEWORK, COMMENT, PRICE, PRICE_END, SKIDKA, KOLVO, SUMMAP, SUMMAK, NZUB_WP, TYPEPROV_WP from WORKPACIENT where NUM_PACIENT = @P_ID order by DATEWORK desc, ID_WORK desc""", patientId, token);
        var services = await ReadRowsAsync(connection, """select i.DATE_IND, x.TEXT_IND1, x.CENA_IND1, x.KOL_IND1, x.SUMMA_IND1 from INDEXF i join INDEXF_1 x on x.IDIND2_IND1 = i.NC_IND where i.IDPAC_IND = @P_ID order by i.DATE_IND desc, x.ID_IND1""", patientId, token);
        var payments = await ReadRowsAsync(connection, """select DATE_IND2, TIME_IND2, SUMMA_IND2, TEXT_IND2, NOMKASSA_IND2, NOMER_CHECK_IN_KASSA_IND2 from INDEXF_2 where IDPAC_IND2 = @P_ID order by DATE_IND2 desc, TIME_IND2 desc""", patientId, token);
        var appointments = await ReadRowsAsync(connection, """select DATEPR, TIMEPR, ID_DOC, ID_KABINET, NOTCOMING, DOP_INFO from SETKA where ID_PAC = @P_ID order by DATEPR desc, TIMEPR desc""", patientId, token);
        return new FirebirdPatientDossier(profile.FirstOrDefault(), visits, works, services, payments, appointments);
    }

    private async Task<List<Dictionary<string, string>>> ReadRowsAsync(FbConnection connection, string sql, long patientId, CancellationToken token)
    {
        var result = new List<Dictionary<string, string>>();
        await using var command = new FbCommand(sql, connection); command.Parameters.AddWithValue("@P_ID", patientId);
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token))
        {
            var row = new Dictionary<string, string>();
            for (var i = 0; i < reader.FieldCount; i++) row[reader.GetName(i)] = reader.IsDBNull(i) ? "" : Convert.ToString(reader.GetValue(i))?.Trim() ?? "";
            result.Add(row);
        }
        return result;
    }

    private string BuildConnectionString() => new FbConnectionStringBuilder { DataSource = options.Server, Port = options.Port, Database = options.DatabasePath, UserID = options.User, Password = options.Password, Charset = options.Charset, Dialect = 3, Pooling = false, ConnectionTimeout = options.ConnectionTimeoutSeconds }.ToString();
}

public sealed record FirebirdPatientDossier(Dictionary<string, string>? Profile, IReadOnlyList<Dictionary<string, string>> Visits, IReadOnlyList<Dictionary<string, string>> Works, IReadOnlyList<Dictionary<string, string>> Services, IReadOnlyList<Dictionary<string, string>> Payments, IReadOnlyList<Dictionary<string, string>> Appointments);
