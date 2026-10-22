using System.Threading.Tasks;
using Frontier.Services;
using Frontier.Services.Unity;
using GooglePlayGames;
using GooglePlayGames.BasicApi;
using UnityEngine;

namespace Frontier.Services.Unity.Recovery.GooglePlayGames
{
    sealed class GooglePlayGamesRecoveryCredentialProvider : MonoBehaviour, IRecoveryCredentialProvider
    {
        static bool activated;

        public bool IsConfigured { get { return Application.platform == RuntimePlatform.Android; } }
        public string ProviderName { get { return "Google Play Games"; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void RegisterProvider()
        {
            UnityRecoveryCredentialProviderRegistry.Register(CreateProvider);
        }

        static IRecoveryCredentialProvider CreateProvider()
        {
            if (Application.platform != RuntimePlatform.Android)
                return null;

            var host = new GameObject("Frontier Google Play Games");
            DontDestroyOnLoad(host);
            return host.AddComponent<GooglePlayGamesRecoveryCredentialProvider>();
        }

        public Task<ServiceResult<RecoveryCredential>> AcquireAsync()
        {
            if (!IsConfigured)
                return Task.FromResult(ServiceResult<RecoveryCredential>.NotConfigured(ProviderName));

            if (!activated)
            {
                PlayGamesPlatform.Activate();
                activated = true;
            }

            var completion = new TaskCompletionSource<ServiceResult<RecoveryCredential>>();
            PlayGamesPlatform.Instance.Authenticate(status =>
            {
                if (status != SignInStatus.Success)
                {
                    completion.TrySetResult(ServiceResult<RecoveryCredential>.Failed(
                        "Google Play Games sign-in did not complete: " + status + "."));
                    return;
                }

                PlayGamesPlatform.Instance.RequestServerSideAccess(true, serverAuthCode =>
                {
                    if (string.IsNullOrWhiteSpace(serverAuthCode))
                    {
                        completion.TrySetResult(ServiceResult<RecoveryCredential>.Failed(
                            "Google Play Games did not return an authorization code."));
                        return;
                    }

                    completion.TrySetResult(ServiceResult<RecoveryCredential>.Success(
                        new RecoveryCredential(RecoveryProvider.GooglePlayGames, serverAuthCode)));
                });
            });
            return completion.Task;
        }
    }
}
