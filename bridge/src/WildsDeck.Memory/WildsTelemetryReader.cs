using WildsDeck.Core;

namespace WildsDeck.Memory;

public sealed class WildsTelemetryReader
{
    private const int MaximumMonsters = 700;
    private const int MaximumParts = 32;
    private const int MaximumAilments = 30;
    private const int MaterialCollectorCapacity = 16;

    private readonly WildsProcess _process;
    private readonly ProcessMemoryReader _memory;
    private readonly MemoryAddressResolver _resolver;
    private readonly WildsCrypto _crypto;

    public WildsTelemetryReader(WildsProcess process)
    {
        _process = process;
        _memory = process.Memory;
        _resolver = process.Resolver;
        _crypto = new WildsCrypto(_memory, _resolver);
    }

    public WildsState ReadState()
    {
        QuestState? quest = Try(ReadQuest);
        GameMode mode = quest?.Active switch
        {
            true => GameMode.Hunt,
            false => GameMode.Town,
            _ => GameMode.Unknown
        };

        PlayerSnapshot? playerSnapshot = Try(ReadPlayerSnapshot);
        IReadOnlyList<PartyMemberState> party = Try(() => ReadParty(playerSnapshot)) ?? [];
        PlayerState? player = playerSnapshot is null ? null : new PlayerState
        {
            Name = playerSnapshot.Name,
            WeaponType = playerSnapshot.WeaponType,
            DamageTotal = playerSnapshot.Damage,
            DamagePartySharePercent = TelemetryMath.Share(playerSnapshot.Damage, party.Select(static member => member.Damage)),
            Attack = playerSnapshot.Attack,
            Affinity = playerSnapshot.Affinity
        };

        return new WildsState
        {
            Connected = true,
            GameVersion = _process.Version,
            MapFile = Path.GetFileName(_process.MapPath),
            Mode = mode,
            Timestamp = DateTimeOffset.UtcNow,
            Quest = quest,
            Player = player,
            Party = party,
            Monster = mode == GameMode.Hunt ? Try(ReadSelectedMonster) : null,
            Town = Try(ReadTown)
        };
    }

    private QuestState ReadQuest()
    {
        nint questAddress = _resolver.ResolveIndirect("Game::QuestManager", "Quest::Data");
        nint informationAddress = _resolver.ResolveIndirect("Game::QuestManager", "Quest::CurrentInformation");
        nint questPointer = _memory.Read<nint>(questAddress);
        nint informationPointer = _memory.Read<nint>(informationAddress);

        if (!MemoryAddressResolver.IsValidPointer(questPointer) || !MemoryAddressResolver.IsValidPointer(informationPointer))
            return new QuestState { Active = false };

        float elapsed = _memory.Read<float>(informationPointer + 0xE0);
        float maximum = _memory.Read<float>(informationPointer + 0xE4);
        int success = _memory.Read<int>(informationPointer + 0x108);
        int failure = _memory.Read<int>(informationPointer + 0x114);
        bool timersValid = float.IsFinite(elapsed) && float.IsFinite(maximum) && elapsed >= 0 && maximum > 0 && elapsed <= maximum + 60_000;
        bool active = timersValid && success == 0 && failure == 0;

        return new QuestState
        {
            Active = active,
            Id = _memory.Read<int>(questPointer + 0x38),
            ElapsedSeconds = timersValid ? elapsed / 1000f : null,
            MaxSeconds = timersValid ? maximum / 1000f : null,
            SuccessState = success,
            FailureState = failure
        };
    }

    private MonsterState? ReadSelectedMonster()
    {
        nint monsterList = _resolver.ResolveIndirect("Game::EnemyManager", "Environment::MonsterList");
        nint elements = _memory.Read<nint>(monsterList);
        int count = Math.Clamp(_memory.Read<int>(monsterList + 0x8), 0, MaximumMonsters);
        if (!MemoryAddressResolver.IsValidPointer(elements) || count == 0)
            return null;

        nint cameraTarget = TryValue(ReadCameraTarget) ?? 0;
        var valid = new List<MonsterCandidate>(Math.Min(count, 8));
        foreach (nint address in _memory.ReadArray<nint>(elements + 0x20, count))
        {
            if (!MemoryAddressResolver.IsValidPointer(address))
                continue;

            MonsterCandidate? candidate = Try(() => ReadMonsterCandidate(address, cameraTarget));
            if (candidate is not null)
                valid.Add(candidate);
        }

        MonsterCandidate? selected = valid.FirstOrDefault(static candidate => candidate.IsCameraTarget)
            ?? valid.FirstOrDefault();
        return selected is null ? null : ReadMonster(selected);
    }

    private nint ReadCameraTarget()
    {
        nint address = _resolver.ResolveIndirect("Game::CameraManager", "Camera::Monster::Target");
        return _memory.Read<nint>(address);
    }

    private MonsterCandidate? ReadMonsterCandidate(nint address, nint cameraTarget)
    {
        // Monster::Magic is currently unusable on Wilds 1.42.0.2: every entry's
        // pointer path resolves through a null pointer, while BasicData and Context
        // remain stable and match HunterPie's structures. Validate candidates from
        // BasicData instead and use Context to identify the camera-selected monster.
        nint basic = _resolver.ResolvePointerPath(address, "Monster::BasicData");
        byte enabled = _memory.Read<byte>(basic + 0x10);
        int id = _memory.Read<int>(basic + 0x48);
        int category = _memory.Read<int>(basic + 0x54);
        if (enabled != 1 || category != 0 || id < 0)
            return null;

        nint context = _resolver.ResolvePointerPath(address, "Monster::Context");
        return new MonsterCandidate(address, id, context == cameraTarget);
    }

    private MonsterState ReadMonster(MonsterCandidate candidate)
    {
        GaugeState? health = Try(() => ReadHealth(candidate.Address));
        GaugeState? stamina = Try(() => ReadStamina(candidate.Address));
        EnrageState? enrage = Try(() => ReadEnrage(candidate.Address));
        float? captureThreshold = TryValue(() => ReadCaptureThreshold(candidate.Address));
        bool? captureReady = health?.Current is not null && health.Max > 0 && captureThreshold > 0
            ? health.Current.Value / health.Max.Value <= captureThreshold.Value
            : null;

        return new MonsterState
        {
            Id = candidate.Id,
            Name = WildsKnowledgeBase.MonsterName(candidate.Id),
            Selection = candidate.IsCameraTarget ? "cameraTarget" : "firstValidLargeMonster",
            Health = health,
            Stamina = stamina,
            Enrage = enrage,
            CaptureThreshold = captureThreshold,
            CaptureReady = captureReady,
            Parts = Try(() => ReadParts(candidate.Address)) ?? [],
            Ailments = Try(() => ReadAilments(candidate.Address)) ?? []
        };
    }

    private GaugeState ReadHealth(nint monster)
    {
        nint component = _resolver.ResolvePointerPath(monster, "Monster::Health");
        nint currentPointer = _memory.Read<nint>(component + 0x10);
        nint maximumPointer = _memory.Read<nint>(component + 0x18);
        return new GaugeState
        {
            Current = ValidFloat(_crypto.DecryptFloat(currentPointer)),
            Max = ValidFloat(_crypto.DecryptFloat(maximumPointer))
        };
    }

    private GaugeState ReadStamina(nint monster)
    {
        AilmentSnapshot ailment = ReadAilmentSnapshot(_resolver.ResolvePointerPath(monster, "Monster::Stamina"));
        float current = ailment.Active ? ailment.TimerMax - ailment.Timer : ailment.BuildUpCurrent;
        float maximum = ailment.Active ? ailment.TimerMax : ailment.BuildUpMax;
        return new GaugeState { Current = ValidFloat(current), Max = ValidFloat(maximum) };
    }

    private EnrageState ReadEnrage(nint monster)
    {
        AilmentSnapshot ailment = ReadAilmentSnapshot(_resolver.ResolvePointerPath(monster, "Monster::Enrage"));
        return new EnrageState
        {
            Active = ailment.Timer > 0,
            Value = ValidFloat(ailment.BuildUpCurrent),
            Max = ValidFloat(ailment.BuildUpMax),
            Timer = ValidFloat(ailment.Timer),
            MaxTimer = ValidFloat(ailment.TimerMax)
        };
    }

    private float ReadCaptureThreshold(nint monster)
    {
        nint thresholds = _resolver.ResolvePointerPath(monster, "Monster::Thresholds");
        float value = _memory.Read<float>(thresholds + 0x20);
        if (!float.IsFinite(value) || value < 0 || value > 1)
            throw new InvalidDataException($"Invalid capture threshold {value}.");
        return value;
    }

    private IReadOnlyList<AilmentState> ReadAilments(nint monster)
    {
        nint array = _resolver.ResolvePointerPath(monster, "Monster::Ailments");
        var result = new List<AilmentState>();
        foreach (nint pointer in ReadPointerArray(array, MaximumAilments))
        {
            AilmentSnapshot? snapshot = Try(() => ReadAilmentSnapshot(pointer));
            if (snapshot is null)
                continue;

            result.Add(new AilmentState
            {
                Id = snapshot.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Name = WildsKnowledgeBase.AilmentName(snapshot.Id),
                Active = snapshot.Active,
                Current = ValidFloat(snapshot.BuildUpCurrent),
                Max = ValidFloat(snapshot.BuildUpMax),
                Timer = ValidFloat(snapshot.Timer),
                MaxTimer = ValidFloat(snapshot.TimerMax)
            });
        }
        return result;
    }

    private AilmentSnapshot ReadAilmentSnapshot(nint address)
    {
        MemoryAddressResolver.EnsureValid(address);
        nint buildUp = _memory.Read<nint>(address + 0x20);
        MemoryAddressResolver.EnsureValid(buildUp);
        return new AilmentSnapshot(
            _memory.Read<int>(address + 0xB4),
            _memory.Read<int>(address + 0x58) == 1,
            _memory.Read<float>(address + 0x48),
            _memory.Read<float>(address + 0x4C),
            _memory.Read<float>(buildUp + 0x14),
            _memory.Read<float>(buildUp + 0x1C));
    }

    private IReadOnlyList<MonsterPartState> ReadParts(nint monster)
    {
        nint data = _resolver.ResolvePointerPath(monster, "Monster::Parts");
        var healthPointers = ReadPointerArray(_memory.Read<nint>(data + 0x78), MaximumParts).ToList();
        var breakPointers = ReadPointerArray(_memory.Read<nint>(data + 0x20), healthPointers.Count).ToList();

        nint severableArray = _memory.Read<nint>(data + 0x18);
        nint severableBreakArray = _memory.Read<nint>(data + 0x28);
        if (MemoryAddressResolver.IsValidPointer(severableArray) && MemoryAddressResolver.IsValidPointer(severableBreakArray))
        {
            List<nint> severable = ReadPointerArray(severableArray, MaximumParts - healthPointers.Count).ToList();
            healthPointers.AddRange(severable);
            breakPointers.AddRange(ReadPointerArray(severableBreakArray, severable.Count));
        }

        int count = Math.Min(healthPointers.Count, breakPointers.Count);
        var parts = new List<MonsterPartState>(count);
        for (int index = 0; index < count; index++)
        {
            MonsterPartState? part = Try(() => ReadPart(index, healthPointers[index], breakPointers[index]));
            if (part is not null)
                parts.Add(part);
        }
        return parts;
    }

    private MonsterPartState ReadPart(int index, nint healthAddress, nint breakAddress)
    {
        nint maxPointer = _memory.Read<nint>(healthAddress + 0x10);
        nint currentPointer = _memory.Read<nint>(healthAddress + 0x28);
        float rawMax = _crypto.DecryptFloat(maxPointer);
        float rawCurrent = _crypto.DecryptFloat(currentPointer);
        int resetCount = _memory.Read<int>(healthAddress + 0x58);
        int maxBreaks = _memory.Read<int>(breakAddress + 0x10);
        int multiplier = _memory.Read<int>(breakAddress + 0x14);
        int breaks = _memory.Read<int>(breakAddress + 0x18);
        bool severable = _memory.Read<byte>(breakAddress + 0x31) != 0;
        bool enabled = _memory.Read<byte>(breakAddress + 0x32) != 0;
        bool breakable = enabled && !severable;
        int normalized = Math.Max(0, multiplier - 1 - resetCount);

        float current;
        float maximum;
        string type;
        if (severable)
        {
            type = "severable";
            maximum = rawMax * multiplier * maxBreaks;
            current = rawMax * normalized + rawCurrent;
        }
        else if (breakable)
        {
            type = "breakable";
            maximum = rawMax * multiplier;
            current = breaks >= maxBreaks && maxBreaks > 0
                ? maximum
                : rawMax * normalized + rawCurrent;
        }
        else
        {
            // HunterPie exposes raw flinch health for non-breakable parts. The
            // break-association fields are not meaningful here and are often zero.
            type = "flinch";
            maximum = rawMax;
            current = rawCurrent;
        }

        return new MonsterPartState
        {
            Id = index.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Name = $"Part {index + 1}",
            Type = type,
            Current = ValidFloat(current),
            Max = ValidFloat(maximum),
            Breakable = breakable,
            Severable = severable,
            Broken = breakable ? breaks >= maxBreaks && maxBreaks > 0 : null,
            BreakCount = breakable ? breaks : resetCount,
            MaxBreaks = enabled && maxBreaks > 0 ? maxBreaks : null,
            ResetCount = resetCount,
            BreakMultiplier = enabled && multiplier > 0 ? multiplier : null
        };
    }

    private PlayerSnapshot ReadPlayerSnapshot()
    {
        nint localAddress = _resolver.ResolveIndirect("Game::PlayerManager", "Player::Local");
        nint local = _memory.Read<nint>(localAddress);
        MemoryAddressResolver.EnsureValid(local);
        nint context = _resolver.ResolvePointerPath(local, "Player::Context");
        nint namePointer = _memory.Read<nint>(context + 0x38);
        string? name = MemoryAddressResolver.IsValidPointer(namePointer) ? _memory.ReadWildsString(namePointer) : null;
        nint gear = _resolver.ResolvePointerPath(local, "Player::Gear");
        string? weapon = WildsKnowledgeBase.WeaponName(_memory.Read<byte>(gear + 0xC8));
        float? damage = TryValue(ReadLocalDamage);
        (float? Attack, float? Affinity) status = Try(() => ReadPlayerStatus(local));
        return new PlayerSnapshot(local, name, weapon, damage, status.Attack, status.Affinity);
    }

    private (float? Attack, float? Affinity) ReadPlayerStatus(nint local)
    {
        nint damageStatus = _resolver.ResolvePointerPath(local, "Player::Status::Damage");
        nint affinityStatus = _resolver.ResolvePointerPath(local, "Player::Status::Affinity");
        nint attackPointer = _memory.Read<nint>(damageStatus + 0x18);
        nint affinityPointer = _memory.Read<nint>(affinityStatus + 0x20);
        return (ValidFloat(_crypto.DecryptFloat(attackPointer)), ValidFloat(_crypto.DecryptFloat(affinityPointer)));
    }

    private float ReadLocalDamage()
    {
        nint address = _resolver.ResolveIndirect("Game::QuestManager", "Quest::LocalPlayer::Damage");
        float damage = _memory.Read<float>(address);
        if (!float.IsFinite(damage) || damage < 0)
            throw new InvalidDataException($"Invalid player damage {damage}.");
        return damage;
    }

    private IReadOnlyList<PartyMemberState> ReadParty(PlayerSnapshot? player)
    {
        if (player is null)
            return [];

        var raw = new List<(string? Name, string? Weapon, float Damage, bool Local)>();
        if (player.Damage is >= 0)
            raw.Add((player.Name, player.WeaponType, player.Damage.Value, true));

        nint remoteDamage = _resolver.ResolveIndirect("Game::QuestManager", "Quest::RemotePlayer::Damage");
        for (int index = 1; index < 4; index++)
        {
            float damage = _memory.Read<float>(remoteDamage + index * 0x78);
            if (float.IsFinite(damage) && damage > 0)
                raw.Add((null, null, damage, false));
        }

        float total = raw.Sum(static member => member.Damage);
        return raw.Select(member => new PartyMemberState
        {
            Name = member.Name,
            WeaponType = member.Weapon,
            Damage = member.Damage,
            DamageSharePercent = total > 0 ? member.Damage / total * 100f : null,
            IsLocalPlayer = member.Local
        }).ToArray();
    }

    private TownState ReadTown()
    {
        int? rank = TryValue(() => _memory.Read<int>(_resolver.ResolveIndirect("Game::SaveManager", "Save::Player::HunterRank")));
        nint saveAddress = ReadSaveAddress();
        return new TownState
        {
            HunterRank = rank,
            SupportShip = Try(() => ReadSupportShip(saveAddress)),
            IngredientsCenter = Try(() => ReadIngredientsCenter(saveAddress)),
            MaterialCollectors = Try(() => ReadMaterialCollectors(saveAddress)) ?? [],
            NpcNotification = null,
            Npcs = []
        };
    }

    private nint ReadSaveAddress()
    {
        int index = _memory.Read<int>(_resolver.ResolveIndirect("Game::SaveManager", "Save::Index"));
        if (index < 0 || index > 8)
            throw new InvalidDataException($"Invalid save index {index}.");
        nint array = _resolver.ResolveIndirect("Game::SaveManager", "Save::Data");
        nint save = _memory.Read<nint>(array + index * sizeof(long));
        MemoryAddressResolver.EnsureValid(save);
        return save;
    }

    private ActivityState ReadSupportShip(nint save)
    {
        nint context = _resolver.ResolvePointerPath(save, "Activities::SupportShip");
        int days = _memory.Read<byte>(context + 0x23);
        bool inTown = _memory.Read<byte>(context + 0x24) == 1;
        return new ActivityState
        {
            Available = true,
            Ready = inTown,
            Current = days,
            Status = inTown ? "In town" : $"{days} day(s)",
            Support = SupportStatus.Supported
        };
    }

    private ActivityState ReadIngredientsCenter(nint save)
    {
        nint context = _resolver.ResolvePointerPath(save, "Activities::IngredientsCenter");
        float timer = _memory.Read<float>(context + 0x40);
        int count = _memory.Read<short>(context + 0x44);
        return new ActivityState
        {
            Available = true,
            Ready = count >= 10,
            Current = Math.Clamp(count, 0, 10),
            Max = 10,
            Timer = ValidFloat(timer),
            Status = $"{Math.Clamp(count, 0, 10)}/10",
            Support = SupportStatus.Supported
        };
    }

    private IReadOnlyList<MaterialCollectorState> ReadMaterialCollectors(nint save)
    {
        nint sources = _resolver.ResolvePointerPath(save, "Activities::MaterialRetrieval");
        var collectors = new List<(int Order, MaterialCollectorState State)>();

        foreach (nint collector in ReadPointerArray(sources, 12))
        {
            uint rawId = _memory.Read<uint>(collector + 0x2C);
            (string Id, string Name, int Order)? definition = MaterialCollectorDefinition(rawId);
            if (definition is null)
                continue;

            nint items = _memory.Read<nint>(collector + 0x10);
            int count = ReadPointerArray(items, MaterialCollectorCapacity)
                .Count(item => _memory.Read<short>(item + 0x12) > 0);

            collectors.Add((definition.Value.Order, new MaterialCollectorState
            {
                Id = definition.Value.Id,
                Name = definition.Value.Name,
                Current = Math.Clamp(count, 0, MaterialCollectorCapacity),
                Max = MaterialCollectorCapacity
            }));
        }

        return collectors
            .GroupBy(static item => item.State.Id, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .OrderBy(static item => item.Order)
            .Select(static item => item.State)
            .ToArray();
    }

    private static (string Id, string Name, int Order)? MaterialCollectorDefinition(uint id) => id switch
    {
        0x8552AD80 => ("rysher", "Rysher", 0),
        0x00000023 => ("murtabak", "Murtabak", 1),
        0x251E0440 => ("apar", "Apar", 2),
        0x3F8E9480 => ("plumpeach", "Plumpeach", 3),
        0x5CE6D780 => ("sabar", "Sabar", 4),
        _ => null
    };

    private IEnumerable<nint> ReadPointerArray(nint address, int maximum)
    {
        if (!MemoryAddressResolver.IsValidPointer(address) || maximum <= 0)
            yield break;

        int count = Math.Clamp(_memory.Read<int>(address + 0x1C), 0, maximum);
        foreach (nint pointer in _memory.ReadArray<nint>(address + 0x20, count))
        {
            if (MemoryAddressResolver.IsValidPointer(pointer))
                yield return pointer;
        }
    }

    private static float? ValidFloat(float value) => float.IsFinite(value) && value >= 0 ? value : null;

    private static T? Try<T>(Func<T> read)
    {
        try
        {
            return read();
        }
        catch (Exception exception) when (exception is InvalidDataException or System.ComponentModel.Win32Exception or OverflowException or KeyNotFoundException)
        {
            return default;
        }
    }

    private static T? TryValue<T>(Func<T> read) where T : struct
    {
        try
        {
            return read();
        }
        catch (Exception exception) when (exception is InvalidDataException or System.ComponentModel.Win32Exception or OverflowException or KeyNotFoundException)
        {
            return null;
        }
    }

    private sealed record MonsterCandidate(nint Address, int Id, bool IsCameraTarget);
    private sealed record AilmentSnapshot(int Id, bool Active, float Timer, float TimerMax, float BuildUpCurrent, float BuildUpMax);
    private sealed record PlayerSnapshot(nint Address, string? Name, string? WeaponType, float? Damage, float? Attack, float? Affinity);
}
