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
            int inspected = 0, contextOk = 0, contextErrors = 0, targetMatches = 0;
            int basicOk = 0, basicErrors = 0, plausibleBasic = 0;
            int magicOk = 0, magicErrors = 0, magicMatches = 0;

            foreach (nint address in entries)
            {
                if (inspected >= maximumEntries)
                    break;
                inspected++;
                if (!MemoryAddressResolver.IsValidPointer(address))
                    continue;

                nint? context = TryPath(resolver, address, "Monster::Context");
                if (context is null) contextErrors++;
                else
                {
                    contextOk++;
                    if (context.Value == cameraTarget)
                    {
                        targetMatches++;
                        lines.Add($"TARGET CONTEXT MATCH entry=0x{address:X} context=0x{context.Value:X}");
                    }
                }

                nint? basic = TryPath(resolver, address, "Monster::BasicData");
                if (basic is null) basicErrors++;
                else
                {
                    basicOk++;
                    try
                    {
                        byte enabled = memory.Read<byte>(basic.Value + 0x10);
                        int id = memory.Read<int>(basic.Value + 0x48);
                        int category = memory.Read<int>(basic.Value + 0x54);
                        bool plausible = enabled == 1 && category == 0 && id >= 0 && id < 1000;
                        if (plausible)
                        {
                            plausibleBasic++;
                            lines.Add($"PLAUSIBLE BASIC entry=0x{address:X} basic=0x{basic.Value:X} enabled={enabled} id={id} category={category} context={(context is null ? "ERR" : $"0x{context.Value:X}")} target={context == cameraTarget}");
                            ProbeMonster(memory, resolver, address, lines);
                        }
                    }
                    catch { basicErrors++; }
                }

                nint? magicRaw = TryPath(resolver, address, "Monster::Magic");
                if (magicRaw is null) magicErrors++;
                else
                {
                    magicOk++;
                    int magic = unchecked((int)magicRaw.Value);
                    if (magic == 0x6D0045)
                    {
                        magicMatches++;
                        lines.Add($"MAGIC MATCH entry=0x{address:X} raw=0x{magicRaw.Value:X}");
                    }
                }
            }

            lines.Add($"Inspected={inspected} ContextOk={contextOk} ContextErrors={contextErrors} TargetMatches={targetMatches} BasicOk={basicOk} BasicErrors={basicErrors} PlausibleBasic={plausibleBasic} MagicOk={magicOk} MagicErrors={magicErrors} MagicMatches={magicMatches}");
        }
        catch (Exception exception)
        {
            lines.Add($"FATAL {exception.GetType().Name}: {exception.Message}");
        }

        return lines;
    }

    private static nint? TryPath(MemoryAddressResolver resolver, nint address, string symbol)
    {
        try { return resolver.ResolvePointerPath(address, symbol); }
        catch (Exception exception) when (exception is InvalidDataException or System.ComponentModel.Win32Exception or OverflowException or KeyNotFoundException) { return null; }
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
        catch (Exception exception) { lines.Add($"  Health ERROR {exception.GetType().Name}: {exception.Message}"); }

        foreach (string symbol in new[] { "Monster::Enrage", "Monster::Stamina", "Monster::Ailments", "Monster::Parts", "Monster::Thresholds" })
        {
            try { lines.Add($"  {symbol}=0x{resolver.ResolvePointerPath(monster, symbol):X}"); }
            catch (Exception exception) { lines.Add($"  {symbol} ERROR {exception.GetType().Name}: {exception.Message}"); }
        }
    }
}
