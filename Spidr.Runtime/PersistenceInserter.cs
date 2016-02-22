using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spidr.Runtime
{
    interface PersistenceInserter
    {
        void InsertBinaryFile(BinaryFile f, string type);
        void InsertLink(LinkTag t);
        void InsertPage(Page p);
    }
}
