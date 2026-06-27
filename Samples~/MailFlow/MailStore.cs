using System.Collections.Generic;

namespace KidzDev.Unity.ScreenNavigator.MailFlow
{
    /// <summary>A trivial in-memory mail database for the sample. Not part of the package.</summary>
    public sealed class MailStore
    {
        public readonly struct Mail
        {
            public readonly int Id;
            public readonly string Subject;
            public readonly string Body;
            public Mail(int id, string subject, string body) { Id = id; Subject = subject; Body = body; }
        }

        private readonly List<Mail> _mails = new List<Mail>
        {
            new Mail(1, "Welcome to KidzDev", "Thanks for trying the Scene Navigator sample.\nTap a message, then Delete to see push/pop history in action."),
            new Mail(2, "Your build is ready", "Addressables content finished building.\nDownload size: 12.4 MB."),
            new Mail(3, "Weekly summary",     "3 commits, 1 release, 0 fires.\nNice week."),
        };

        public IReadOnlyList<Mail> All => _mails;

        public bool TryGet(int id, out Mail mail)
        {
            for (int i = 0; i < _mails.Count; i++)
            {
                if (_mails[i].Id == id) { mail = _mails[i]; return true; }
            }
            mail = default;
            return false;
        }

        public void Delete(int id)
        {
            for (int i = 0; i < _mails.Count; i++)
            {
                if (_mails[i].Id == id) { _mails.RemoveAt(i); return; }
            }
        }
    }
}
