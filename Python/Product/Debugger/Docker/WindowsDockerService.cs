using Microsoft.PythonTools.Infrastructure;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PythonTools.Docker {
    public class WindowsDockerService : LocalDockerService, IContainerService {
        const string DockerServiceName = "Docker for Windows";
        private LocalDocker _docker;
        private readonly WindowsLocalDockerFinder _dockerFinder;

        public WindowsDockerService() : base() {
            _dockerFinder = new WindowsLocalDockerFinder();
        }

        public ContainerServiceStatus GetServiceStatus() => GetDockerProcess(DockerServiceName).HasExited
            ? new ContainerServiceStatus(false, Resources.Error_ServiceNotAvailable, ContainerServiceStatusType.Error)
            : new ContainerServiceStatus(true, Resources.Info_ServiceAvailable, ContainerServiceStatusType.Information);

        internal static Process GetDockerProcess(string processName) => Process.GetProcessesByName(processName).FirstOrDefault();

        public async Task<bool> BuildImageAsync(BuildImageParameters buildParams, CancellationToken ct) {
            var buildOptions = $"-t {buildParams.Image}:{buildParams.Tag} {Path.GetDirectoryName(buildParams.DockerfilePath)}";
            var output = await BuildImageAsync(buildOptions, ct);
            return output.ContainsOrdinal($"Successfully tagged {buildParams.Image}:{buildParams.Tag}", true) ||
                output.ContainsOrdinal($"Successfully built", true);
        }

        public async Task<IContainer> CreateContainerAsync(ContainerCreateParameters createParams, CancellationToken ct) {
            if (createParams.ImageSourceCredentials != null) {
                await RepositoryLoginAsync(createParams.ImageSourceCredentials, ct);
            }
            try {
                await PullImageAsync(($"{createParams.Image}:{createParams.Tag}"), ct);

                string createOptions = ($"{createParams.StartOptions} {createParams.Image}:{createParams.Tag} {createParams.Command}");
                var containerId = await CreateContainerAsync(createOptions, ct);
                if (string.IsNullOrEmpty(containerId)) {
                    throw new ContainerException(Resources.Error_ContainerIdInvalid.FormatInvariant(containerId));
                }
                return await GetContainerAsync(containerId, ct);
            } finally {
                if (createParams.ImageSourceCredentials != null) {
                    await RepositoryLogoutAsync(createParams.ImageSourceCredentials, ct);
                }
            }
        }

        async Task IContainerService.DeleteContainerAsync(string containerId, CancellationToken ct) {
            var result = await DeleteContainerAsync(containerId, ct);
            if (!result.StartsWithOrdinal(containerId, true)) {
                throw new ContainerException(Resources.Error_ContainerDeleteFailed.FormatInvariant(containerId, result));
            }
        }

        async Task IContainerService.StartContainerAsync(string containerId, CancellationToken ct) {
            var result = await StartContainerAsync(containerId, ct);
            if (!result.StartsWithOrdinal(containerId, true)) {
                throw new ContainerException(Resources.Error_ContainerStartFailed.FormatInvariant(containerId, result));
            }
        }

        async Task IContainerService.StopContainerAsync(string containerId, CancellationToken ct) {
            var result = await StopContainerAsync(containerId, ct);
            if (!result.StartsWithOrdinal(containerId, true)) {
                throw new ContainerException(Resources.Error_ContainerStopFailed.FormatInvariant(containerId, result));
            }
        }

        protected override LocalDocker GetLocalDocker() {
            _docker = _docker ?? _dockerFinder.GetLocalDocker();
            return _docker;
        }
    }
}
