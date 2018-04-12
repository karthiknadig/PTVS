using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PythonTools.Docker {
    public interface IContainerService {
        ContainerServiceStatus GetServiceStatus();
        IContainer GetContainer(string containerId, CancellationToken ct);
        IEnumerable<IContainer> ListContainers(bool allContainers, CancellationToken ct);
        IEnumerable<ContainerImage> ListImages(bool allImages, CancellationToken ct);
        IContainer CreateContainer(ContainerCreateParameters createParams, CancellationToken ct);
        void DeleteContainer(string containerId, CancellationToken ct);
        void StartContainer(string containerId, CancellationToken ct);
        void StopContainer(string containerId, CancellationToken ct);
    }
}
