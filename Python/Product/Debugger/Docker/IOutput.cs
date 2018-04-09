using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PythonTools.Docker {
    public interface IOutput {
        void Write(string text);
        void WriteError(string text);

        void WriteLine(string text);
        void WriteErrorLine(string text);
    }
}
