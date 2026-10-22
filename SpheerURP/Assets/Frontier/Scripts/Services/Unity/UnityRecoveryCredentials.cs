using System;
using System.Threading.Tasks;
using Frontier.Services;

namespace Frontier.Services.Unity
{
    public enum RecoveryProvider
    {
        Apple,
        GooglePlayGames
    }

    public readonly struct RecoveryCredential
    {
        public readonly RecoveryProvider provider;
        public readonly string token;

        public RecoveryCredential(RecoveryProvider provider, string token)
        {
            this.provider = provider;
            this.token = token ?? string.Empty;
        }
    }

    public interface IRecoveryCredentialProvider
    {
        bool IsConfigured { get; }
        string ProviderName { get; }
        Task<ServiceResult<RecoveryCredential>> AcquireAsync();
    }

    public static class UnityRecoveryCredentialProviderRegistry
    {
        static Func<IRecoveryCredentialProvider> factory;
        static IRecoveryCredentialProvider instance;

        public static void Register(Func<IRecoveryCredentialProvider> providerFactory)
        {
            factory = providerFactory ?? throw new ArgumentNullException(nameof(providerFactory));
            instance = null;
        }

        public static IRecoveryCredentialProvider GetOrCreate()
        {
            if (instance != null)
                return instance;
            if (factory == null)
                return null;

            instance = factory();
            return instance;
        }
    }
}
