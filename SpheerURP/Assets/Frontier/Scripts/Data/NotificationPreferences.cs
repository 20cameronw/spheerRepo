using System;
using Frontier.Services;
using UnityEngine;

namespace Frontier
{
    public static class NotificationPreferences
    {
        const string Key = "fr_job_notifications";
        public static event Action Changed;
        public static event Action StatusChanged;
        static string lastError;
        static ServiceMessage? lastFailure;
        public static string LastError
        {
            get { return lastFailure.HasValue ? lastFailure.Value.Resolve(UiText.Get) : lastError; }
            set
            {
                if (lastError == value && !lastFailure.HasValue) return;
                lastError = value;
                lastFailure = null;
                StatusChanged?.Invoke();
            }
        }
        public static void ReportFailure(ServiceMessage text)
        {
            lastError = text.ToString();
            lastFailure = text;
            StatusChanged?.Invoke();
        }
        public static bool Enabled
        {
            get { return PlayerPrefs.GetInt(Key, 0) == 1; }
            set
            {
                if (Enabled == value) return;
                PlayerPrefs.SetInt(Key, value ? 1 : 0);
                PlayerPrefs.Save();
                LastError = null;
                Changed?.Invoke();
            }
        }
    }
}
