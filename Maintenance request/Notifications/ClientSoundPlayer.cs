using System.Media;

namespace Maintenance_request.Notifications;

public static class ClientSoundPlayer
{
    #region النغمات الثلاث المدمجة ومعاينتها دون الاعتماد على مجلد التشغيل
    private static readonly Dictionary<short, SoundPlayer> players = [];
    private static readonly object gate = new();
    public static void Play(short choice)
    {
        choice = choice is >= 1 and <= 3 ? choice : (short)2;
        lock (gate)
        {
            if (!players.TryGetValue(choice, out var player))
            {
                string resource = choice == 2 ? "Maintenance.RequestNotification.wav" : $"Maintenance.ClientNotification{choice}.wav";
                var stream = typeof(ClientSoundPlayer).Assembly.GetManifestResourceStream(resource)
                    ?? throw new InvalidOperationException("تعذر تحميل نغمة التنبيه المدمجة.");
                player = new SoundPlayer(stream); player.Load(); players.Add(choice, player);
            }
            // المعاينة تستبدل النغمة السابقة؛ الصوت التلقائي محكوم أيضًا بمنع التكرار في NotificationSound.
            player.Play();
        }
    }
    #endregion
}
