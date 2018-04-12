using Microsoft.PythonTools.Infrastructure;
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

        public string BuildImage(string buildOptions, CancellationToken ct) {
            var command = "build";
            return ExecuteCommand(($"{command} {buildOptions}"), command, -1, true, ct);
        }

        public IEnumerable<IContainer> ListContainers(bool getAll = true, CancellationToken ct = default(CancellationToken)) {

            var command = "ps";
            var commandOptions = getAll ? "-a -q" : "-q";
            var output = ExecuteCommand(($"{command} {commandOptions}"), null, _defaultTimeout, true, ct);
            var lines = output.Split(new[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            var ids = lines.Where(line => _containerIdMatcher12.IsMatch(line) || _containerIdMatcher64.IsMatch(line));
            var arr = Inspect(ids, ct);
            return arr.Select(c => new LocalDockerContainer(c));
        }

        public IEnumerable<ContainerImage> ListImages(bool getAll = true, CancellationToken ct = default(CancellationToken)) {
            var command = "images";
            var commandOptions = getAll ? "-a -q" : "-q";
            var output = ExecuteCommand(($"{command} {commandOptions}"), null, _defaultTimeout, true, ct);
            var lines = output.Split(new[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            var ids = lines.Where(line => _containerIdMatcher12.IsMatch(line) || _containerIdMatcher64.IsMatch(line));
            var arr = Inspect(ids, ct);
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

        public IContainer GetContainer(string containerId, CancellationToken ct) {

            var ids = (ListContainers(true, ct)).Where(container => containerId.ToLower().StartsWith(container.Id.ToLower()));
            if (ids.Any()) {
                var arr = Inspect(new string[] { containerId }, ct);
                if (arr.Count == 1) {
                    return new LocalDockerContainer(arr[0]);
                }
            }

            return null;
        }

        public JArray Inspect(IEnumerable<string> containerIds, CancellationToken ct) {
            var ids = containerIds.ToList();
            if (ids.Any()) {
                var command = "inspect";
                var commandOptions = string.Join(" ", ids);
                var result = ExecuteCommand(($"{command} {commandOptions}"), null, _defaultTimeout, false, ct);
                return JArray.Parse(result);
            }

            return new JArray();
        }

        public string RepositoryLogin(string username, string password, string server, CancellationToken ct) {
            var command = "login";
            var commandOptions = $"-u {username} -p {password} {server}";
            return ExecuteCommand(($"{command} {commandOptions}"), $"{command} {server}", -1, true, ct);
        }

        public string RepositoryLogin(RepositoryCredentials auth, CancellationToken ct)
            => RepositoryLogin(auth.Username, auth.Password, auth.RepositoryServer, ct);

        public string RepositoryLogout(string server, CancellationToken ct) {
            var command = $"logout {server}";
            return ExecuteCommand(command, command, -1, true, ct);
        }

        public string RepositoryLogout(RepositoryCredentials auth, CancellationToken ct)
            => RepositoryLogout(auth.RepositoryServer, ct);

        public string PullImage(string fullImageName, CancellationToken ct) {
            var command = $"pull {fullImageName}";
            return ExecuteCommand(command, command, -1, true, ct);
        }

        public string CreateContainer(string createOptions, CancellationToken ct) {
            var command = "create";
            var output = ExecuteCommand(($"{command} {createOptions}"), command, -1, true, ct);
            var matches = _containerIdMatcher64.Matches(output);

            return matches.Count >= 1 ? matches[0].Value : string.Empty;
        }

        public string DeleteContainer(string containerId, CancellationToken ct) {
            var command = ($"rm {containerId}");
            return ExecuteCommand(command, command, -1, true, ct);
        }

        public string StartContainer(string containerId, CancellationToken ct) {
            var command = ($"start {containerId}");
            return ExecuteCommand(command, command, -1, true, ct);
        }

        public string StopContainer(string containerId, CancellationToken ct) {
            var command = $"stop {containerId}";
            return ExecuteCommand(command, command, -1, true, ct);
        }

        protected abstract LocalDocker GetLocalDocker();

        private string ExecuteCommand(string arguments, string outputPrefix, int timeoutms, bool failOnTimeout = true, CancellationToken ct = default(CancellationToken)) {
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
                    var line = process.StandardOutput.ReadLine();
                    if (printOutput) {
                        Output.WriteLine(($"{outputPrefix}> {line}"));
                    }
                    result.AppendLine(line);
                }

                process.WaitForExit(timeoutms);
            } catch (IOException) {
                if (printOutput) {
                    Output.WriteError(Resources.LocalDockerErrorFormat.FormatInvariant(outputPrefix, Resources.LocalDockerOutputStreamException));
                }
                throw new ContainerException(Resources.LocalDockerOutputStreamException);
            } catch (OperationCanceledException) when (!failOnTimeout && !ct.IsCancellationRequested) {
            }

            var error = process.StandardError.ReadToEnd();
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
