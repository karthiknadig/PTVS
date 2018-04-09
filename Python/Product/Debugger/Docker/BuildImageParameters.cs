using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PythonTools.Docker {
    public class BuildImageParameters {
        public string Image { get; }
        public string Tag { get; }
        public IReadOnlyDictionary<string, string> Params { get; }
        public string Name { get; }
        public string DockerfilePath { get; }
        public int Port { get; }

        public BuildImageParameters(string dockerFile, string imageName, string imageTag, string name, int port)
            : this(dockerFile, imageName, imageTag, new Dictionary<string, string>(), name, port) { }

        public BuildImageParameters(string dockerFile, string imageName, string imageTag, IReadOnlyDictionary<string, string> imageParams, string name, int port) {
            DockerfilePath = dockerFile;
            Image = imageName;
            Tag = imageTag;
            Params = imageParams;
            Name = name;
            Port = port;
        }

        public void Deconstruct(out string dockerFile, out string imageName, out string imageTag, out IReadOnlyDictionary<string, string> imageParams, out string name, out int port) {
            dockerFile = DockerfilePath;
            imageName = Image;
            imageTag = Tag;
            imageParams = Params;
            name = Name;
            port = Port;
        }
    }
}
