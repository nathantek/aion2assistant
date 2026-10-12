using Aion2Assistant.Core.Events;

namespace Aion2Assistant.Combat;

/// <summary>
/// Suit le cycle de vie d'un combat et accumule les dégâts par acteur.
/// Not thread-safe: call from a single thread (the UI thread).
/// </summary>
public sealed class EncounterTracker
{
    private const long DuplicateDeathWindowMs = 2_000;

    /// <summary>Coups tardifs sur (ou d') un acteur mort : ils ne doivent pas ouvrir un nouveau combat (combat fantôme de 0 s).</summary>
    private const long PostDeathIgnoreMs = 10_000;

    /// <summary>Plafond d'un soin quand les PV max de la cible sont inconnus.</summary>
    private const long UnknownMaxHpHealCap = 1_000_000;

    private readonly record struct RecapEntry(long AtMs, uint Source, int Skill, long Amount, bool IsHeal);

    private readonly GameData _data;
    private readonly CombatOptions _options;
    private readonly Dictionary<uint, ActorMeter> _dps = new();
    private readonly Dictionary<uint, Dictionary<uint, ActorMeter>> _dpsByTarget = new();
    // Dégâts par seconde : cible, puis source, puis seconde écoulée depuis le début du combat.
    private readonly Dictionary<uint, Dictionary<uint, SecondSeries>> _secondsByTarget = new();
    private readonly Dictionary<uint, ActorMeter> _heal = new();
    private readonly Dictionary<uint, ActorMeter> _taken = new();
    private readonly Dictionary<uint, long> _damageToTarget = new();
    private readonly Dictionary<uint, List<RecapEntry>> _recap = new();
    private readonly List<DeathRecap> _deaths = new();
    private readonly Dictionary<uint, long> _lastDeathMs = new();
    private bool _hasEncounter;
    private long _startMs;
    private int _startMapId;
    private long _lastActivityMs;
    private EncounterSnapshot? _frozen;
    private EncounterSnapshot? _lastGood;
    private long _trainingArmedDurationMs;
    private long _trainingDurationMs;

    public EncounterTracker(GameData data, CombatOptions options)
    {
        _data = data;
        _options = options;
        Entities = new EntityRegistry(data);
    }

    public EntityRegistry Entities { get; }
    public bool IsActive { get; private set; }

    /// <summary>Essai demandé (StartTraining), en attente du premier coup.</summary>
    public bool TrainingArmed { get; private set; }

    /// <summary>Essai d'entraînement en cours.</summary>
    public bool IsTraining { get; private set; }

    /// <summary>Dernière carte annoncée par un ZoneChangedEvent (0 au départ et après StreamStarted).</summary>
    public int CurrentMapId { get; private set; }
    public event Action<EncounterSnapshot>? EncounterEnded;

    /// <summary>Levé quand le traitement d'un événement (ou Tick/Reset/Snapshot) a échoué ; le suivi continue.</summary>
    public event Action<Exception>? Faulted;

    /// <summary>Message de diagnostic (niveau Debug) : soin écarté, etc.</summary>
    public event Action<string>? DebugMessage;

    /// <summary>Message de diagnostic (niveau Information) : une ligne par combat, si des soins ont été écartés.</summary>
    public event Action<string>? InfoMessage;

    private int _droppedHeals;

    private void Info(string message)
    {
        try
        {
            InfoMessage?.Invoke(message);
        }
        catch
        {
            // Un abonné défaillant ne doit pas casser le suivi.
        }
    }

    private void Debug(string message)
    {
        try
        {
            DebugMessage?.Invoke(message);
        }
        catch
        {
            // Un abonné défaillant ne doit pas casser le suivi.
        }
    }

    private void Fault(Exception ex)
    {
        try
        {
            Faulted?.Invoke(ex);
        }
        catch
        {
            // Un abonné défaillant ne doit pas casser le suivi.
        }
    }

    public void Handle(IEnumerable<GameEvent> events)
    {
        foreach (var e in events) Handle(e);
    }

    public void Handle(GameEvent e)
    {
        try
        {
            HandleCore(e);
        }
        catch (Exception ex)
        {
            Fault(ex);
        }
    }

    private void HandleCore(GameEvent e)
    {
        // Un essai à durée fixe se termine avant tout événement horodaté à partir de l'échéance.
        if (IsTraining && _trainingDurationMs > 0 && e.TimeMs >= _startMs + _trainingDurationMs)
            End(EncounterOutcome.Training, _startMs + _trainingDurationMs);
        switch (e)
        {
            case ZoneChangedEvent { IsTeleport: false } or StreamStartedEvent:
                TrainingArmed = false;
                // Figer avant que le registre ne soit vidé, pour conserver les noms.
                if (IsActive) End(EncounterOutcome.Timeout, _lastActivityMs);
                _lastDeathMs.Clear();
                Entities.Apply(e);
                CurrentMapId = e is ZoneChangedEvent zc ? zc.MapId : 0;
                break;
            case ZoneChangedEvent teleport:
                Entities.Apply(e);
                CurrentMapId = teleport.MapId;
                break;
            case HpEvent h:
                var previous = Entities.CurrentHpOf(h.ActorId);
                Entities.Apply(e);
                OnHp(h, previous);
                break;
            case DeathEvent d:
                Entities.Apply(e);
                OnDeath(d);
                break;
            case CastEvent cast:
                Entities.Apply(e);
                InferPlayer(cast.ActorId, cast.SkillCode);
                break;
            case DamageEvent dmg:
                Entities.Apply(e);
                OnDamage(dmg);
                break;
            case HealEvent heal:
                Entities.Apply(e);
                OnHeal(heal.TimeMs, heal.SourceId, heal.TargetId, heal.SkillCode, heal.Amount, heal.Flags, 1);
                break;
            default:
                Entities.Apply(e);
                break;
        }
    }

    private bool IsPlayer(uint id, int skillCode)
    {
        var kind = Entities.KindOf(id);
        return kind == EntityKind.Player || (kind == EntityKind.Unknown && SkillCodes.IsPlayerSkill(skillCode));
    }

    private bool TrackedSource(uint id, int skillCode) =>
        (Entities.SelfId != 0 && id == Entities.SelfId) || (_options.IncludeAllPlayers && IsPlayer(id, skillCode));

    private bool TrackedMember(uint id) =>
        (Entities.SelfId != 0 && id == Entities.SelfId) || (_options.IncludeAllPlayers && Entities.KindOf(id) == EntityKind.Player);

    private bool TrackedTarget(uint id) =>
        (Entities.SelfId != 0 && id == Entities.SelfId) || Entities.KindOf(id) == EntityKind.Player;

    private void InferPlayer(uint id, int skillCode)
    {
        if (Entities.KindOf(id) == EntityKind.Unknown && SkillCodes.IsPlayerSkill(skillCode))
            Entities.MarkInferredPlayer(id, SkillCodes.ClassOf(skillCode));
    }

    private void OnDamage(DamageEvent e)
    {
        if (SkillCodes.IsNonDamage(e.SkillCode)) return;
        var src = Entities.ResolveOwner(e.SourceId);
        // Sur un coup reçu par un joueur, un code de classe peut être le bouclier de la cible (ex. Nezekan's Shield)
        // attribué au PNJ attaquant : il ne prouve pas que l'auteur est un joueur.
        if (!TrackedTarget(e.TargetId)) InferPlayer(src, e.SkillCode);
        if (TrackedSource(src, e.SkillCode))
        {
            if (TrackedTarget(e.TargetId))
            {
                OnHeal(e.TimeMs, e.SourceId, e.TargetId, e.SkillCode, e.Damage, e.Flags, e.HitCount);
                return;
            }
            if (!IsActive && RecentlyDead(e.TargetId, e.TimeMs)) return;
            Begin(e.TimeMs);
            _lastActivityMs = Math.Max(_lastActivityMs, e.TimeMs);
            if (!_dps.TryGetValue(src, out var meter)) _dps[src] = meter = new ActorMeter(src);
            meter.Add(SkillCodes.Normalize(e.SkillCode), e.Damage, e.Flags, e.HitCount);
            if (!_dpsByTarget.TryGetValue(e.TargetId, out var bySource)) _dpsByTarget[e.TargetId] = bySource = new Dictionary<uint, ActorMeter>();
            if (!bySource.TryGetValue(src, out var targetMeter)) bySource[src] = targetMeter = new ActorMeter(src);
            targetMeter.Add(SkillCodes.Normalize(e.SkillCode), e.Damage, e.Flags, e.HitCount);
            if (!_secondsByTarget.TryGetValue(e.TargetId, out var secondsBySource)) _secondsByTarget[e.TargetId] = secondsBySource = new Dictionary<uint, SecondSeries>();
            if (!secondsBySource.TryGetValue(src, out var seconds)) secondsBySource[src] = seconds = new SecondSeries();
            var second = (int)Math.Max(0, (e.TimeMs - _startMs) / 1000);
            seconds.Add(second, e.Damage);
            _damageToTarget[e.TargetId] = _damageToTarget.GetValueOrDefault(e.TargetId) + e.Damage;
        }
        else if (TrackedTarget(e.TargetId) && !IsPlayer(src, e.SkillCode))
        {
            if (!TrackedMember(e.TargetId)) return;
            if (!IsActive && RecentlyDead(src, e.TimeMs)) return;
            Begin(e.TimeMs);
            _lastActivityMs = Math.Max(_lastActivityMs, e.TimeMs);
            var skill = SkillCodes.Normalize(e.SkillCode);
            if (!_taken.TryGetValue(e.TargetId, out var meter)) _taken[e.TargetId] = meter = new ActorMeter(e.TargetId);
            meter.Add(skill, e.Damage, e.Flags, e.HitCount);
            Record(e.TargetId, new RecapEntry(e.TimeMs, src, skill, e.Damage, false));
        }
    }

    private void OnHeal(long timeMs, uint sourceId, uint targetId, int skillCode, long amount, HitFlags flags, int hits)
    {
        if (!IsActive || SkillCodes.IsSpiritLinkRecord(skillCode)) return;
        var src = Entities.ResolveOwner(sourceId);
        if (!TrackedSource(src, skillCode)) return;

        // Un soin ne peut dépasser les PV max de la cible : au-delà, valeur mal décodée.
        var max = Entities.MaxHpOf(targetId);
        var cap = max > 0 ? max : UnknownMaxHpHealCap;
        if (amount > cap)
        {
            _droppedHeals++;
            Debug($"Soin écarté : {amount} > {cap} (skill {skillCode}, {sourceId} → {targetId})");
            return;
        }
        _lastActivityMs = Math.Max(_lastActivityMs, timeMs);

        long? overheal = null; // null : PV de la cible inconnus, surplus non mesurable
        if (Entities.CurrentHpOf(targetId) is { } cur && max > 0)
        {
            overheal = Math.Max(0, amount - Math.Max(0, max - cur));
            Entities.SetCurrentHp(targetId, Math.Min(max, cur + amount));
        }

        var skill = SkillCodes.Normalize(skillCode);
        if (!_heal.TryGetValue(src, out var meter)) _heal[src] = meter = new ActorMeter(src);
        meter.Add(skill, amount, flags, hits, overheal);
        if (TrackedMember(targetId)) Record(targetId, new RecapEntry(timeMs, src, skill, amount, true));
    }

    private void Record(uint member, RecapEntry entry)
    {
        if (!_recap.TryGetValue(member, out var list)) _recap[member] = list = new List<RecapEntry>();
        list.Add(entry);
        list.RemoveAll(r => entry.AtMs - r.AtMs > _options.RecapWindowMs);
    }

    private void OnHp(HpEvent h, long? previous)
    {
        // Un mannequin d'entraînement se régénère : pas de Wipe pendant un essai.
        if (!IsActive || IsTraining || MainTarget() != h.ActorId) return;
        var max = Entities.MaxHpOf(h.ActorId);
        if (max > 0 && h.CurrentHp == max && previous is { } p && p < max)
            End(EncounterOutcome.Wipe, h.TimeMs);
    }

    private void OnDeath(DeathEvent d)
    {
        if (_lastDeathMs.TryGetValue(d.ActorId, out var last) && d.TimeMs - last < DuplicateDeathWindowMs && d.TimeMs >= last) return;
        _lastDeathMs[d.ActorId] = d.TimeMs;
        if (IsActive && TrackedMember(d.ActorId)) RecordDeath(d);
        if (IsActive && !IsTraining && MainTarget() == d.ActorId && Entities.MaxHpOf(d.ActorId) >= _options.BossMinHp)
            End(EncounterOutcome.Kill, d.TimeMs);
    }

    private void RecordDeath(DeathEvent d)
    {
        if (!_taken.TryGetValue(d.ActorId, out var meter)) _taken[d.ActorId] = meter = new ActorMeter(d.ActorId);
        meter.Deaths++;
        var lines = new List<RecapLine>();
        if (_recap.TryGetValue(d.ActorId, out var list))
        {
            foreach (var r in list)
            {
                if (d.TimeMs - r.AtMs > _options.RecapWindowMs) continue;
                lines.Add(new RecapLine(r.AtMs, Entities.NameOf(r.Source), _data.SkillName(r.Skill), r.Amount, r.IsHeal));
            }
        }
        _deaths.Add(new DeathRecap(d.ActorId, Entities.NameOf(d.ActorId), d.TimeMs, lines));
    }

    private bool RecentlyDead(uint id, long timeMs) =>
        _lastDeathMs.TryGetValue(id, out var death) && timeMs >= death && timeMs - death <= PostDeathIgnoreMs;

    private void Begin(long timeMs)
    {
        if (IsActive) return;
        ClearData();
        _frozen = null;
        _lastGood = null;
        _hasEncounter = true;
        IsActive = true;
        _startMs = timeMs;
        _startMapId = CurrentMapId;
        _lastActivityMs = timeMs;
        if (TrainingArmed)
        {
            TrainingArmed = false;
            IsTraining = true;
            _trainingDurationMs = _trainingArmedDurationMs;
        }
    }

    private uint? MainTarget()
    {
        uint? best = null;
        long bestDmg = -1;
        foreach (var (id, dmg) in _damageToTarget)
        {
            if (Entities.KindOf(id) == EntityKind.Player) continue;
            if (dmg > bestDmg || (dmg == bestDmg && id < best))
            {
                best = id;
                bestDmg = dmg;
            }
        }
        return best;
    }

    public void Tick(long nowMs)
    {
        try
        {
            if (IsTraining)
            {
                // Pas de fin par inactivité pendant un essai ; seule l'échéance d'un essai à durée fixe le termine.
                if (_trainingDurationMs > 0 && nowMs - _startMs >= _trainingDurationMs)
                    End(EncounterOutcome.Training, _startMs + _trainingDurationMs);
            }
            else if (IsActive && nowMs - _lastActivityMs >= _options.IdleTimeoutMs)
                End(EncounterOutcome.Timeout, _lastActivityMs);
        }
        catch (Exception ex)
        {
            Fault(ex);
        }
    }

    /// <summary>
    /// Demande un essai d'entraînement (0 = durée libre) : termine le combat en cours (Manual), puis l'essai
    /// démarre au prochain coup.
    /// </summary>
    public void StartTraining(long durationMs)
    {
        try
        {
            if (IsActive) End(EncounterOutcome.Manual, _lastActivityMs);
            TrainingArmed = true;
            _trainingArmedDurationMs = Math.Max(0, durationMs);
        }
        catch (Exception ex)
        {
            Fault(ex);
        }
    }

    /// <summary>Termine l'essai en cours (Training) ou désarme l'essai demandé ; sans effet sinon.</summary>
    public void StopTraining(long nowMs)
    {
        try
        {
            if (IsTraining)
            {
                var end = Math.Max(_lastActivityMs, nowMs);
                if (_trainingDurationMs > 0) end = Math.Min(end, _startMs + _trainingDurationMs);
                End(EncounterOutcome.Training, end);
            }
            else
                TrainingArmed = false;
        }
        catch (Exception ex)
        {
            Fault(ex);
        }
    }

    /// <summary>Termine le combat actif (fin Timeout, ou Training pour un essai) ; sans effet s'il n'y en a pas.</summary>
    public void Flush(long nowMs)
    {
        try
        {
            if (IsTraining) StopTraining(_lastActivityMs);
            else if (IsActive) End(EncounterOutcome.Timeout, _lastActivityMs);
        }
        catch (Exception ex)
        {
            Fault(ex);
        }
    }

    public void Reset()
    {
        try
        {
            if (IsActive) End(EncounterOutcome.Manual, _lastActivityMs);
            TrainingArmed = false;
            IsTraining = false;
            _frozen = null;
            _hasEncounter = false;
            _lastGood = null;
            _lastDeathMs.Clear();
            ClearData();
        }
        catch (Exception ex)
        {
            Fault(ex);
        }
    }

    // _lastDeathMs survit au début d'un combat : il filtre les morts en double et les coups tardifs entre deux combats.
    private void ClearData()
    {
        _dps.Clear();
        _dpsByTarget.Clear();
        _secondsByTarget.Clear();
        _heal.Clear();
        _taken.Clear();
        _damageToTarget.Clear();
        _recap.Clear();
        _deaths.Clear();
        _droppedHeals = 0;
    }

    public EncounterSnapshot? Snapshot(long nowMs)
    {
        try
        {
            if (_frozen is not null) return _frozen;
            if (!_hasEncounter) return null;
            var duration = IsTraining
                ? (_trainingDurationMs > 0 ? Math.Min(nowMs, _startMs + _trainingDurationMs) : nowMs) - _startMs
                : Math.Min(nowMs, _lastActivityMs + _options.IdleTimeoutMs) - _startMs;
            return _lastGood = Build(Math.Max(duration, _lastActivityMs - _startMs), true, null);
        }
        catch (Exception ex)
        {
            Fault(ex);
            return _hasEncounter ? _lastGood : null;
        }
    }

    private void End(EncounterOutcome outcome, long endMs)
    {
        var duration = Math.Max(endMs, _lastActivityMs) - _startMs;
        var snap = Build(Math.Max(0, duration), false, outcome);
        IsActive = false;
        IsTraining = false;
        _frozen = snap;
        if (_droppedHeals > 0) Info($"Combat terminé : {_droppedHeals} soin(s) écarté(s) (supérieurs aux PV max de la cible)");
        _droppedHeals = 0;
        try
        {
            EncounterEnded?.Invoke(snap);
        }
        catch (Exception ex)
        {
            Fault(ex);
        }
    }

    /// <summary>
    /// Écarte des meters de dégâts et de soins les entités sans nom réel (joueurs seulement déduits, ex. « #46884 »)
    /// qui n'ont utilisé aucun skill de classe : ce sont des PNJ/effets, pas des joueurs. Soi-même est toujours gardé.
    /// </summary>
    private bool IsRealActor(ActorMeter m) =>
        m.ActorId == Entities.SelfId || Entities.HasName(m.ActorId) || m.Skills.Keys.Any(SkillCodes.IsClassSkill);

    /// <summary>
    /// Soins : en plus, seul un acteur identifié comme joueur (annoncé, ou déduit par un skill de joueur hors cible
    /// joueur) est affiché. Les boucliers absorbants arrivent avec le PNJ attaquant comme auteur et un code de classe ;
    /// un PNJ jamais annoncé apparaissait ainsi en « #id ». L'enregistrement est conservé : un vrai joueur pas encore
    /// annoncé apparaît, avec tous ses soins, dès qu'il est identifié.
    /// </summary>
    private bool IsRealHealer(ActorMeter m) =>
        IsRealActor(m) && (m.ActorId == Entities.SelfId || Entities.KindOf(m.ActorId) == EntityKind.Player);

    private EncounterSnapshot Build(long durationMs, bool active, EncounterOutcome? outcome)
    {
        var self = Entities.SelfId;
        var dps = MeterBuilder.Build(MeterKind.Dps, _dps.Values.Where(IsRealActor), durationMs, Entities.NameOf, Entities.ClassOf, _data.SkillName, self, Entities.CombatPowerOf);
        var heal = MeterBuilder.Build(MeterKind.Heal, _heal.Values.Where(IsRealHealer), durationMs, Entities.NameOf, Entities.ClassOf, _data.SkillName, self, Entities.CombatPowerOf);
        var taken = MeterBuilder.Build(MeterKind.Taken, _taken.Values, durationMs, Entities.NameOf, Entities.ClassOf, _data.SkillName, self, Entities.CombatPowerOf);

        // Cibles boss évaluées à l'instantané : les PV max peuvent arriver après les premiers coups.
        MeterSnapshot? bossDps = null;
        var merged = new Dictionary<uint, ActorMeter>();
        foreach (var (target, bySource) in _dpsByTarget)
        {
            if (Entities.MaxHpOf(target) < _options.BossMinHp) continue;
            foreach (var (source, meter) in bySource)
            {
                if (!merged.TryGetValue(source, out var m)) merged[source] = m = new ActorMeter(source);
                m.MergeFrom(meter);
            }
        }
        if (merged.Values.Any(IsRealActor))
            bossDps = MeterBuilder.Build(MeterKind.Dps, merged.Values.Where(IsRealActor), durationMs, Entities.NameOf, Entities.ClassOf, _data.SkillName, self, Entities.CombatPowerOf);

        var dpsTimeline = Timeline.Build(dps, _secondsByTarget, durationMs);
        var bossTimeline = bossDps is null ? null
            : Timeline.Build(bossDps, _secondsByTarget.Where(kv => Entities.MaxHpOf(kv.Key) >= _options.BossMinHp), durationMs);

        var training = active ? IsTraining : outcome == EncounterOutcome.Training;
        string? targetName = null;
        double? hpPct = null;
        var isBoss = false;
        if (MainTarget() is { } t)
        {
            targetName = Entities.NameOf(t);
            var max = Entities.MaxHpOf(t);
            isBoss = max >= _options.BossMinHp;
            if (Entities.CurrentHpOf(t) is { } cur && max > 0) hpPct = cur * 100.0 / max;
        }
        return new EncounterSnapshot(targetName ?? "Combat", _startMs, durationMs, active, outcome, targetName, hpPct,
            dps, heal, taken, Enumerable.Reverse(_deaths).ToList(), _startMapId, isBoss, bossDps, dpsTimeline, bossTimeline,
            MainTarget() is { } nt ? Entities.NpcCodeOf(nt) : 0, training, training ? _trainingDurationMs : 0);
    }
}
