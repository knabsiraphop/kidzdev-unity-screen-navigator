using UnityEngine.UI;

namespace KidzDev.Unity.ScreenNavigator.MailFlow
{
    /// <summary>
    /// The message screen as a <see cref="NavPanel"/> subclass. It binds its content from the push payload
    /// (the mail id) inside <see cref="NavPanel.OnPushedInternal"/> — the idiomatic way to hand data to a
    /// screen as it is pushed, instead of poking its widgets from the caller.
    /// </summary>
    public sealed class MessagePanel : NavPanel
    {
        private MailStore _store;
        private Text _subject;
        private Text _body;

        /// <summary>Wires the store and the text widgets this panel renders into (called once at build time).</summary>
        public void Configure(MailStore store, Text subject, Text body)
        {
            _store = store;
            _subject = subject;
            _body = body;
        }

        protected override void OnPushedInternal(object arg)
        {
            if (arg is int id && _store != null && _store.TryGet(id, out var mail))
            {
                _subject.text = mail.Subject;
                _body.text = mail.Body;
            }
        }
    }
}
