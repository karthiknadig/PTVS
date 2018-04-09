using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PythonTools.Docker {
    public static class Resources {
        public static string LocalDockerErrorFormat => "{0}> ERROR: {1}";
        public static string LocalDockerOutputStreamException => "Can't read from Docker process output stream";

        public static string Error_ContainerDeleteFailed => "Failed to delete container with id: {0}\n{1}";

        public static string Error_ContainerIdInvalid => "Invalid container id: {0}";

        public static string Error_ContainerStartFailed => "Failed to start container with id: {0}\n{1}";

        public static string Error_ContainerStopFailed => "Failed to stop container with id: {0}\n{1}";

        public static string Error_DockerForWindowsNotRunning => "\"Docker for windows.exe\" is not running.";

        public static string Error_DockerNotFound => "Could not find Docker installation at \"{0}\".";

        public static string Error_DockerServiceNotRunning => "Docker windows service \"com.docker.service\" is not running, status: {0}";

        public static string Error_NoDockerCommand => "Could not find the required Docker command at \"{0}\".";

        public static string Error_ServiceNotAvailable => "Container service is not available.Check if \"{0}\" is installed and running.";

        public static string Error_UserNotInDockerUsersGroup => "You don't have permissions to use docker. Please add user \"{0}\" to \"{1}\" group.";

        public static string Info_ServiceAvailable => "Container service is available.";

    }
}
