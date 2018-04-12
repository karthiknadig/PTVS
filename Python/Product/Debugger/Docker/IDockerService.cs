using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PythonTools.Docker {
    public interface IDockerService {
        string BuildImage(string buildOptions, CancellationToken ct);
        IEnumerable<IContainer> ListContainers(bool getAll = true, CancellationToken ct = default(CancellationToken));
        IEnumerable<ContainerImage> ListImages(bool getAll = true, CancellationToken ct = default(CancellationToken));
        IContainer GetContainer(string containerId, CancellationToken ct);
        JArray Inspect(IEnumerable<string> objectIds, CancellationToken ct);
        string RepositoryLogin(string username, string password, string server, CancellationToken ct);
        string RepositoryLogin(RepositoryCredentials auth, CancellationToken ct);
        string RepositoryLogout(RepositoryCredentials auth, CancellationToken ct);
        string RepositoryLogout(string server, CancellationToken ct);
        string PullImage(string fullImageName, CancellationToken ct);
        string CreateContainer(string createOptions, CancellationToken ct);
        string DeleteContainer(string containerId, CancellationToken ct);
        string StartContainer(string containerId, CancellationToken ct);
        string StopContainer(string containerId, CancellationToken ct);
    }
}
