using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Frontier.Services;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Leaderboards;
using Unity.Services.Leaderboards.Models;
using UgsLeaderboardEntry = Unity.Services.Leaderboards.Models.LeaderboardEntry;
using UnityEngine;

namespace Frontier.Services.Unity
{
    sealed class UnityServicesInitializer
    {
        readonly FrontierUnityServicesConfig config;
        Task<ServiceResult> initializationTask;

        public UnityServicesInitializer(FrontierUnityServicesConfig config)
        {
            this.config = config;
        }

        public Task<ServiceResult> InitializeAsync()
        {
            if (config == null || !config.CanUseAuthentication)
                return Task.FromResult(ServiceResult.NotConfigured("Unity Gaming Services"));

            if (UnityServices.State == ServicesInitializationState.Initialized)
                return Task.FromResult(ServiceResult.Success());

            if (initializationTask == null || initializationTask.IsCompleted)
                initializationTask = InitializeCoreAsync();
            return initializationTask;
        }

        async Task<ServiceResult> InitializeCoreAsync()
        {
            try
            {
                await UnityServices.InitializeAsync();
                return ServiceResult.Success();
            }
            catch (ServicesInitializationException exception)
            {
                Debug.LogWarning("Unity Gaming Services initialization failed: " + exception.Message);
                return ServiceResult.Failed(exception.Message);
            }
            catch (RequestFailedException exception)
            {
                Debug.LogWarning("Unity Gaming Services initialization failed: " + exception.Message);
                return ServiceResult.Failed(exception.Message);
            }
        }
    }

    sealed class UnityAuthenticationProvider : IAuthService
    {
        readonly FrontierUnityServicesConfig config;
        readonly UnityServicesInitializer initializer;

        public UnityAuthenticationProvider(FrontierUnityServicesConfig config, UnityServicesInitializer initializer)
        {
            this.config = config;
            this.initializer = initializer;
        }

        public bool IsConfigured { get { return config != null && config.CanUseAuthentication; } }

        public Task<ServiceResult> InitializeAsync()
        {
            return initializer.InitializeAsync();
        }

        public async Task<ServiceResult<AuthIdentity>> SignInAnonymouslyAsync()
        {
            if (!IsConfigured)
                return ServiceResult<AuthIdentity>.NotConfigured("Unity Authentication");

            ServiceResult initialized = await InitializeAsync();
            if (!initialized.succeeded)
                return ServiceResult<AuthIdentity>.Failed(initialized.message);

            try
            {
                var authentication = AuthenticationService.Instance;
                if (!authentication.IsSignedIn)
                    await authentication.SignInAnonymouslyAsync();

                bool anonymous = authentication.PlayerInfo != null &&
                    authentication.PlayerInfo.Identities.Count == 0;
                return ServiceResult<AuthIdentity>.Success(
                    new AuthIdentity(authentication.PlayerId, anonymous));
            }
            catch (RequestFailedException exception)
            {
                Debug.LogWarning("Unity Authentication sign-in failed: " + exception.Message);
                return ServiceResult<AuthIdentity>.Failed(exception.Message);
            }
        }

        public async Task<ServiceResult> DeleteAccountAsync()
        {
            if (!IsConfigured)
                return ServiceResult.NotConfigured("Unity Authentication");

            ServiceResult initialized = await InitializeAsync();
            if (!initialized.succeeded)
                return initialized;

            try
            {
                var authentication = AuthenticationService.Instance;
                if (!authentication.IsSignedIn)
                    return ServiceResult.InvalidRequest("There is no signed-in account to delete.");
                await authentication.DeleteAccountAsync();
                return ServiceResult.Success();
            }
            catch (RequestFailedException exception)
            {
                Debug.LogWarning("Unity Authentication account deletion failed: " + exception.Message);
                return ServiceResult.Failed(exception.Message);
            }
        }
    }

    sealed class UnityLeaderboardProvider : ILeaderboardService
    {
        const int MaximumPageSize = 100;
        const long MaximumExactIntegerScore = 9007199254740991L;

        readonly FrontierUnityServicesConfig config;
        readonly UnityServicesInitializer initializer;

        public UnityLeaderboardProvider(FrontierUnityServicesConfig config, UnityServicesInitializer initializer)
        {
            this.config = config;
            this.initializer = initializer;
        }

        public bool IsConfigured { get { return config != null && config.CanUseLeaderboard; } }

        public Task<ServiceResult> InitializeAsync()
        {
            return IsConfigured
                ? initializer.InitializeAsync()
                : Task.FromResult(ServiceResult.NotConfigured("Unity Leaderboards"));
        }

        public async Task<ServiceResult<IReadOnlyList<Frontier.Services.LeaderboardEntry>>> GetScoresAsync(
            string boardId, int limit)
        {
            if (!IsConfigured)
                return ServiceResult<IReadOnlyList<Frontier.Services.LeaderboardEntry>>.NotConfigured("Unity Leaderboards");
            if (!string.Equals(boardId, config.leaderboardId, StringComparison.Ordinal))
                return ServiceResult<IReadOnlyList<Frontier.Services.LeaderboardEntry>>.InvalidRequest(
                    "The requested leaderboard does not match the configured board.");
            if (limit < 1 || limit > MaximumPageSize)
                return ServiceResult<IReadOnlyList<Frontier.Services.LeaderboardEntry>>.InvalidRequest(
                    "Leaderboard page size must be between 1 and " + MaximumPageSize + ".");

            ServiceResult initialized = await initializer.InitializeAsync();
            if (!initialized.succeeded)
                return ServiceResult<IReadOnlyList<Frontier.Services.LeaderboardEntry>>.Failed(initialized.message);

            try
            {
                if (!AuthenticationService.Instance.IsSignedIn)
                    return ServiceResult<IReadOnlyList<Frontier.Services.LeaderboardEntry>>.InvalidRequest(
                        "Sign in before loading leaderboard scores.");
                LeaderboardScoresPage page = await LeaderboardsService.Instance.GetScoresAsync(
                    config.leaderboardId, new GetScoresOptions { Limit = limit });
                var entries = new List<Frontier.Services.LeaderboardEntry>(
                    page.Results == null ? 0 : page.Results.Count);
                if (page.Results != null)
                {
                    foreach (UgsLeaderboardEntry entry in page.Results)
                    {
                        if (entry.Score < 0 || entry.Score > MaximumExactIntegerScore ||
                            entry.Score != Math.Truncate(entry.Score))
                            return ServiceResult<IReadOnlyList<Frontier.Services.LeaderboardEntry>>.Failed(
                                "The configured leaderboard contains a score outside the supported integer range.");

                        entries.Add(new Frontier.Services.LeaderboardEntry(
                            entry.PlayerId,
                            string.IsNullOrWhiteSpace(entry.PlayerName) ? "Unknown pilot" : entry.PlayerName,
                            long.Parse(entry.Score.ToString("0", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture),
                            entry.Rank));
                    }
                }

                return ServiceResult<IReadOnlyList<Frontier.Services.LeaderboardEntry>>.Success(entries);
            }
            catch (ServicesInitializationException exception)
            {
                Debug.LogWarning("Unity Leaderboards request failed: " + exception.Message);
                return ServiceResult<IReadOnlyList<Frontier.Services.LeaderboardEntry>>.Failed(exception.Message);
            }
            catch (RequestFailedException exception)
            {
                Debug.LogWarning("Unity Leaderboards request failed: " + exception.Message);
                return ServiceResult<IReadOnlyList<Frontier.Services.LeaderboardEntry>>.Failed(exception.Message);
            }
        }

        public async Task<ServiceResult> SubmitScoreAsync(string boardId, long score)
        {
            if (!IsConfigured)
                return ServiceResult.NotConfigured("Unity Leaderboards");
            if (!string.Equals(boardId, config.leaderboardId, StringComparison.Ordinal))
                return ServiceResult.InvalidRequest("The requested leaderboard does not match the configured board.");
            if (score < 0 || score > MaximumExactIntegerScore)
                return ServiceResult.InvalidRequest("Score must be a non-negative integer within the supported range.");

            ServiceResult initialized = await initializer.InitializeAsync();
            if (!initialized.succeeded)
                return initialized;

            try
            {
                if (!AuthenticationService.Instance.IsSignedIn)
                    return ServiceResult.InvalidRequest("Sign in before submitting a leaderboard score.");
                await LeaderboardsService.Instance.AddPlayerScoreAsync(config.leaderboardId, score);
                return ServiceResult.Success();
            }
            catch (ServicesInitializationException exception)
            {
                Debug.LogWarning("Unity Leaderboards submission failed: " + exception.Message);
                return ServiceResult.Failed(exception.Message);
            }
            catch (RequestFailedException exception)
            {
                Debug.LogWarning("Unity Leaderboards submission failed: " + exception.Message);
                return ServiceResult.Failed(exception.Message);
            }
        }
    }

    public static class FrontierUnityServicesFactory
    {
        public static FrontierServices Create(IConsentService consent, FrontierUnityServicesConfig config)
        {
            var unavailable = new UnavailableFrontierProvider();
            var initializer = new UnityServicesInitializer(config);
            var authentication = new UnityAuthenticationProvider(config, initializer);
            var leaderboards = new UnityLeaderboardProvider(config, initializer);
            return new FrontierServices(consent, unavailable, unavailable, authentication, leaderboards,
                unavailable, unavailable, unavailable, new LocalTimeService(), unavailable,
                config == null ? null : config.leaderboardId,
                config != null && config.HasValidPrivacyPolicy ? config.privacyPolicyUrl : null);
        }
    }
}
