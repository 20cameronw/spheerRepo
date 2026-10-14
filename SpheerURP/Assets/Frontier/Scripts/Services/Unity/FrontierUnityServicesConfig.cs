using System;
using UnityEngine;

namespace Frontier.Services.Unity
{
    [CreateAssetMenu(menuName = "Frontier/Services Configuration")]
    public sealed class FrontierUnityServicesConfig : ScriptableObject
    {
        [Tooltip("Enable only after legal/audience review, account-deletion UX, leaderboard policy and Unity Dashboard setup are complete.")]
        public bool enableOnlineServices;

        [Tooltip("UGS leaderboard ID created in the Unity Dashboard.")]
        public string leaderboardId;

        [Tooltip("Public HTTPS privacy-policy URL shown in Settings before online-service opt-in.")]
        public string privacyPolicyUrl;

        public bool HasValidPrivacyPolicy
        {
            get
            {
                Uri uri;
                return Uri.TryCreate(privacyPolicyUrl, UriKind.Absolute, out uri) &&
                    uri.Scheme == Uri.UriSchemeHttps;
            }
        }

        public bool CanUseAuthentication
        {
            get
            {
                return enableOnlineServices && !string.IsNullOrWhiteSpace(Application.cloudProjectId) &&
                    HasValidPrivacyPolicy;
            }
        }

        public bool CanUseLeaderboard
        {
            get { return CanUseAuthentication && !string.IsNullOrWhiteSpace(leaderboardId); }
        }
    }
}
