using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spidr.Runtime
{
    public class Page
    {
        public Guid PageId { get; set; }
        public string Name { get; set; }
        public UrlObject Link { get; set; }
        public string Content { get; set; }
        public List<LinkTag> LinkTags { get; set; }
        public List<BinaryFile> ImageTags { get; set; }
        public List<BinaryFile> FileTags { get; set; }
        public bool Processed { get; set; }
        public DateTime AggregateDateTime { get; set; }
        public Page()
        {
            this.Processed = false;
            this.AggregateDateTime = DateTime.Now;
        }
    }
}
