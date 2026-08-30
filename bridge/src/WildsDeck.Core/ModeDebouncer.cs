namespace WildsDeck.Core;

public sealed class ModeDebouncer(TimeSpan debounce, GameMode initial = GameMode.Unknown)
{
    private GameMode _published = initial;
    private GameMode _candidate = initial;
    private DateTimeOffset _candidateSince = DateTimeOffset.MinValue;

    public GameMode Current => _published;

    public bool Observe(GameMode observed, DateTimeOffset now, out GameMode previous)
    {
        previous = _published;

        if (observed == GameMode.Unknown)
        {
            _candidate = GameMode.Unknown;
            _candidateSince = now;
            return false;
        }

        if (observed == _published)
        {
            _candidate = observed;
            _candidateSince = now;
            return false;
        }

        if (observed != _candidate)
        {
            _candidate = observed;
            _candidateSince = now;
            return false;
        }

        if (now - _candidateSince < debounce)
            return false;

        _published = observed;
        _candidateSince = now;
        return true;
    }
}
