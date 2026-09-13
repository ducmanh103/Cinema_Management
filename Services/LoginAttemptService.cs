using System;
using System.Collections.Concurrent;

namespace CinemaManagement.Services
{
    public interface ILoginAttemptService
    {
        bool IsLockedOut(string key, out TimeSpan remainingTime);
        void RecordFailedAttempt(string key);
        void ResetAttempts(string key);
    }

    public class LoginAttemptService : ILoginAttemptService
    {
        private class AttemptInfo
        {
            public int FailCount { get; set; }
            public DateTime? LockoutEnd { get; set; }
            public DateTime LastFailedAt { get; set; }
        }

        private static readonly ConcurrentDictionary<string, AttemptInfo> _attempts = new(StringComparer.OrdinalIgnoreCase);

        private const int MaxFailedAttempts = 5;
        private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan AttemptWindow = TimeSpan.FromMinutes(30);

        public bool IsLockedOut(string key, out TimeSpan remainingTime)
        {
            remainingTime = TimeSpan.Zero;
            if (string.IsNullOrWhiteSpace(key)) return false;

            if (_attempts.TryGetValue(key, out var info))
            {
                if (info.LockoutEnd.HasValue && info.LockoutEnd.Value > DateTime.UtcNow)
                {
                    remainingTime = info.LockoutEnd.Value - DateTime.UtcNow;
                    return true;
                }

                // Hết hạn khóa thì mở lại
                if (info.LockoutEnd.HasValue && info.LockoutEnd.Value <= DateTime.UtcNow)
                {
                    info.LockoutEnd = null;
                    info.FailCount = 0;
                }
            }

            return false;
        }

        public void RecordFailedAttempt(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return;

            var now = DateTime.UtcNow;
            _attempts.AddOrUpdate(
                key,
                _ => new AttemptInfo
                {
                    FailCount = 1,
                    LastFailedAt = now,
                    LockoutEnd = null
                },
                (_, info) =>
                {
                    // Nếu lần fail trước đã quá lâu, tính lại từ đầu
                    if (now - info.LastFailedAt > AttemptWindow)
                    {
                        info.FailCount = 1;
                        info.LockoutEnd = null;
                    }
                    else
                    {
                        info.FailCount++;
                        if (info.FailCount >= MaxFailedAttempts)
                        {
                            info.LockoutEnd = now.Add(LockoutDuration);
                        }
                    }
                    info.LastFailedAt = now;
                    return info;
                });
        }

        public void ResetAttempts(string key)
        {
            if (!string.IsNullOrWhiteSpace(key))
            {
                _attempts.TryRemove(key, out _);
            }
        }
    }
}
