namespace AttendanceClock.Tests;

/// <summary>
/// Test classes that use the shared test database run one after another, never in parallel,
/// because each one resets the database when it starts.
/// </summary>
[CollectionDefinition(Name)]
public sealed class DatabaseCollection
{
    public const string Name = "Database";
}
