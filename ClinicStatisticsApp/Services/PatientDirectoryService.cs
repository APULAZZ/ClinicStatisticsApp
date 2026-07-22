using ClinicStatisticsApp.CallCenter.Models;
using ClinicStatisticsApp.Data;
using ClinicStatisticsApp.Models;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace ClinicStatisticsApp.Services;

public sealed class PatientDirectoryService
{
    private readonly AppDbContext _db;

    public PatientDirectoryService(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<PatientSearchRow>> SearchAsync(string? query, int? branchId, CancellationToken token = default)
    {
        var cards = _db.ExternalPatientCards.AsNoTracking().Include(x => x.Branch).Include(x => x.ClinicDataSource).Include(x => x.CrmPerson).AsQueryable();
        // Some legacy cards have no readable FIO. They remain in the raw source
        // projection but are not useful as the default CRM directory listing.
        cards = cards.Where(x => x.LastName != "" && x.FirstName != "");
        if (branchId is not null) cards = cards.Where(x => x.BranchId == branchId);
        var value = query?.Trim();
        if (!string.IsNullOrWhiteSpace(value))
        {
            var normalizedPhone = ExternalPatientSynchronizationService.NormalizePhone(value);
            var normalizedEmail = ExternalPatientSynchronizationService.NormalizeEmail(value);
            if (normalizedPhone is not null || normalizedEmail is not null || value.All(char.IsDigit))
                cards = cards.Where(x => (normalizedPhone != null && x.NormalizedMobilePhone == normalizedPhone) ||
                    (normalizedEmail != null && x.NormalizedEmail == normalizedEmail) ||
                    (x.SourceCardNumber != null && x.SourceCardNumber.Contains(value)));
            else
            {
                foreach (var word in value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    var term = word;
                    cards = cards.Where(x => x.LastName.Contains(term) || x.FirstName.Contains(term) || (x.MiddleName != null && x.MiddleName.Contains(term)));
                }
            }
        }

        var rows = await cards
            .OrderBy(x => x.CrmPersonId != null && x.SourceCardNumber == x.CrmPerson!.PrimaryCardNumber ? 0 : 1)
            .ThenBy(x => x.LastName).ThenBy(x => x.FirstName).Take(2000)
            .Select(x => new PatientSearchRow(x.Id, x.CrmPersonId, x.LastName, x.FirstName, x.MiddleName,
                x.DateOfBirth, x.MobilePhone, x.Email,
                x.CrmPersonId != null ? x.CrmPerson!.PrimaryCardNumber : x.SourceCardNumber,
                x.Branch!.Name, x.ClinicDataSource!.Name))
            .ToListAsync(token);
        // One CRM identity must be represented by one directory row, while raw
        // branch cards without a CRM identity remain separate rows.
        return rows.Where(x => x.FullName.Any(char.IsLetter))
            .GroupBy(x => x.CrmPersonId is int personId ? $"crm:{personId}" : $"card:{x.CardId}")
            .Select(group =>
            {
                var first = group.First();
                return group.Key.StartsWith("crm:", StringComparison.Ordinal)
                    ? first with { BranchName = $"{group.Count()} карт(ы)", SourceName = string.Join(", ", group.Select(x => x.BranchName).Distinct()) }
                    : first;
            })
            .Take(500).ToList();
    }

    public async Task<PatientCardDetails?> GetCardAsync(int cardId, CancellationToken token = default)
    {
        var card = (await ReadCardsAsync("p.[Id] = @value", cardId, token)).SingleOrDefault();
        if (card is null) return null;
        IReadOnlyList<ExternalPatientCard> linkedCards = card.CrmPersonId is null ? [card] : await ReadCardsAsync("p.[CrmPersonId] = @value", card.CrmPersonId.Value, token);
        var candidates = await _db.PatientMatchCandidates.AsNoTracking().Include(x => x.ProposedCrmPerson)
            .Where(x => x.ExternalPatientCardId == cardId && x.Status == "Pending").OrderByDescending(x => x.ConfidenceScore).ToListAsync(token);
        var tasks = card.CrmPersonId is null ? [] : await _db.WorkTasks.AsNoTracking().Where(x => x.CrmPersonId == card.CrmPersonId)
            .OrderByDescending(x => x.CreatedAt).Take(20).ToListAsync(token);
        var activity = card.CrmPersonId is null ? [] : await _db.CrmActivityLinks.AsNoTracking().Where(x => x.CrmPersonId == card.CrmPersonId)
            .OrderByDescending(x => x.OccurredAt).Take(30).ToListAsync(token);
        return new PatientCardDetails(card, linkedCards, candidates, tasks, activity);
    }

    private async Task<List<ExternalPatientCard>> ReadCardsAsync(string filter, int value, CancellationToken token)
    {
        var connection = _db.Database.GetDbConnection();
        var closeConnection = connection.State != ConnectionState.Open;
        if (closeConnection) await connection.OpenAsync(token);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"SELECT p.[Id], p.[BranchId], p.[ClinicDataSourceId], p.[CrmPersonId], p.[SourcePatientId], p.[SourceCardNumber], p.[LastName], p.[FirstName], p.[MiddleName], p.[DateOfBirth], p.[MobilePhone], p.[NormalizedMobilePhone], p.[Email], p.[NormalizedEmail], p.[IsActive], p.[LastSyncedAt], b.[Name], s.[Name] FROM [dbo].[ExternalPatientCards] p INNER JOIN [dbo].[Branches] b ON b.[Id] = p.[BranchId] LEFT JOIN [dbo].[ClinicDataSources] s ON s.[Id] = p.[ClinicDataSourceId] WHERE {filter} ORDER BY b.[Name], p.[LastName], p.[FirstName]";
            var parameter = command.CreateParameter(); parameter.ParameterName = "@value"; parameter.Value = value; command.Parameters.Add(parameter);
            var result = new List<ExternalPatientCard>();
            await using var reader = await command.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token))
            {
                result.Add(new ExternalPatientCard
                {
                    Id = reader.GetInt32(0), BranchId = reader.GetInt32(1), ClinicDataSourceId = reader.IsDBNull(2) ? null : reader.GetInt32(2), CrmPersonId = reader.IsDBNull(3) ? null : reader.GetInt32(3), SourcePatientId = reader.GetInt64(4), SourceCardNumber = reader.IsDBNull(5) ? null : reader.GetString(5), LastName = reader.GetString(6), FirstName = reader.GetString(7), MiddleName = reader.IsDBNull(8) ? null : reader.GetString(8), DateOfBirth = reader.IsDBNull(9) ? null : reader.GetDateTime(9), MobilePhone = reader.IsDBNull(10) ? null : reader.GetString(10), NormalizedMobilePhone = reader.IsDBNull(11) ? null : reader.GetString(11), Email = reader.IsDBNull(12) ? null : reader.GetString(12), NormalizedEmail = reader.IsDBNull(13) ? null : reader.GetString(13), IsActive = reader.GetBoolean(14), LastSyncedAt = reader.GetDateTime(15), Branch = new Branch { Id = reader.GetInt32(1), Name = reader.GetString(16) }, ClinicDataSource = reader.IsDBNull(17) ? null : new ClinicDataSource { Id = reader.GetInt32(2), Name = reader.GetString(17) }
                });
            }
            return result;
        }
        finally
        {
            if (closeConnection) await connection.CloseAsync();
        }
    }

    public async Task<int> EnsureCrmPersonAsync(int cardId, int userId, CancellationToken token = default)
    {
        var card = (await ReadCardsAsync("p.[Id] = @value", cardId, token)).SingleOrDefault()
            ?? throw new InvalidOperationException("Карточка пациента не найдена.");
        if (card.CrmPersonId is int existing) return existing;
        var person = new CrmPerson { LastName = card.LastName, FirstName = card.FirstName, MiddleName = card.MiddleName, DateOfBirth = card.DateOfBirth, PrimaryCardNumber = card.SourceCardNumber, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        _db.CrmPersons.Add(person);
        await _db.SaveChangesAsync(token);
        await _db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [dbo].[ExternalPatientCards] SET [CrmPersonId] = {person.Id} WHERE [Id] = {card.Id} AND [CrmPersonId] IS NULL", token);
        _db.PatientIdentityAuditEntries.Add(new PatientIdentityAuditEntry { ExternalPatientCardId = card.Id, CurrentCrmPersonId = person.Id, Action = "CreatePerson", PerformedByUserId = userId, PerformedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync(token);
        await LinkMangoCallsAsync(person.Id, token);
        return person.Id;
    }

    public async Task<int> GenerateMatchCandidatesAsync(int cardId, CancellationToken token = default)
    {
        var card = await _db.ExternalPatientCards.AsNoTracking().SingleAsync(x => x.Id == cardId, token);
        var candidates = await _db.ExternalPatientCards.AsNoTracking()
            .Where(x => x.Id != card.Id && x.CrmPersonId != null &&
                ((card.NormalizedMobilePhone != null && x.NormalizedMobilePhone == card.NormalizedMobilePhone) ||
                 (card.NormalizedEmail != null && x.NormalizedEmail == card.NormalizedEmail) ||
                 (x.LastName == card.LastName && x.FirstName == card.FirstName && x.DateOfBirth == card.DateOfBirth)))
            .Select(x => new { x.CrmPersonId, x.NormalizedMobilePhone, x.NormalizedEmail, x.LastName, x.FirstName, x.DateOfBirth })
            .Distinct().Take(20).ToListAsync(token);
        var existing = await _db.PatientMatchCandidates.Where(x => x.ExternalPatientCardId == cardId).Select(x => x.ProposedCrmPersonId).ToListAsync(token);
        var added = 0;
        foreach (var match in candidates.Where(x => x.CrmPersonId is not null && !existing.Contains(x.CrmPersonId!.Value)))
        {
            var score = (match.NormalizedMobilePhone == card.NormalizedMobilePhone && card.NormalizedMobilePhone is not null ? 60 : 0) +
                        (match.NormalizedEmail == card.NormalizedEmail && card.NormalizedEmail is not null ? 35 : 0) +
                        (match.LastName == card.LastName && match.FirstName == card.FirstName && match.DateOfBirth == card.DateOfBirth ? 25 : 0);
            _db.PatientMatchCandidates.Add(new PatientMatchCandidate { ExternalPatientCardId = cardId, ProposedCrmPersonId = match.CrmPersonId!.Value, ConfidenceScore = score, EvidenceJson = "{}", CreatedAt = DateTime.UtcNow });
            added++;
        }
        if (added > 0) await _db.SaveChangesAsync(token);
        return added;
    }

    public async Task LinkToCandidateAsync(int cardId, int candidateId, int userId, CancellationToken token = default)
    {
        var candidate = await _db.PatientMatchCandidates.SingleAsync(x => x.Id == candidateId && x.ExternalPatientCardId == cardId && x.Status == "Pending", token);
        var oldPerson = await _db.ExternalPatientCards.Where(x => x.Id == cardId).Select(x => x.CrmPersonId).SingleAsync(token);
        await _db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [dbo].[ExternalPatientCards] SET [CrmPersonId] = {candidate.ProposedCrmPersonId} WHERE [Id] = {cardId}", token);
        candidate.Status = "Accepted"; candidate.DecidedAt = DateTime.UtcNow; candidate.DecidedByUserId = userId;
        _db.PatientIdentityAuditEntries.Add(new PatientIdentityAuditEntry { ExternalPatientCardId = cardId, PreviousCrmPersonId = oldPerson, CurrentCrmPersonId = candidate.ProposedCrmPersonId, Action = "LinkToPerson", PerformedByUserId = userId, PerformedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync(token);
    }

    public async Task<IReadOnlyList<PendingCandidateRow>> GetPendingCandidatesAsync(CancellationToken token = default)
    {
        var connection = _db.Database.GetDbConnection();
        var closeConnection = connection.State != ConnectionState.Open;
        if (closeConnection) await connection.OpenAsync(token);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT TOP (500) c.[Id], c.[ExternalPatientCardId], p.[LastName], p.[FirstName], p.[MiddleName], b.[Name], person.[LastName], person.[FirstName], person.[MiddleName], c.[ConfidenceScore], c.[CreatedAt] FROM [dbo].[PatientMatchCandidates] c INNER JOIN [dbo].[ExternalPatientCards] p ON p.[Id] = c.[ExternalPatientCardId] INNER JOIN [dbo].[Branches] b ON b.[Id] = p.[BranchId] INNER JOIN [dbo].[CrmPersons] person ON person.[Id] = c.[ProposedCrmPersonId] WHERE c.[Status] = N'Pending' ORDER BY c.[ConfidenceScore] DESC, c.[CreatedAt]";
            var rows = new List<PendingCandidateRow>();
            await using var reader = await command.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token))
                rows.Add(new PendingCandidateRow(reader.GetInt32(0), reader.GetInt32(1), FullName(reader, 2), reader.GetString(5), FullName(reader, 6), reader.GetDecimal(9), reader.GetDateTime(10)));
            return rows;
        }
        finally
        {
            if (closeConnection) await connection.CloseAsync();
        }
    }

    public async Task<IReadOnlyList<PotentialDuplicateGroupRow>> GetPotentialDuplicateGroupsAsync(string? query = null, CancellationToken token = default)
    {
        var connection = _db.Database.GetDbConnection();
        var closeConnection = connection.State != ConnectionState.Open;
        if (closeConnection) await connection.OpenAsync(token);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "WITH [EligibleCards] AS (SELECT p.[Id], p.[SourceCardNumber], p.[LastName], p.[FirstName], p.[MiddleName], p.[DateOfBirth], b.[Name] AS [BranchName], p.[ClinicDataSourceId] FROM [dbo].[ExternalPatientCards] p INNER JOIN [dbo].[ClinicDataSources] s ON s.[Id] = p.[ClinicDataSourceId] INNER JOIN [dbo].[Branches] b ON b.[Id] = p.[BranchId] WHERE p.[CrmPersonId] IS NULL AND p.[SourceCardNumber] IS NOT NULL AND p.[SourceCardNumber] <> N'' AND s.[IsTest] = 0 AND p.[LastName] <> N'' AND p.[FirstName] <> N'' AND p.[LastName] NOT LIKE N'%*%' AND p.[LastName] NOT LIKE N'.%'), [CandidatePairs] AS (SELECT a.[Id] AS [FirstCardId], b.[Id] AS [SecondCardId], a.[SourceCardNumber], a.[LastName], a.[FirstName], a.[MiddleName], a.[DateOfBirth] FROM [EligibleCards] a INNER JOIN [EligibleCards] b ON b.[Id] > a.[Id] AND b.[ClinicDataSourceId] <> a.[ClinicDataSourceId] AND b.[SourceCardNumber] = a.[SourceCardNumber] AND b.[LastName] = a.[LastName] AND b.[FirstName] = a.[FirstName] LEFT JOIN [dbo].[PatientDuplicateReviewDecisions] d ON d.[FirstExternalPatientCardId] = a.[Id] AND d.[SecondExternalPatientCardId] = b.[Id] WHERE d.[Id] IS NULL), [Groups] AS (SELECT TOP (500) [SourceCardNumber], [LastName], [FirstName], [MiddleName], MIN([FirstCardId]) AS [FirstCardId], MIN([SecondCardId]) AS [SecondCardId] FROM [CandidatePairs] GROUP BY [SourceCardNumber], [LastName], [FirstName], [MiddleName]) SELECT g.[FirstCardId], g.[SecondCardId], g.[SourceCardNumber], g.[LastName], g.[FirstName], g.[MiddleName], COUNT(DISTINCT c.[Id]) AS [CardCount], STRING_AGG(CONVERT(nvarchar(max), c.[BranchName]), N', ') WITHIN GROUP (ORDER BY c.[BranchName]) AS [Branches] FROM [Groups] g INNER JOIN [EligibleCards] c ON c.[SourceCardNumber] = g.[SourceCardNumber] AND c.[LastName] = g.[LastName] AND c.[FirstName] = g.[FirstName] AND (c.[MiddleName] = g.[MiddleName] OR (c.[MiddleName] IS NULL AND g.[MiddleName] IS NULL)) GROUP BY g.[FirstCardId], g.[SecondCardId], g.[SourceCardNumber], g.[LastName], g.[FirstName], g.[MiddleName] ORDER BY g.[SourceCardNumber], g.[LastName], g.[FirstName]";
            command.CommandText = command.CommandText
                .Replace("WHERE p.[CrmPersonId] IS NULL AND", "WHERE")
                .Replace("SELECT TOP (500) [SourceCardNumber]", "SELECT [SourceCardNumber]")
                .Replace("p.[ClinicDataSourceId] FROM [dbo].[ExternalPatientCards]", "p.[ClinicDataSourceId], p.[CrmPersonId] FROM [dbo].[ExternalPatientCards]")
                .Replace("AND b.[FirstName] = a.[FirstName] LEFT JOIN", "AND b.[FirstName] = a.[FirstName] AND (a.[CrmPersonId] IS NULL OR b.[CrmPersonId] IS NULL OR a.[CrmPersonId] <> b.[CrmPersonId]) LEFT JOIN");
            // The clinic operator's rule is card number + surname + first name.
            // A missing or differently entered patronymic must not split one group.
            command.CommandText = command.CommandText
                .Replace("[SourceCardNumber], [LastName], [FirstName], [MiddleName], MIN([FirstCardId])", "[SourceCardNumber], [LastName], [FirstName], MIN([MiddleName]) AS [MiddleName], MIN([FirstCardId])")
                .Replace("GROUP BY [SourceCardNumber], [LastName], [FirstName], [MiddleName]) SELECT", "GROUP BY [SourceCardNumber], [LastName], [FirstName]) SELECT")
                .Replace(" AND (c.[MiddleName] = g.[MiddleName] OR (c.[MiddleName] IS NULL AND g.[MiddleName] IS NULL))", "");
            if (!string.IsNullOrWhiteSpace(query))
            {
                var clauses = new List<string>();
                var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                for (var i = 0; i < terms.Length; i++)
                {
                    var name = $"@search{i}";
                    clauses.Add($"(g.[SourceCardNumber] LIKE {name} OR g.[LastName] LIKE {name} OR g.[FirstName] LIKE {name} OR g.[MiddleName] LIKE {name})");
                    var parameter = command.CreateParameter(); parameter.ParameterName = name; parameter.Value = "%" + terms[i] + "%"; command.Parameters.Add(parameter);
                }
                command.CommandText = command.CommandText.Replace(" GROUP BY g.[FirstCardId]", " WHERE " + string.Join(" AND ", clauses) + " GROUP BY g.[FirstCardId]");
            }
            var rows = new List<PotentialDuplicateGroupRow>();
            await using var reader = await command.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token))
                rows.Add(new PotentialDuplicateGroupRow(reader.GetInt32(0), reader.GetInt32(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.IsDBNull(5) ? null : reader.GetString(5), reader.GetInt32(6), reader.GetString(7)));
            return rows;
        }
        finally
        {
            if (closeConnection) await connection.CloseAsync();
        }
    }

    public async Task AcceptPotentialDuplicateGroupAsync(PotentialDuplicateGroupRow group, int userId, CancellationToken token = default)
    {
        var cards = await ReadDuplicateGroupCardsAsync(group, token);
        if (cards.Count < 2) return;
        var existingPersons = cards.Where(x => x.CrmPersonId is not null).Select(x => x.CrmPersonId!.Value).Distinct().ToList();
        if (existingPersons.Count > 1) throw new InvalidOperationException("В группе уже есть разные CRM-пациенты. Требуется ручная проверка.");
        var first = cards[0];
        var personId = existingPersons.SingleOrDefault();
        if (personId == 0) { var person = new CrmPerson { LastName = first.LastName, FirstName = first.FirstName, MiddleName = first.MiddleName, DateOfBirth = first.DateOfBirth, PrimaryCardNumber = first.SourceCardNumber, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }; _db.CrmPersons.Add(person); await _db.SaveChangesAsync(token); personId = person.Id; }
        var ids = cards.Where(x => x.CrmPersonId is null).Select(x => x.Id).ToArray();
        if (ids.Length == 0) return;
        await _db.Database.ExecuteSqlRawAsync($"UPDATE [dbo].[ExternalPatientCards] SET [CrmPersonId] = {{0}} WHERE [Id] IN ({string.Join(",", ids)})", [personId], token);
        _db.PatientIdentityAuditEntries.AddRange(cards.Where(x => x.CrmPersonId is null).Select(x => new PatientIdentityAuditEntry { ExternalPatientCardId = x.Id, CurrentCrmPersonId = personId, Action = "AcceptDuplicateGroup", PerformedByUserId = userId, PerformedAt = DateTime.UtcNow }));
        await _db.SaveChangesAsync(token);
        await LinkMangoCallsAsync(personId, token);
    }

    public async Task<IReadOnlyList<CrmProfileMergeRow>> GetCrmProfilesForMergeAsync(int anchorPersonId, CancellationToken token = default)
    {
        var anchor = await _db.CrmPersons.AsNoTracking().SingleOrDefaultAsync(x => x.Id == anchorPersonId, token)
            ?? throw new InvalidOperationException("CRM-карточка не найдена.");

        var profiles = await _db.CrmPersons.AsNoTracking()
            .Include(x => x.ExternalPatientCards)
            .Where(x => x.Status != "Merged" && x.LastName == anchor.LastName && x.FirstName == anchor.FirstName)
            .OrderBy(x => x.PrimaryCardNumber).ThenBy(x => x.Id)
            .ToListAsync(token);

        var crmRows = profiles.Select(x => new CrmProfileMergeRow(
            x.Id,
            null,
            x.PrimaryCardNumber ?? "—",
            string.Join(" ", new[] { x.LastName, x.FirstName, x.MiddleName }.Where(value => !string.IsNullOrWhiteSpace(value))),
            x.DateOfBirth,
            x.ExternalPatientCards.Count,
            string.Join(", ", x.ExternalPatientCards.Select(card => card.SourceCardNumber).Where(number => !string.IsNullOrWhiteSpace(number)).Distinct().OrderBy(number => number)),
            true)).ToList();

        var unlinkedCards = await _db.ExternalPatientCards.AsNoTracking()
            .Where(x => x.CrmPersonId == null && x.LastName == anchor.LastName && x.FirstName == anchor.FirstName)
            .OrderBy(x => x.SourceCardNumber).ToListAsync(token);
        var cardRows = unlinkedCards.Select(card => new CrmProfileMergeRow(
            null,
            card.Id,
            card.SourceCardNumber ?? "—",
            string.Join(" ", new[] { card.LastName, card.FirstName, card.MiddleName }.Where(value => !string.IsNullOrWhiteSpace(value))),
            card.DateOfBirth,
            1,
            card.SourceCardNumber ?? "—",
            false));
        return crmRows.Concat(cardRows).ToList();
    }

    public async Task MergeCrmProfilesAsync(int targetPersonId, IReadOnlyCollection<int> sourcePersonIds, IReadOnlyCollection<int> sourceCardIds, int userId, CancellationToken token = default)
    {
        var sourceIds = sourcePersonIds.Where(id => id != targetPersonId).Distinct().ToArray();
        var cardIds = sourceCardIds.Distinct().ToArray();
        if (sourceIds.Length == 0 && cardIds.Length == 0) throw new InvalidOperationException("Выберите хотя бы одну карточку для переноса.");

        var profiles = await _db.CrmPersons.Include(x => x.ExternalPatientCards)
            .Where(x => x.Id == targetPersonId || sourceIds.Contains(x.Id)).ToListAsync(token);
        var target = profiles.SingleOrDefault(x => x.Id == targetPersonId)
            ?? throw new InvalidOperationException("Целевая CRM-карточка не найдена.");
        var sources = profiles.Where(x => sourceIds.Contains(x.Id)).ToList();
        if (sources.Count != sourceIds.Length) throw new InvalidOperationException("Одна или несколько выбранных CRM-карточек уже были объединены. Обновите список.");
        if (profiles.Any(x => x.Status == "Merged")) throw new InvalidOperationException("В выбранном наборе есть уже объединённая CRM-карточка. Обновите список.");
        if (profiles.Select(x => (x.LastName, x.FirstName)).Distinct().Count() != 1)
            throw new InvalidOperationException("Для защиты данных объединять можно только CRM-карточки с одинаковыми фамилией и именем.");

        var directCards = await _db.ExternalPatientCards.Where(x => cardIds.Contains(x.Id)).ToListAsync(token);
        if (directCards.Count != cardIds.Length || directCards.Any(x => x.CrmPersonId is not null))
            throw new InvalidOperationException("Одна или несколько выбранных карт уже привязаны к CRM-карточке. Обновите список.");
        if (directCards.Any(x => x.LastName != target.LastName || x.FirstName != target.FirstName))
            throw new InvalidOperationException("Для защиты данных добавлять можно только карты с одинаковыми фамилией и именем. Отчество может отличаться.");

        await using var transaction = await _db.Database.BeginTransactionAsync(token);
        try
        {
            var allIds = sourceIds.Append(targetPersonId).ToArray();
            var activities = await _db.CrmActivityLinks.Where(x => allIds.Contains(x.CrmPersonId)).ToListAsync(token);
            var duplicateActivities = activities.GroupBy(x => (x.ActivityType, x.ExternalId))
                .SelectMany(group => group.OrderBy(x => x.CrmPersonId == targetPersonId ? 0 : 1).ThenBy(x => x.Id).Skip(1)).ToList();
            _db.CrmActivityLinks.RemoveRange(duplicateActivities);
            await _db.SaveChangesAsync(token);

            foreach (var activity in activities.Except(duplicateActivities).Where(x => x.CrmPersonId != targetPersonId)) activity.CrmPersonId = targetPersonId;
            var candidates = await _db.PatientMatchCandidates.Where(x => sourceIds.Contains(x.ProposedCrmPersonId) || x.ProposedCrmPersonId == targetPersonId).ToListAsync(token);
            var duplicateCandidates = candidates.GroupBy(x => x.ExternalPatientCardId)
                .SelectMany(group => group.OrderBy(x => x.ProposedCrmPersonId == targetPersonId ? 0 : 1).ThenByDescending(x => x.CreatedAt).Skip(1)).ToList();
            _db.PatientMatchCandidates.RemoveRange(duplicateCandidates);
            foreach (var candidate in candidates.Except(duplicateCandidates).Where(x => sourceIds.Contains(x.ProposedCrmPersonId))) candidate.ProposedCrmPersonId = targetPersonId;

            var movedCards = sources.SelectMany(x => x.ExternalPatientCards).Concat(directCards).ToList();
            foreach (var card in movedCards) card.CrmPersonId = targetPersonId;
            var tasks = await _db.WorkTasks.Where(x => x.CrmPersonId.HasValue && sourceIds.Contains(x.CrmPersonId.Value)).ToListAsync(token);
            foreach (var task in tasks) task.CrmPersonId = targetPersonId;
            foreach (var source in sources) { source.Status = "Merged"; source.UpdatedAt = DateTime.UtcNow; }
            target.UpdatedAt = DateTime.UtcNow;
            _db.PatientIdentityAuditEntries.AddRange(movedCards.Select(card => new PatientIdentityAuditEntry
            {
                ExternalPatientCardId = card.Id,
                PreviousCrmPersonId = sourceIds
                    .Where(id => profiles.Single(profile => profile.Id == id).ExternalPatientCards.Any(sourceCard => sourceCard.Id == card.Id))
                    .Select(id => (int?)id)
                    .FirstOrDefault(),
                CurrentCrmPersonId = targetPersonId,
                Action = "MergeCrmProfiles",
                PerformedByUserId = userId,
                PerformedAt = DateTime.UtcNow
            }));
            await _db.SaveChangesAsync(token);
            await transaction.CommitAsync(token);
        }
        catch
        {
            await transaction.RollbackAsync(token);
            throw;
        }

        await LinkMangoCallsAsync(targetPersonId, token);
    }

    public async Task RejectPotentialDuplicateGroupAsync(PotentialDuplicateGroupRow group, int userId, CancellationToken token = default)
    {
        var cards = await ReadDuplicateGroupCardsAsync(group, token);
        for (var i = 0; i < cards.Count; i++)
        for (var j = i + 1; j < cards.Count; j++)
            _db.PatientDuplicateReviewDecisions.Add(new PatientDuplicateReviewDecision { FirstExternalPatientCardId = Math.Min(cards[i].Id, cards[j].Id), SecondExternalPatientCardId = Math.Max(cards[i].Id, cards[j].Id), Status = "Rejected", DecidedByUserId = userId, DecidedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync(token);
    }

    private async Task<List<ExternalPatientCard>> ReadDuplicateGroupCardsAsync(PotentialDuplicateGroupRow group, CancellationToken token)
    {
        var connection = _db.Database.GetDbConnection();
        var closeConnection = connection.State != ConnectionState.Open;
        if (closeConnection) await connection.OpenAsync(token);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT p.[Id], p.[BranchId], p.[ClinicDataSourceId], p.[CrmPersonId], p.[SourcePatientId], p.[SourceCardNumber], p.[LastName], p.[FirstName], p.[MiddleName], p.[DateOfBirth], p.[MobilePhone], p.[NormalizedMobilePhone], p.[Email], p.[NormalizedEmail], p.[IsActive], p.[LastSyncedAt], b.[Name], s.[Name] FROM [dbo].[ExternalPatientCards] p INNER JOIN [dbo].[ClinicDataSources] s ON s.[Id] = p.[ClinicDataSourceId] INNER JOIN [dbo].[Branches] b ON b.[Id] = p.[BranchId] WHERE p.[SourceCardNumber] = @cardNumber AND p.[LastName] = @lastName AND p.[FirstName] = @firstName AND s.[IsTest] = 0 ORDER BY b.[Name]";
            foreach (var (name, value) in new[] { ("@cardNumber", (object?)group.CardNumber), ("@lastName", group.LastName), ("@firstName", group.FirstName) }) { var p = command.CreateParameter(); p.ParameterName = name; p.Value = value ?? DBNull.Value; command.Parameters.Add(p); }
            var cards = new List<ExternalPatientCard>();
            await using var reader = await command.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token))
                cards.Add(new ExternalPatientCard { Id = reader.GetInt32(0), BranchId = reader.GetInt32(1), ClinicDataSourceId = reader.IsDBNull(2) ? null : reader.GetInt32(2), CrmPersonId = reader.IsDBNull(3) ? null : reader.GetInt32(3), SourcePatientId = reader.GetInt64(4), SourceCardNumber = reader.IsDBNull(5) ? null : reader.GetString(5), LastName = reader.GetString(6), FirstName = reader.GetString(7), MiddleName = reader.IsDBNull(8) ? null : reader.GetString(8), DateOfBirth = reader.IsDBNull(9) ? null : reader.GetDateTime(9), MobilePhone = reader.IsDBNull(10) ? null : reader.GetString(10), NormalizedMobilePhone = reader.IsDBNull(11) ? null : reader.GetString(11), Email = reader.IsDBNull(12) ? null : reader.GetString(12), NormalizedEmail = reader.IsDBNull(13) ? null : reader.GetString(13), IsActive = reader.GetBoolean(14), LastSyncedAt = reader.GetDateTime(15), Branch = new Branch { Id = reader.GetInt32(1), Name = reader.GetString(16) }, ClinicDataSource = new ClinicDataSource { Id = reader.GetInt32(2), Name = reader.GetString(17) } });
            return cards;
        }
        finally { if (closeConnection) await connection.CloseAsync(); }
    }

    public async Task AcceptPotentialPhoneDuplicateAsync(int firstCardId, int secondCardId, int userId, CancellationToken token = default)
    {
        var first = (await ReadCardsAsync("p.[Id] = @value", firstCardId, token)).Single();
        var second = (await ReadCardsAsync("p.[Id] = @value", secondCardId, token)).Single();
        if (first.CrmPersonId is not null || second.CrmPersonId is not null)
            throw new InvalidOperationException("Одна из карточек уже связана с CRM-пациентом. Обновите очередь.");
        var person = new CrmPerson { LastName = first.LastName, FirstName = first.FirstName, MiddleName = first.MiddleName, DateOfBirth = first.DateOfBirth, PrimaryCardNumber = first.SourceCardNumber, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        _db.CrmPersons.Add(person);
        await _db.SaveChangesAsync(token);
        await _db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [dbo].[ExternalPatientCards] SET [CrmPersonId] = {person.Id} WHERE [Id] IN ({firstCardId}, {secondCardId})", token);
        _db.PatientDuplicateReviewDecisions.Add(new PatientDuplicateReviewDecision { FirstExternalPatientCardId = Math.Min(firstCardId, secondCardId), SecondExternalPatientCardId = Math.Max(firstCardId, secondCardId), Status = "Accepted", DecidedByUserId = userId, DecidedAt = DateTime.UtcNow });
        _db.PatientIdentityAuditEntries.AddRange(
            new PatientIdentityAuditEntry { ExternalPatientCardId = firstCardId, CurrentCrmPersonId = person.Id, Action = "AcceptPhoneDuplicate", PerformedByUserId = userId, PerformedAt = DateTime.UtcNow },
            new PatientIdentityAuditEntry { ExternalPatientCardId = secondCardId, CurrentCrmPersonId = person.Id, Action = "AcceptPhoneDuplicate", PerformedByUserId = userId, PerformedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync(token);
    }

    public async Task RejectPotentialPhoneDuplicateAsync(int firstCardId, int secondCardId, int userId, CancellationToken token = default)
    {
        _db.PatientDuplicateReviewDecisions.Add(new PatientDuplicateReviewDecision { FirstExternalPatientCardId = Math.Min(firstCardId, secondCardId), SecondExternalPatientCardId = Math.Max(firstCardId, secondCardId), Status = "Rejected", DecidedByUserId = userId, DecidedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync(token);
    }

    private static string FullName(System.Data.Common.DbDataReader reader, int index) => string.Join(" ", new[] { reader.GetString(index), reader.GetString(index + 1), reader.IsDBNull(index + 2) ? null : reader.GetString(index + 2) }.Where(x => !string.IsNullOrWhiteSpace(x)));

    public async Task RejectCandidateAsync(int candidateId, int userId, string? comment, CancellationToken token = default)
    {
        var candidate = await _db.PatientMatchCandidates.SingleAsync(x => x.Id == candidateId && x.Status == "Pending", token);
        candidate.Status = "Rejected";
        candidate.DecidedAt = DateTime.UtcNow;
        candidate.DecidedByUserId = userId;
        candidate.DecisionComment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        await _db.SaveChangesAsync(token);
    }

    public async Task<int> LinkMangoCallsAsync(int personId, CancellationToken token = default)
    {
        var phones = await _db.ExternalPatientCards.AsNoTracking().Where(x => x.CrmPersonId == personId && x.NormalizedMobilePhone != null)
            .Select(x => x.NormalizedMobilePhone!).Distinct().ToListAsync(token);
        if (phones.Count == 0) return 0;
        var calls = await _db.CallCenterCallRecords.AsNoTracking().Where(x => x.ExternalPhoneNumber != null)
            .OrderByDescending(x => x.CallDateTime).Take(10000).ToListAsync(token);
        var known = await _db.CrmActivityLinks.Where(x => x.CrmPersonId == personId && x.ActivityType == "MangoCall").Select(x => x.ExternalId).ToListAsync(token);
        var links = calls.Where(x => ExternalPatientSynchronizationService.NormalizePhone(x.ExternalPhoneNumber) is string phone && phones.Contains(phone) && !known.Contains(x.Id.ToString()))
            .Select(x => new CrmActivityLink { CrmPersonId = personId, ActivityType = "MangoCall", ExternalId = x.Id.ToString(), ContactValue = x.ExternalPhoneNumber, OccurredAt = x.CallDateTime, Title = $"Mango · {x.CallDateTime:dd.MM.yyyy HH:mm} · {x.Direction} · {x.ExternalPhoneNumber ?? "номер не указан"} · {(x.DurationSeconds is > 0 ? TimeSpan.FromSeconds(x.DurationSeconds.Value).ToString(@"m\:ss") : "без длительности")}" }).ToList();
        if (links.Count > 0) { _db.CrmActivityLinks.AddRange(links); await _db.SaveChangesAsync(token); }
        return links.Count;
    }
}

public sealed record PatientSearchRow(int CardId, int? CrmPersonId, string LastName, string FirstName, string? MiddleName, DateTime? DateOfBirth, string? Phone, string? Email, string? CardNumber, string BranchName, string SourceName)
{
    public string FullName => string.Join(" ", new[] { LastName, FirstName, MiddleName }.Where(x => !string.IsNullOrWhiteSpace(x)));
    public string PersonState => CrmPersonId is null ? "Карточка филиала" : "Единый CRM-пациент";
}

public sealed record PatientCardDetails(ExternalPatientCard Card, IReadOnlyList<ExternalPatientCard> LinkedCards, IReadOnlyList<PatientMatchCandidate> Candidates, IReadOnlyList<WorkTask> Tasks, IReadOnlyList<CrmActivityLink> Activity);
public sealed record CrmProfileMergeRow(int? PersonId, int? ExternalCardId, string CardNumber, string FullName, DateTime? DateOfBirth, int CardCount, string CardNumbers, bool IsCrmProfile)
{
    public string DisplayName => $"№ {CardNumber} · {FullName} · {(IsCrmProfile ? $"CRM, карт: {CardCount}" : "карточка филиала")}";
}
public sealed record PendingCandidateRow(int CandidateId, int ExternalPatientCardId, string CardPatientName, string BranchName, string CrmPersonName, decimal ConfidenceScore, DateTime CreatedAt);
public sealed record PotentialDuplicateGroupRow(int FirstCardId, int SecondCardId, string CardNumber, string LastName, string FirstName, string? MiddleName, int CardCount, string Branches)
{
    public string PatientName => string.Join(" ", new[] { LastName, FirstName, MiddleName }.Where(x => !string.IsNullOrWhiteSpace(x)));
}
