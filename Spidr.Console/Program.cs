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
            System.Console.WriteLine("Address to process: ");
            string address = System.Console.ReadLine();
            Task.Factory.StartNew(() =>
            {
                Spider s = new Spider(address, 1000, true, 50);
                s.Start();
            });
            while (System.Console.ReadKey().Key != ConsoleKey.Escape) { }
            System.Console.WriteLine("Done!");
#if DEBUG
            Thread.Sleep(1000);
#endif
        }
    }
}
