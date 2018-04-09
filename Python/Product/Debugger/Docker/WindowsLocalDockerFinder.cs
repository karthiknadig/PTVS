using Microsoft.PythonTools.Infrastructure;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PythonTools.Docker {
    public class WindowsLocalDockerFinder {
        public const string DockerCommand = "docker.exe";
        public const string DockerRegistryPath = @"SOFTWARE\Docker Inc.\Docker";
        public const string DockerRegistryPath2 = @"SYSTEM\CurrentControlSet\Services\com.docker.service";
        public const string DockerServiceName = "com.docker.service";
        public const string DockerForWindowsName = "Docker for Windows";
        public const string DockerUsersGroup = "docker-users";

        public WindowsLocalDockerFinder() {
        }

        public LocalDocker GetLocalDocker() {
            LocalDocker docker = null;
            using (var hklm64 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)) {
                if (!TryGetDockerFromRegistryInstall(hklm64, out docker) &&
                   !TryGetDockerFromServiceInstall(hklm64, out docker) &&
                   !TryGetDockerFromProgramFiles(out docker)) {
                    throw new ContainerServiceNotInstalledException(Resources.Error_DockerNotFound.FormatInvariant(DockerRegistryPath));
                }
            }

            if (!File.Exists(docker.DockerCommandPath)) {
                throw new ContainerServiceNotInstalledException(Resources.Error_NoDockerCommand.FormatInvariant(docker.DockerCommandPath));
            }

            CheckUserPermissions();
            CheckIfServiceIsRunning();
            return docker;
        }

        public void CheckIfServiceIsRunning() {
            using (var sc = new ServiceController(DockerServiceName)) {
                if (sc.Status != ServiceControllerStatus.Running) {
                    throw new ContainerServiceNotRunningException(DockerServiceName, Resources.Error_DockerServiceNotRunning.FormatInvariant(sc.Status.ToString()));
                }

                if (!Process.GetProcessesByName(DockerForWindowsName).Any()) {
                    throw new ContainerServiceNotRunningException(DockerForWindowsName, Resources.Error_DockerForWindowsNotRunning);
                }
            }
        }

        public void CheckUserPermissions() {
            using (var currentUserIdentity = WindowsIdentity.GetCurrent()) {
                var principal = new WindowsPrincipal(currentUserIdentity);
                if (!principal.IsInRole(DockerUsersGroup)) {
                    throw new ContainerServicePermissionException(Resources.Error_UserNotInDockerUsersGroup.FormatInvariant(currentUserIdentity.Name, DockerUsersGroup));
                }
            }
        }

        private static bool TryGetDockerFromProgramFiles(out LocalDocker docker) {
            string[] envVars = { "ProgramFiles", "ProgramFiles(x86)", "ProgramW6432" };
            foreach (var envVar in envVars) {
                var progFiles = Environment.GetEnvironmentVariable(envVar);
                if (!string.IsNullOrWhiteSpace(progFiles)) {
                    var basePath = Path.Combine(progFiles, "Docker", "Docker");
                    var binPath = Path.Combine(basePath, "resources", "bin");
                    var commandPath = Path.Combine(binPath, DockerCommand);
                    if (File.Exists(commandPath)) {
                        docker = new LocalDocker(binPath, commandPath);
                        return true;
                    }
                }
            }

            docker = null;
            return false;
        }

        private static bool TryGetDockerFromRegistryInstall(RegistryKey hklm, out LocalDocker docker) {
            using (var dockerRegKey = hklm.OpenSubKey(DockerRegistryPath)) {
                if (dockerRegKey != null) {
                    string[] subkeys = dockerRegKey.GetSubKeyNames();
                    foreach (var subKey in subkeys) {
                        using (var key = dockerRegKey.OpenSubKey(subKey)) {
                            var isInstallKey = key.GetValueNames().Count(v => v.Equals("BinPath") || v.Equals("Version")) == 2;
                            if (isInstallKey) {
                                var binPath = ((string)key.GetValue("BinPath")).Trim('\"');
                                var commandPath = Path.Combine(binPath, DockerCommand);
                                if (File.Exists(commandPath)) {
                                    docker = new LocalDocker(binPath, commandPath);
                                    return true;
                                }
                            }
                        }
                    }
                }
            }

            docker = null;
            return false;
        }

        private static bool TryGetDockerFromServiceInstall(RegistryKey hklm, out LocalDocker docker) {
            using (var dockerRegKey = hklm.OpenSubKey(DockerRegistryPath2)) {
                if (dockerRegKey != null) {
                    var valueNames = dockerRegKey.GetValueNames();
                    if (valueNames.Contains("ImagePath")) {
                        var comPath = ((string)dockerRegKey.GetValue("ImagePath")).Trim('\"');
                        var basePath = Path.GetDirectoryName(comPath);
                        var binPath = Path.Combine(basePath, "resources", "bin");
                        var commandPath = Path.Combine(binPath, DockerCommand);
                        if (File.Exists(commandPath)) {
                            docker = new LocalDocker(binPath, commandPath);
                            return true;
                        }
                    }
                }
            }

            docker = null;
            return false;
        }
    }
}
