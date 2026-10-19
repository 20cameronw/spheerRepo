using System;
using UnityEngine;

namespace Frontier
{
    public static class NotificationPreferences
    {
        const string Key = "fr_job_notifications";
        public static event Action Changed;
        public static event Action StatusChanged;
        static string lastError;
        public static string LastError
        {
            get { return lastError; }
            set
            {
                if (lastError == value) return;
                lastError = value;
                StatusChanged?.Invoke();
            }
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
