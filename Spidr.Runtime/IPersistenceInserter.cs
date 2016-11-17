using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spidr.Runtime
{
    public interface IPersistenceInserter
    {
        void PersistData(Page value);
        void InsertBinaryFile(BinaryFile f, string type);
        void InsertLink(LinkTag t);
        void InsertPage(Page p);
    }
}
