using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PythonTools.Docker {
    public class ContainerImage {
        public string Id { get; }
        public string Name { get; }
        public string Tag { get; }

        public ContainerImage(string id, string name, string tag) {
            Id = id;
            Name = name;
            Tag = tag;
        }

        public void Deconstruct(out string id, out string name, out string tag) {
            id = Id;
            name = Name;
            tag = Tag;
        }
    }
}
