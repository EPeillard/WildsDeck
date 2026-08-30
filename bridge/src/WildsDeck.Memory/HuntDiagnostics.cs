namespace WildsDeck.Memory;

public static class HuntDiagnostics
{
    public static IReadOnlyList<string> Probe(WildsProcess process, int maximumEntries = 700)
    {
        var lines = new List<string>();
        ProcessMemoryReader memory = process.Memory;
        MemoryAddressResolver resolver = process.Resolver;

        try
        {
            nint monsterList = resolver.ResolveIndirect("Game::EnemyManager", "Environment::MonsterList");
            nint elements = memory.Read<nint>(monsterList);
            int rawCount = memory.Read<int>(monsterList + 0x8);
            int count = Math.Clamp(rawCount, 0, 700);

            lines.Add($"MonsterList=0x{monsterList:X} Elements=0x{elements:X} RawCount={rawCount} Count={count}");

            nint cameraTarget = 0;
            try
            {
                nint cameraAddress = resolver.ResolveIndirect("Game::CameraManager", "Camera::Monster::Target");
                cameraTarget = memory.Read<nint>(cameraAddress);
                lines.Add($"CameraTarget=0x{cameraTarget:X}");
            }
            catch (Exception exception)
            {
                lines.Add($"CameraTarget ERROR {exception.GetType().Name}: {exception.Message}");
            }

            if (!MemoryAddressResolver.IsValidPointer(elements) || count == 0)
            {
                lines.Add("No readable monster entries.");
                return lines;
            }

            nint[] entries = memory.ReadArray<nint>(elements + 0x20, count);
            int inspected = 0;
            int accepted = 0;
            int invalidEntryPointers = 0;
            int pointerPathErrors = 0;
            int wrongMagic = 0;
            int disabled = 0;
            int wrongCategory = 0;
            int invalidIds = 0;

            foreach (nint address in entries)
            {
                if (inspected >= maximumEntries)
                    break;
                inspected++;

                if (!MemoryAddressResolver.IsValidPointer(address))
                {
                    invalidEntryPointers++;
                    continue;
                }

                try
                {
                    nint magicRaw = resolver.ResolvePointerPath(address, "Monster::Magic");
                    int magic = unchecked((int)magicRaw);
                    if (magic != 0x6D0045)
                    {
                        wrongMagic++;
                        continue;
                    }

                    nint basic = resolver.ResolvePointerPath(address, "Monster::BasicData");
                    byte enabled = memory.Read<byte>(basic + 0x10);
                    int id = memory.Read<int>(basic + 0x48);
                    int category = memory.Read<int>(basic + 0x54);

                    if (enabled != 1) disabled++;
                    if (category != 0) wrongCategory++;
                    if (id < 0) invalidIds++;

                    nint context = resolver.ResolvePointerPath(address, "Monster::Context");
                    bool target = context == cameraTarget;
                    bool valid = enabled == 1 && category == 0 && id >= 0;

                    lines.Add($"MAGIC MATCH 0x{address:X}: magicRaw=0x{magicRaw:X} enabled={enabled} id={id} category={category} context=0x{context:X} target={target} VALID={valid}");

                    if (!valid)
                        continue;

                    accepted++;
                    ProbeMonster(memory, resolver, address, lines);
                }
                catch (Exception exception) when (exception is InvalidDataException or System.ComponentModel.Win32Exception or OverflowException or KeyNotFoundException)
                {
                    pointerPathErrors++;
                }
            }

            lines.Add($"Inspected={inspected} Accepted={accepted} InvalidEntryPointers={invalidEntryPointers} PointerPathErrors={pointerPathErrors} WrongMagic={wrongMagic} Disabled={disabled} WrongCategory={wrongCategory} InvalidIds={invalidIds}");
        }
        catch (Exception exception)
        {
            lines.Add($"FATAL {exception.GetType().Name}: {exception.Message}");
        }

        return lines;
    }

    private static void ProbeMonster(ProcessMemoryReader memory, MemoryAddressResolver resolver, nint monster, List<string> lines)
    {
        try
        {
            nint health = resolver.ResolvePointerPath(monster, "Monster::Health");
            nint current = memory.Read<nint>(health + 0x10);
            nint maximum = memory.Read<nint>(health + 0x18);
            lines.Add($"  Health=0x{health:X} currentPtr=0x{current:X} maxPtr=0x{maximum:X}");
        }
        catch (Exception exception)
        {
            lines.Add($"  Health ERROR {exception.GetType().Name}: {exception.Message}");
        }

        foreach (string symbol in new[] { "Monster::Enrage", "Monster::Stamina", "Monster::Ailments", "Monster::Parts", "Monster::Thresholds" })
        {
            try
            {
                nint value = resolver.ResolvePointerPath(monster, symbol);
                lines.Add($"  {symbol}=0x{value:X}");
            }
            catch (Exception exception)
            {
                lines.Add($"  {symbol} ERROR {exception.GetType().Name}: {exception.Message}");
            }
        }
    }
}
