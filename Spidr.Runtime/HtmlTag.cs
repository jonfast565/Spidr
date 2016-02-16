using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spidr.Runtime
{
    public abstract class HtmlTag
    {
        public string Tag { get; set; }
        public UrlObject Url { get; set; }
    }
}
