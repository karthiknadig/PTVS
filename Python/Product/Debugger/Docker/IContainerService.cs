using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PythonTools.Docker {
    public interface IContainerService {
        ContainerServiceStatus GetServiceStatus();
        Task<IContainer> GetContainerAsync(string containerId, CancellationToken ct);
        Task<IEnumerable<IContainer>> ListContainersAsync(bool allContainers, CancellationToken ct);
        Task<IEnumerable<ContainerImage>> ListImagesAsync(bool allImages, CancellationToken ct);
        Task<IContainer> CreateContainerFromFileAsync(BuildImageParameters buildOptions, CancellationToken ct);
        Task<IContainer> CreateContainerAsync(ContainerCreateParameters createParams, CancellationToken ct);
        Task DeleteContainerAsync(string containerId, CancellationToken ct);
        Task StartContainerAsync(string containerId, CancellationToken ct);
        Task StopContainerAsync(string containerId, CancellationToken ct);
    }
}
