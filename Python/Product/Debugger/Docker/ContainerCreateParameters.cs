using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PythonTools.Docker {
    public struct ContainerCreateParameters {
        public string Image { get; }
        public string Tag { get; }
        public RepositoryCredentials ImageSourceCredentials { get; }
        public string StartOptions { get; }
        public string Command { get; }

        public ContainerCreateParameters(string image, string tag) {
            Image = image;
            Tag = tag;
            StartOptions = string.Empty;
            ImageSourceCredentials = null;
            Command = string.Empty;
        }

        public ContainerCreateParameters(string image, string tag, string startOptions) {
            Image = image;
            Tag = tag;
            StartOptions = startOptions;
            ImageSourceCredentials = null;
            Command = string.Empty;
        }

        public ContainerCreateParameters(string image, string tag, string startOptions, string command) {
            Image = image;
            Tag = tag;
            StartOptions = startOptions;
            Command = command;
            ImageSourceCredentials = null;
        }

        public ContainerCreateParameters(string image, string tag, string startOptions, RepositoryCredentials imageSourceCredentials, string command) {
            Image = image;
            Tag = tag;
            StartOptions = startOptions;
            ImageSourceCredentials = imageSourceCredentials;
            Command = command;
        }
    }
}
