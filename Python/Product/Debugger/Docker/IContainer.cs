using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PythonTools.Docker {
    public interface IContainer {
        string Id { get; }
        string Name { get; }
        IEnumerable<int> HostPorts { get; }
        string Status { get; }
        bool IsRunning { get; }
    }
}
