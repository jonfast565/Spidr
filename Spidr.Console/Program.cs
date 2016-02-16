using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Spidr.Runtime;
using log4net;
using Newtonsoft.Json;
using System.Threading;

namespace Spidr.Console
{
    class Program
    {
        static void Main(string[] args)
        {
            log4net.Config.XmlConfigurator.Configure();
            Spider s = new Spider("", 1000);
            s.Start();
            Thread.Sleep(100000);
        }
    }
}
