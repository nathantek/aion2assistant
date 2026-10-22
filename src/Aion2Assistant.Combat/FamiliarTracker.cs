using Aion2Assistant.Core.Events;

namespace Aion2Assistant.Combat;

/// <summary>Règle d'affichage : « MAX » si palier ≥ 3, sinon « âmes/seuil » avec les seuils 5, 25 et 75.</summary>
public static class FamiliarProgress
{
    public const int MaxTier = 3;
    private static readonly int[] Thresholds = [5, 25, 75];

    public static bool IsMax(int tier) => tier >= MaxTier;

    /// <summary>Âmes nécessaires pour le palier suivant ; null au MAX.</summary>
    public static int? Threshold(int tier) => IsMax(tier) ? null : Thresholds[Math.Max(0, tier)];

    public static string Text(int tier, int souls) => Threshold(tier) is { } max ? $"{souls}/{max}" : "MAX";

    /// <summary>Remplissage de la barre, entre 0 et 1 (1 au MAX).</summary>
    public static double Fraction(int tier, int souls) =>
        Threshold(tier) is { } max ? Math.Clamp((double)souls / max, 0, 1) : 1;
}

/// <summary>État affichable d'un familier du serveur affiché.</summary>
public sealed record FamiliarStatus(int Id, int Tier, int Souls, long LastUpdateMs)
{
    public bool IsMax => FamiliarProgress.IsMax(Tier);
    public string Text => FamiliarProgress.Text(Tier, Souls);
    public double Fraction => FamiliarProgress.Fraction(Tier, Souls);
}

/// <summary>Montée de palier venue d'un 0x1490 : palier 1 ou 2 = pop-up « palier », palier 3 = pop-up « MAX ».</summary>
public sealed record FamiliarMilestone(int Id, int Tier)
{
    public bool IsMax => FamiliarProgress.IsMax(Tier);
}

/// <summary><see cref="Changed"/> : l'état a changé (à enregistrer et à réafficher).</summary>
public sealed record FamiliarApplyResult(bool Changed, IReadOnlyList<FamiliarMilestone> Milestones)
{
    public static readonly FamiliarApplyResult None = new(false, []);
}

/// <summary>
/// Suivi des âmes de familiers, par serveur (la possession est partagée par tous les personnages d'un serveur).
/// Logique pure, sans thread : l'appelant sérialise les appels (thread UI).
/// </summary>
public sealed class FamiliarTracker
{
    public const int DefaultRecentLimit = 20;

    /// <summary>Taille maximale de la file d'attente (serveur inconnu) : au-delà, les plus anciens sont écartés.</summary>
    public const int MaxPending = 1000;

    private readonly Queue<GameEvent> _pending = new();
    private readonly HashSet<int> _syncedThisSession = new();
    private int? _currentServer;
    private long? _identifiedAtMs;
    private long? _streamStartedAtMs;
    private bool _pendingOverflowLogged;

    public FamiliarTracker(FamiliarFile file)
    {
        // Un familiars.json valide mais édité à la main peut contenir des null ou des valeurs hors bornes.
        file.Servers ??= new();
        foreach (var key in file.Servers.Where(p => p.Value is null).Select(p => p.Key).ToList())
            file.Servers.Remove(key);
        foreach (var server in file.Servers.Values)
        {
            server.Entries ??= new();
            foreach (var key in server.Entries.Where(p => p.Value is null).Select(p => p.Key).ToList())
                server.Entries.Remove(key);
            foreach (var rec in server.Entries.Values)
            {
                rec.Tier = Math.Clamp(rec.Tier, 0, FamiliarProgress.MaxTier);
                rec.Souls = Math.Max(0, rec.Souls);
            }
        }
        File = file;
    }

    /// <summary>Contenu à enregistrer dans familiars.json (modifié sur place).</summary>
    public FamiliarFile File { get; }

    /// <summary>Message de diagnostic (valeur inattendue ignorée ou corrigée), à journaliser.</summary>
    public event Action<string>? DebugMessage;

    /// <summary>Serveur du personnage de ce flux ; null tant qu'il n'est pas identifié.</summary>
    public int? CurrentServerId => _currentServer;

    /// <summary>Serveur affiché : le serveur courant, sinon le dernier connu (0 = aucun).</summary>
    public int DisplayServerId => _currentServer ?? File.LastServerId;

    /// <summary>Horodatage (ms Unix) de l'identification du serveur courant ; null tant qu'il n'est pas identifié.</summary>
    public long? IdentifiedAtMs => _identifiedAtMs;

    /// <summary>Début du flux de jeu verrouillé (ms Unix) ; null tant qu'aucun flux n'a commencé.</summary>
    public long? StreamStartedAtMs => _streamStartedAtMs;

    /// <summary>Des événements attendent que le serveur soit connu.</summary>
    public bool HasPending => _pending.Count > 0;

    /// <summary>Nombre d'événements en attente du serveur (au plus <see cref="MaxPending"/>).</summary>
    public int PendingCount => _pending.Count;

    /// <summary>Vrai si un état complet (0x0090) du serveur affiché a été reçu pendant cette session.</summary>
    public bool IsSynced => _syncedThisSession.Contains(DisplayServerId);

    /// <summary>Date du dernier état complet du serveur affiché (ms Unix) ; null s'il n'a jamais été synchronisé.</summary>
    public long? LastSyncMs => Server(DisplayServerId) is { LastSyncMs: > 0 } s ? s.LastSyncMs : null;

    public FamiliarApplyResult Apply(GameEvent e)
    {
        switch (e)
        {
            case StreamStartedEvent started:
                _streamStartedAtMs = started.TimeMs;
                _currentServer = null;
                _identifiedAtMs = null;
                _pending.Clear();
                _pendingOverflowLogged = false;
                return FamiliarApplyResult.None;
            case SelfIdentifiedEvent self:
                return OnSelf(self);
            case FamiliarStateEvent or FamiliarSoulEvent or FamiliarTierEvent:
                if (_currentServer is not { } server)
                {
                    Enqueue(e);
                    return FamiliarApplyResult.None;
                }
                var milestones = new List<FamiliarMilestone>();
                var changed = ApplyTo(server, e, milestones);
                return changed ? new FamiliarApplyResult(true, milestones) : FamiliarApplyResult.None;
            default:
                return FamiliarApplyResult.None;
        }
    }

    /// <summary>État d'un familier du serveur affiché ; null s'il n'est pas connu.</summary>
    public FamiliarStatus? Get(int id) =>
        Server(DisplayServerId)?.Entries.TryGetValue(id, out var r) == true ? Status(id, r) : null;

    /// <summary>Derniers familiers mis à jour (âme ou palier) du serveur affiché, du plus récent au plus ancien.</summary>
    public IReadOnlyList<FamiliarStatus> Recent(int limit = DefaultRecentLimit)
    {
        if (Server(DisplayServerId) is not { } s || limit <= 0) return [];
        return s.Entries
            .Where(p => p.Value.LastUpdateMs > 0)
            .OrderByDescending(p => p.Value.LastUpdateMs)
            .ThenBy(p => p.Key)
            .Take(limit)
            .Select(p => Status(p.Key, p.Value))
            .ToList();
    }

    /// <summary>File bornée : si le serveur n'est jamais identifié, on garde les plus récents (un seul message de diagnostic).</summary>
    private void Enqueue(GameEvent e)
    {
        if (_pending.Count >= MaxPending)
        {
            _pending.Dequeue();
            if (!_pendingOverflowLogged)
            {
                _pendingOverflowLogged = true;
                DebugMessage?.Invoke($"Familiers : plus de {MaxPending} événements en attente du serveur, les plus anciens sont écartés");
            }
        }
        _pending.Enqueue(e);
    }

    private FamiliarApplyResult OnSelf(SelfIdentifiedEvent self)
    {
        // Serveur inconnu (0) : on garde les événements en attente.
        if (self.ServerId <= 0) return FamiliarApplyResult.None;
        // Le même personnage revu ne relance pas la période de grâce du bandeau.
        if (_currentServer != self.ServerId) _identifiedAtMs = self.TimeMs;
        _currentServer = self.ServerId;
        var changed = File.LastServerId != self.ServerId;
        File.LastServerId = self.ServerId;
        var milestones = new List<FamiliarMilestone>();
        foreach (var e in _pending)
            changed |= ApplyTo(self.ServerId, e, milestones);
        _pending.Clear();
        _pendingOverflowLogged = false;
        return new FamiliarApplyResult(changed, milestones);
    }

    private bool ApplyTo(int server, GameEvent e, List<FamiliarMilestone> milestones)
    {
        switch (e)
        {
            case FamiliarStateEvent state:
                ReplaceState(server, state);
                return true;
            case FamiliarSoulEvent soul:
                if (soul.Id <= 0 || soul.Amount <= 0)
                {
                    DebugMessage?.Invoke($"Familiers : âme ignorée (familier {soul.Id}, +{soul.Amount})");
                    return false;
                }
                var rec = Entry(server, soul.Id);
                rec.Souls += soul.Amount;
                rec.LastUpdateMs = soul.TimeMs;
                return true;
            case FamiliarTierEvent tier:
                return ApplyTier(server, tier, milestones);
            default:
                return false;
        }
    }

    /// <summary>L'état complet remplace tout ; il ne donne jamais de pop-up (ce n'est pas une montée de palier en direct).</summary>
    private void ReplaceState(int server, FamiliarStateEvent state)
    {
        var s = EnsureServer(server);
        var entries = new Dictionary<int, FamiliarRecord>();
        foreach (var en in state.Entries)
        {
            if (en is null || en.Id <= 0)
            {
                DebugMessage?.Invoke($"Familiers : entrée d'état ignorée ({en?.Id})");
                continue;
            }
            var lastUpdate = s.Entries.TryGetValue(en.Id, out var old) ? old.LastUpdateMs : 0;
            entries[en.Id] = new FamiliarRecord
            {
                Tier = Math.Clamp(en.Tier, 0, FamiliarProgress.MaxTier),
                Souls = Math.Max(0, en.Souls),
                LastUpdateMs = lastUpdate,
            };
        }
        s.Entries = entries;
        s.LastSyncMs = state.TimeMs;
        _syncedThisSession.Add(server);
    }

    private bool ApplyTier(int server, FamiliarTierEvent tier, List<FamiliarMilestone> milestones)
    {
        if (tier.Id <= 0 || tier.Tier < 0)
        {
            DebugMessage?.Invoke($"Familiers : palier ignoré (familier {tier.Id}, palier {tier.Tier})");
            return false;
        }
        var value = Math.Min(tier.Tier, FamiliarProgress.MaxTier);
        if (value != tier.Tier)
            DebugMessage?.Invoke($"Familiers : palier {tier.Tier} inattendu pour {tier.Id}, ramené à {value}");
        var rec = Entry(server, tier.Id);
        var previous = rec.Tier;
        if (value < previous)
            DebugMessage?.Invoke($"Familiers : palier en baisse pour {tier.Id} ({previous} → {value})");
        rec.Tier = value;
        rec.Souls = 0;
        rec.LastUpdateMs = tier.TimeMs;
        if (value > previous) milestones.Add(new FamiliarMilestone(tier.Id, value));
        return true;
    }

    private FamiliarServerState? Server(int server) => File.Servers.GetValueOrDefault(server);

    private FamiliarServerState EnsureServer(int server)
    {
        if (!File.Servers.TryGetValue(server, out var s)) File.Servers[server] = s = new FamiliarServerState();
        return s;
    }

    /// <summary>Familier absent = palier 0, 0 âme.</summary>
    private FamiliarRecord Entry(int server, int id)
    {
        var s = EnsureServer(server);
        if (!s.Entries.TryGetValue(id, out var rec)) s.Entries[id] = rec = new FamiliarRecord();
        return rec;
    }

    private static FamiliarStatus Status(int id, FamiliarRecord r) => new(id, r.Tier, r.Souls, r.LastUpdateMs);
}
