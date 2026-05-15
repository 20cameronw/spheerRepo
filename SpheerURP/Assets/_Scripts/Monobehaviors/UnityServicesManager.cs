using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;

public class UnityServicesManager : MonoBehaviour
{
    public static UnityServicesManager Instance { get; private set; }

    public bool IsInitialized { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Initializes Unity Services and signs in anonymously.
    /// Safe to call multiple times — subsequent calls are no-ops.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (IsInitialized) return;

        await UnityServices.InitializeAsync();

        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();

        IsInitialized = true;
        Debug.Log("[UnityServicesManager] Ready. Player ID: " + AuthenticationService.Instance.PlayerId);
    }
}
