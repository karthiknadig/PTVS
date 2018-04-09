using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PythonTools.Docker {
    public class ContainerException : Exception {
        public ContainerException() { }
        public ContainerException(string message) : base(message) { }
        public ContainerException(string message, Exception innerException) : base(message, innerException) { }
    }

    public class ContainerServiceNotInstalledException : ContainerException {
        public ContainerServiceNotInstalledException() { }
        public ContainerServiceNotInstalledException(string message) : base(message) { }
        public ContainerServiceNotInstalledException(string message, Exception innerException) : base(message, innerException) { }
    }

    public class ContainerServiceNotReadyException : ContainerException {
        public ContainerServiceNotReadyException() { }
        public ContainerServiceNotReadyException(string message) : base(message) { }
        public ContainerServiceNotReadyException(string message, Exception innerException) : base(message, innerException) { }
    }

    public class ContainerServiceNotRunningException : ContainerException {
        public ContainerServiceNotRunningException(string serviceName) {
            ServiceName = serviceName;
        }
        public ContainerServiceNotRunningException(string serviceName, string message) : base(message) {
            ServiceName = serviceName;
        }
        public ContainerServiceNotRunningException(string serviceName, string message, Exception innerException) : base(message, innerException) {
            ServiceName = serviceName;
        }
        public string ServiceName { get; }
    }

    public class ContainerServicePermissionException : ContainerException {
        public ContainerServicePermissionException() { }
        public ContainerServicePermissionException(string message) : base(message) { }
        public ContainerServicePermissionException(string message, Exception innerException) : base(message, innerException) { }
    }
}
