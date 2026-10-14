using System;
using UnityEngine;

namespace Frontier.Services.Unity
{
    [CreateAssetMenu(menuName = "Frontier/Services Configuration")]
    public sealed class FrontierUnityServicesConfig : ScriptableObject
    {
        [Tooltip("Enable only after legal/audience review, account-deletion UX, leaderboard policy and Unity Dashboard setup are complete.")]
        public bool enableOnlineServices;

        [Tooltip("Enable Cloud Save only after local/cloud conflict handling and account recovery UX are ready.")]
        public bool enableCloudSave;

        [Tooltip("Enable Apple Sign-In or Google Play Games recovery after native provider and dashboard setup are complete.")]
        public bool enableAccountRecovery;

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

        public bool CanUseCloudSave
        {
            get { return CanUseAuthentication && enableCloudSave; }
        }

        public bool CanUseAccountRecovery
        {
            get { return CanUseAuthentication && enableAccountRecovery; }
        }
    }
}
