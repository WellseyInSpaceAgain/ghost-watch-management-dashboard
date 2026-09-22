using GhostWatch.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace GhostWatch.Api.Eve.Auth;

// Future ESI services use this entry point; refresh-token rotation is serialised per character.
public sealed class CharacterAccessTokens(GhostWatchDbContext db, SsoClient sso, CharacterGate gates)
{
    public async Task<string> Get(long characterId, CancellationToken ct)
    {
        var gate = gates.For(characterId);
        await gate.WaitAsync(ct);
        try
        {
            var character = await db.EveCharacters.SingleOrDefaultAsync(x => x.CharacterId == characterId, ct)
                ?? throw new SsoException("Character is not connected.");
            // A scoped context may already have tracked this row before waiting on the gate.
            await db.Entry(character).ReloadAsync(ct);
            return await sso.AccessToken(character, ct);
        }
        finally { gate.Release(); }
    }
}
