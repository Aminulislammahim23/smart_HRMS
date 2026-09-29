namespace smartHRMS.Tests.Fakes;

/// <summary>A clock the test controls: <see cref="UtcNow"/> is what "now" returns.</summary>
public class FakeTimeProvider : TimeProvider
{
    public FakeTimeProvider(DateTimeOffset utcNow)
    {
        UtcNow = utcNow;
    }

    public DateTimeOffset UtcNow { get; set; }

    public override DateTimeOffset GetUtcNow() => UtcNow;
}
