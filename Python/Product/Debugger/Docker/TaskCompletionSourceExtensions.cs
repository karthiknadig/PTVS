using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PythonTools.Docker {
    public static class TaskCompletionSourceExtensions {
        public static CancellationTokenRegistration RegisterForCancellation<T>(this TaskCompletionSource<T> taskCompletionSource, CancellationToken cancellationToken)
            => taskCompletionSource.RegisterForCancellation(-1, cancellationToken);

        public static CancellationTokenRegistration RegisterForCancellation<T>(this TaskCompletionSource<T> taskCompletionSource, int millisecondsDelay, CancellationToken cancellationToken) {
            if (millisecondsDelay >= 0) {
                var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(millisecondsDelay);
                cancellationToken = cts.Token;
            }

            var action = new CancelOnTokenAction<T>(taskCompletionSource, cancellationToken);
            return cancellationToken.Register(action.Invoke);
        }

        private struct CancelOnTokenAction<T> {
            private readonly TaskCompletionSource<T> _taskCompletionSource;
            private readonly CancellationToken _cancellationToken;

            public CancelOnTokenAction(TaskCompletionSource<T> taskCompletionSource, CancellationToken cancellationToken) {
                _taskCompletionSource = taskCompletionSource;
                _cancellationToken = cancellationToken;
            }

            public void Invoke() {
                if (!_taskCompletionSource.Task.IsCompleted) {
                    ThreadPool.QueueUserWorkItem(TryCancel);
                }
            }

            private void TryCancel(object state) => _taskCompletionSource.TrySetCanceled(_cancellationToken);
        }
    }
}
