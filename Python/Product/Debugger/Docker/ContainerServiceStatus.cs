using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PythonTools.Docker {
    public enum ContainerServiceStatusType {
        Information,
        Warning,
        Error
    }
    public struct ContainerServiceStatus {
        public bool IsServiceAvailable { get; }
        public string StatusMessage { get; }
        public int StatusCode { get; }
        public ContainerServiceStatusType StatusType { get; }

        public ContainerServiceStatus(bool serviceAvailable, string statusMessage, ContainerServiceStatusType statusType) {
            IsServiceAvailable = serviceAvailable;
            StatusMessage = statusMessage;
            StatusType = statusType;
            StatusCode = 0;
        }

        public ContainerServiceStatus(bool serviceAvailable, string statusMessage, ContainerServiceStatusType statusType, int statusCode) {
            IsServiceAvailable = serviceAvailable;
            StatusMessage = statusMessage;
            StatusType = statusType;
            StatusCode = statusCode;
        }
    }
}
