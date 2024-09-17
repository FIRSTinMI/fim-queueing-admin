using Microsoft.EntityFrameworkCore;

namespace fim_queueing_admin.Data;

/// <summary>
/// Used for anything that can't be easily done with native EF
/// </summary>
/// <param name="dbContext"></param>
public class FimRepository(FimDbContext dbContext)
{
    public async Task<bool> SetCartLastSeen(Guid id, DateTime? lastSeen)
    {
        // Note: EF will automatically parameterize this query, id and lastSeen are *not* just substituted in as strings
        var result = await dbContext.Database.ExecuteSqlAsync(
            $"UPDATE equipment SET configuration['LastSeen'] = to_jsonb({lastSeen}) WHERE id = {id}");
        return result > 0;
    }
}