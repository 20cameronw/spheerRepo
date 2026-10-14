using System.Text;
using System.Threading.Tasks;
using AppleAuth;
using AppleAuth.Enums;
using AppleAuth.Interfaces;
using AppleAuth.Native;
using Frontier.Services;
using Frontier.Services.Unity;
using UnityEngine;

namespace Frontier.Services.Unity.Recovery.Apple
{
    sealed class AppleRecoveryCredentialProvider : MonoBehaviour, IRecoveryCredentialProvider
    {
        IAppleAuthManager manager;

        public bool IsConfigured { get { return Application.platform == RuntimePlatform.IPhonePlayer; } }
        public string ProviderName { get { return "Apple Sign-In"; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void RegisterProvider()
        {
            UnityRecoveryCredentialProviderRegistry.Register(CreateProvider);
        }

        static IRecoveryCredentialProvider CreateProvider()
        {
            if (Application.platform != RuntimePlatform.IPhonePlayer)
                return null;

            var host = new GameObject("Frontier Apple Sign-In");
            DontDestroyOnLoad(host);
            return host.AddComponent<AppleRecoveryCredentialProvider>();
        }

        void Update()
        {
            if (manager != null)
                manager.Update();
        }

        public Task<ServiceResult<RecoveryCredential>> AcquireAsync()
        {
            if (!IsConfigured)
                return Task.FromResult(ServiceResult<RecoveryCredential>.NotConfigured(ProviderName));

            if (manager == null)
                manager = new AppleAuthManager(new PayloadDeserializer());

            var completion = new TaskCompletionSource<ServiceResult<RecoveryCredential>>();
            manager.LoginWithAppleId(
                new AppleAuthLoginArgs(LoginOptions.None),
                credential =>
                {
                    var appleCredential = credential as IAppleIDCredential;
                    byte[] token = appleCredential == null ? null : appleCredential.IdentityToken;
                    if (token == null || token.Length == 0)
                    {
                        completion.TrySetResult(ServiceResult<RecoveryCredential>.Failed(
                            "Apple Sign-In did not return an identity token."));
                        return;
                    }

                    completion.TrySetResult(ServiceResult<RecoveryCredential>.Success(
                        new RecoveryCredential(RecoveryProvider.Apple,
                            Encoding.UTF8.GetString(token, 0, token.Length))));
                },
                error => completion.TrySetResult(
                    ServiceResult<RecoveryCredential>.Failed(error.ToString())));
            return completion.Task;
        }
    }
}
