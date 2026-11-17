namespace Maintenance_request.Support;

public sealed record SoundOption(short Value, string Title, string Icon);

public static class ClientChoices
{
    #region خيارات الصوت الثابتة بقيمها القديمة وأيقونة لكل خيار
    public static IReadOnlyList<SoundOption> Sounds { get; } =
    [
        new(1, "إشعار 1", "BellOutline"),
        new(2, "إشعار 2", "BellRingOutline"),
        new(3, "إشعار 3", "MusicNoteOutline")
    ];
    #endregion
}
