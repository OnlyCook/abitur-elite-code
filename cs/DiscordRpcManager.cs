using DiscordRPC;

namespace AbiturEliteCode
{
    public static class DiscordRpcManager
    {
        private static DiscordRpcClient client;
        private const string ClientId = "1490708551575011470"; // api key to my life savings (no one should see this, not even me)
        private static Timestamps sessionTime;

        public static void Initialize()
        {
            if (client != null && !client.IsDisposed) return;
            client = new DiscordRpcClient(ClientId);
            client.Initialize();
            ResetTimer();
        }

        public static void ResetTimer() => sessionTime = Timestamps.Now;

        public static void UpdatePresence(string details, string state, string smallKey, string smallText)
        {
            if (client == null || client.IsDisposed || !AppSettings.IsDiscordRpcEnabled) return;

            client.SetPresence(new RichPresence()
            {
                Details = details,
                State = state,
                Timestamps = sessionTime,
                Assets = new Assets()
                {
                    LargeImageKey = "aec_app_icon",
                    LargeImageText = "github.com/OnlyCook/abitur-elite-code",
                    SmallImageKey = smallKey,
                    SmallImageText = smallText
                }
            });
        }

        public static void Deinitialize()
        {
            client?.Dispose();
            client = null;
        }
    }
}