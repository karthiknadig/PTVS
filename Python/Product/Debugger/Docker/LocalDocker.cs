using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PythonTools.Docker {
    public class LocalDocker {
        public string BinPath { get; }
        public string DockerCommandPath { get; }

        public LocalDocker(string binPath, string dockerCommandPath) {
            BinPath = binPath;
            DockerCommandPath = dockerCommandPath;
        }
    }
}
