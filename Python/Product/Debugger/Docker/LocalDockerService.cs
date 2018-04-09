﻿using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudio.Shell;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PythonTools.Docker {
    public abstract class LocalDockerService : IDockerService {
        public const string ContainerOutputName = "Python Containers";
        private readonly Regex _containerIdMatcher64 = new Regex("[0-9a-f]{64}", RegexOptions.IgnoreCase);
        private readonly Regex _containerIdMatcher12 = new Regex("[0-9a-f]{12}", RegexOptions.IgnoreCase);
        private readonly int _defaultTimeout = 500;
        private volatile IOutput _output;

        protected IOutput Output => _output ?? (_output = new Output());

        protected LocalDockerService() {
        }

        public async Task<IContainer> CreateContainerFromFileAsync(BuildImageParameters buildParams, CancellationToken ct) {
            var (dockerFile, imageName, imageTag, imageParams, containerName, containerPort) = buildParams;
            var images = await ListImagesAsync(true, ct);
            if (!images.Any(i => i.Name.EqualsOrdinal(imageName) && i.Tag.EqualsOrdinal(imageTag))) {
                var buildArgs = string.Join(" ", imageParams.Select(p => $"--build-arg {p.Key}={p.Value}"));
                var buildOptions = $"-t {imageName}:{imageTag} {buildArgs} \"{Path.GetDirectoryName(dockerFile)}\"";
                await BuildImageAsync(buildOptions, ct);
            }

            var createOptions = ($"-p {containerPort}:5444 --name {containerName} {imageName}:{imageTag} rtvsd");
            var containerId = await CreateContainerAsync(createOptions, ct);
            return await GetContainerAsync(containerId, ct);
        }

        public Task<string> BuildImageAsync(string buildOptions, CancellationToken ct) {
            var command = "build";
            return ExecuteCommandAsync(($"{command} {buildOptions}"), command, -1, true, ct);
        }

        public async Task<IEnumerable<IContainer>> ListContainersAsync(bool getAll = true, CancellationToken ct = default(CancellationToken)) {

            var command = "ps";
            var commandOptions = getAll ? "-a -q" : "-q";
            var output = await ExecuteCommandAsync(($"{command} {commandOptions}"), null, _defaultTimeout, true, ct);
            var lines = output.Split(new[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            var ids = lines.Where(line => _containerIdMatcher12.IsMatch(line) || _containerIdMatcher64.IsMatch(line));
            var arr = await InspectAsync(ids, ct);
            return arr.Select(c => new LocalDockerContainer(c));
        }

        public async Task<IEnumerable<ContainerImage>> ListImagesAsync(bool getAll = true, CancellationToken ct = default(CancellationToken)) {
            var command = "images";
            var commandOptions = getAll ? "-a -q" : "-q";
            var output = await ExecuteCommandAsync(($"{command} {commandOptions}"), null, _defaultTimeout, true, ct);
            var lines = output.Split(new[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            var ids = lines.Where(line => _containerIdMatcher12.IsMatch(line) || _containerIdMatcher64.IsMatch(line));
            var arr = await InspectAsync(ids, ct);
            return arr.Select(GetContainerImage);
        }

        private ContainerImage GetContainerImage(JToken c) {
            var obj = (dynamic)c;
            var objId = ((string)obj.Id);
            int idx = objId.IndexOf(':');
            var id = idx < 0 ? objId : objId.Substring(idx + 1);
            var name = string.Empty;
            var tag = string.Empty;
            if (((JArray)obj.RepoTags).Any()) {
                string[] split = ((string)obj.RepoTags[0]).Split(new[] { ":" }, StringSplitOptions.RemoveEmptyEntries);
                name = split[0];
                tag = split.Length == 2 ? split[1] : string.Empty; ;
            }
            return new ContainerImage(id, name, tag);
        }

        public async Task<IContainer> GetContainerAsync(string containerId, CancellationToken ct) {

            var ids = (await ListContainersAsync(true, ct)).Where(container => containerId.ToLower().StartsWith(container.Id.ToLower()));
            if (ids.Any()) {
                var arr = await InspectAsync(new string[] { containerId }, ct);
                if (arr.Count == 1) {
                    return new LocalDockerContainer(arr[0]);
                }
            }

            return null;
        }

        public async Task<JArray> InspectAsync(IEnumerable<string> containerIds, CancellationToken ct) {
            var ids = containerIds.ToList();
            if (ids.Any()) {
                var command = "inspect";
                var commandOptions = string.Join(" ", ids);
                var result = await ExecuteCommandAsync(($"{command} {commandOptions}"), null, _defaultTimeout, false, ct);
                return JArray.Parse(result);
            }

            return new JArray();
        }

        public Task<string> RepositoryLoginAsync(string username, string password, string server, CancellationToken ct) {
            var command = "login";
            var commandOptions = $"-u {username} -p {password} {server}";
            return ExecuteCommandAsync(($"{command} {commandOptions}"), $"{command} {server}", -1, true, ct);
        }

        public Task<string> RepositoryLoginAsync(RepositoryCredentials auth, CancellationToken ct)
            => RepositoryLoginAsync(auth.Username, auth.Password, auth.RepositoryServer, ct);

        public Task<string> RepositoryLogoutAsync(string server, CancellationToken ct) {
            var command = $"logout {server}";
            return ExecuteCommandAsync(command, command, -1, true, ct);
        }

        public Task<string> RepositoryLogoutAsync(RepositoryCredentials auth, CancellationToken ct)
            => RepositoryLogoutAsync(auth.RepositoryServer, ct);

        public Task<string> PullImageAsync(string fullImageName, CancellationToken ct) {
            var command = $"pull {fullImageName}";
            return ExecuteCommandAsync(command, command, -1, true, ct);
        }

        public async Task<string> CreateContainerAsync(string createOptions, CancellationToken ct) {
            var command = "create";
            var output = await ExecuteCommandAsync(($"{command} {createOptions}"), command, -1, true, ct);
            var matches = _containerIdMatcher64.Matches(output);

            return matches.Count >= 1 ? matches[0].Value : string.Empty;
        }

        public Task<string> DeleteContainerAsync(string containerId, CancellationToken ct) {
            var command = ($"rm {containerId}");
            return ExecuteCommandAsync(command, command, -1, true, ct);
        }

        public Task<string> StartContainerAsync(string containerId, CancellationToken ct) {
            var command = ($"start {containerId}");
            return ExecuteCommandAsync(command, command, -1, true, ct);
        }

        public Task<string> StopContainerAsync(string containerId, CancellationToken ct) {
            var command = $"stop {containerId}";
            return ExecuteCommandAsync(command, command, -1, true, ct);
        }

        protected abstract LocalDocker GetLocalDocker();

        private async Task<string> ExecuteCommandAsync(string arguments, string outputPrefix, int timeoutms, bool failOnTimeout = true, CancellationToken ct = default(CancellationToken)) {
            var printOutput = outputPrefix != null;

            var docker = GetLocalDocker();
            var psi = new ProcessStartInfo {
                CreateNoWindow = true,
                FileName = docker.DockerCommandPath,
                Arguments = arguments,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                UseShellExecute = false
            };

            var process = Process.Start(psi);
            var result = new StringBuilder();
            try {
                while (!process.StandardOutput.EndOfStream) {
                    var line = await process.StandardOutput.ReadLineAsync();
                    if (printOutput) {
                        Output.WriteLine(($"{outputPrefix}> {line}"));
                    }
                    result.AppendLine(line);
                }

                await process.WaitForExitAsync(timeoutms, ct);
            } catch (IOException) {
                if (printOutput) {
                    Output.WriteError(Resources.LocalDockerErrorFormat.FormatInvariant(outputPrefix, Resources.LocalDockerOutputStreamException));
                }
                throw new ContainerException(Resources.LocalDockerOutputStreamException);
            } catch (OperationCanceledException) when (!failOnTimeout && !ct.IsCancellationRequested) {
            }

            var error = await process.StandardError.ReadToEndAsync();
            if (!string.IsNullOrEmpty(error) && !IsSecurityWarning(error)) {
                Output.WriteError(Resources.LocalDockerErrorFormat.FormatInvariant(outputPrefix, error));
                if (IsServiceNotReady(error)) {
                    throw new ContainerServiceNotReadyException(error);
                } else {
                    throw new ContainerException(error);
                }
            }

            return result.ToString();
        }

        private bool IsSecurityWarning(string error) {
            return error.ContainsOrdinal("SECURITY WARNING: You are building a Docker image from Windows against a non-Windows Docker host.");
        }

        private bool IsServiceNotReady(string error) {
            return error.ContainsOrdinal("open //./pipe/docker_engine: The system cannot find the file specified") ||
                error.ContainsOrdinal("docker daemon is not running");
        }
    }
}
