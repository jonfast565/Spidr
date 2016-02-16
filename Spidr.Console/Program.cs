﻿using System;
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
            Spider s = new Spider(address, 1000, true);
            s.Start();
#if DEBUG
            Thread.Sleep(100000);
#endif
        }
    }
}
