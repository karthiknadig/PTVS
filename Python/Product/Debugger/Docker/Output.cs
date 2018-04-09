using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PythonTools.Docker {
    public class Output : IOutput {
        public void Write(string text) {
            throw new NotImplementedException();
        }

        public void WriteError(string text) {
            throw new NotImplementedException();
        }

        public void WriteErrorLine(string text) {
            throw new NotImplementedException();
        }

        public void WriteLine(string text) {
            throw new NotImplementedException();
        }
    }
}
