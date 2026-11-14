using System;
using System.Reactive.Subjects;

namespace NetGenCAD.UI.Services
{
    public sealed class ApiChangeNotifications : IDisposable
    {
        private readonly ISubject<string> _changes =
            Subject.Synchronize(new Subject<string>());

        public IObservable<string> Changes => _changes;

        public void Publish(string change) => _changes.OnNext(change);

        public void Dispose()
        {
            _changes.OnCompleted();
            (_changes as IDisposable)?.Dispose();
        }
    }
}
