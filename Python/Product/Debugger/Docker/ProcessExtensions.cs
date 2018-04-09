using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PythonTools.Docker {
    public static class ProcessExtensions {
        public static Task WaitForExitAsync(this Process process, int milliseconds, CancellationToken cancellationToken = default(CancellationToken)) {
            var tcs = new TaskCompletionSource<int>();
            process.Exited += (o, e) => tcs.TrySetResult(0);
            tcs.RegisterForCancellation(milliseconds, cancellationToken).UnregisterOnCompletion(tcs.Task);
            return tcs.Task;
        }
    }
}
