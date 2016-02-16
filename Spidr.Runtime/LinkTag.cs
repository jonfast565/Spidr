using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spidr.Runtime
{
    public class LinkTag : HtmlTag
    {
        public Guid PageId { get; set; }

        public LinkTag(Guid PageId)
        {
            this.PageId = PageId;
        }
    }
}
