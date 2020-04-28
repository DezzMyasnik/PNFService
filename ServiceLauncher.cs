using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Net;
using Newtonsoft.Json;
//using System.Threading;
using System.Timers;

namespace PNFService
{
    partial class ServiceLauncher : ServiceBase
    {
        public ServiceLauncher()
        {
            InitializeComponent();
        }
        Timer timer = new Timer();
        protected override void OnStart(string[] args)
        {
            timer.Interval = 20000; // 20 seconds
            timer.Elapsed += Timer_Elapsed;
            //eventLog1.WriteEntry("1000" + Properties.Settings.Default.PackflyConnStr, EventLogEntryType.Information, 406);
            timer.Start();
            // TODO: Добавьте код для запуска службы.
        }

        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            PNFService.Service1 ddd = new Service1();
            string ver = ddd.GetCurrentVersion();
            eventLog1.WriteEntry(ver, EventLogEntryType.Information, 500);
        }

        protected override void OnStop()
        {
            // TODO: Добавьте код, выполняющий подготовку к остановке службы.
        }


    }
}
