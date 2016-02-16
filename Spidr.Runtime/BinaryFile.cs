using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spidr.Runtime
{
    public class BinaryFile : HtmlTag
    {
        public Guid PageId;
        public string Name;
        public MemoryStream Contents;

        public BinaryFile(Guid PageId)
        {
            this.PageId = PageId;
        }
    }
}
