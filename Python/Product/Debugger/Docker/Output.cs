using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PythonTools.Docker {
    public class Output : IOutput {
        public void Write(string text) {
            Debug.WriteLine(text);
        }

        public void WriteError(string text) {
            Debug.WriteLine(text);
        }

        public void WriteErrorLine(string text) {
            Debug.WriteLine(text);
        }

        public void WriteLine(string text) {
            Debug.WriteLine(text);
        }
    }
}
