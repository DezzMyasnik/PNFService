using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.ServiceProcess;
using System.Text;
using System.IO;
using System.Net;
using Newtonsoft.Json;
//using System.Threading;
using System.Timers;


namespace PNFService
{
    public partial class Service1 : ServiceBase
    {
        private int eventId = 1;
       
        String ID_Machina = "";
        //string adress = "http://localhost:8080/collectors";
        string adress = "";
        //string adress = "http://develop.db.packandfly.ru:8071/collectors";
        //string alarm_adress = "http://localhost:8080/api/alarm";
        string MDB_BASE = @"C:\Polycomm\Polycomm.mdb";
        string ACCDB_BASE = @"C:\Polycomm\Packfly.accdb";



        PolycommDataSetTableAdapters.SuitcaseTableAdapter suitTableAdapter = 
            new PolycommDataSetTableAdapters.SuitcaseTableAdapter();
        PolycommDataSetTableAdapters.SettingsTableAdapter settingsTableAdapter = 
            new PolycommDataSetTableAdapters.SettingsTableAdapter();
        PolycommDataSetTableAdapters.AllarmiTableAdapter alrmTableAdapter = 
            new PolycommDataSetTableAdapters.AllarmiTableAdapter();


        PackflyDataSetTableAdapters.SettingsTableAdapter settingsTabAdapter = 
            new PackflyDataSetTableAdapters.SettingsTableAdapter();
        PackflyDataSetTableAdapters.SuitcaseTableAdapter suittabAd = new PackflyDataSetTableAdapters.SuitcaseTableAdapter();
        PackflyDataSetTableAdapters.AllarmiTableAdapter alarmtabAd = new PackflyDataSetTableAdapters.AllarmiTableAdapter();
        bool CHECK_DB = false;
        public Service1()
        {
            InitializeComponent();
            
            if (!System.Diagnostics.EventLog.SourceExists("PNFSource"))
            {
                System.Diagnostics.EventLog.CreateEventSource(
                    "PNFSource", "PNFLog");
            }
           
            //eventLog1.Source = "PNFSource";
            //eventLog1.Log = "PNFLog";

        }
        
        Timer timer = new Timer();
        public void InitConection()
        {
            if (File.Exists(MDB_BASE))
            {
                CHECK_DB = false;
                //получаем ID машины из базы данных

                ID_Machina = settingsTableAdapter.GetID_Macchina().ToString();
                eventLog1.WriteEntry( MDB_BASE, EventLogEntryType.Information, 14);
                //SetSettings(ID_Machina);
                if (String.IsNullOrEmpty(ID_Machina))
                {
                    eventLog1.WriteEntry("Don`t setup machine ID ", EventLogEntryType.Error, 4);
                }
            }
            else
                if (File.Exists(ACCDB_BASE))
            {
                CHECK_DB = true;
                ID_Machina = settingsTabAdapter.GetIDMachina().ToString();
                //SetSettings(ID_Machina);
                eventLog1.WriteEntry(ACCDB_BASE, EventLogEntryType.Information, 14);
                if (String.IsNullOrEmpty(ID_Machina))
                {
                    eventLog1.WriteEntry("Don`t setup machine ID ", EventLogEntryType.Error, 4);
                }
            }
            else
            {
                eventLog1.WriteEntry("Нет файла базы данных", EventLogEntryType.Information, 12);
            }
        }
        protected override void OnStart(string[] args)
        {
            GetSettings();
            InitConection();
            
            eventLog1.WriteEntry(adress);
            eventLog1.WriteEntry("In OnStart."+ " "+ adress+" "+ID_Machina, EventLogEntryType.Information, 14);
            Send_sate_of_service(ID_Machina, adress, 1);
           
            timer.Interval = 20000; // 20 seconds
            timer.Elapsed += Timer_Elapsed;
            
            timer.Start();
        }
        
        struct ListIDs
        {
            public int localid { get; set; }
            public int local_total { get; set; }
            public int local_parziale { get; set; }
        }
        ///Запуск службы на стронних машинах:
        ///1. закидываем службу нужной сборки Release x64 для Win x64 и Release x86 для Win x86. 
        /// установка из консоли с правами админа 
        /// c:\Windows\Microsoft.NET\Framework64\v4.0.30319\installutil.exe "место расположения файла службы без пробелов"
        /// <summary>
        /// Обработка таблици упаковок
        /// </summary>
        private void SuitcaseProcess()
        {

            //1.Get last id from big DB
            //    1.1 Получить ID машины.
            //    1.2 Сформировать запрос на сервер
            //    1.3 Получить данные с сервера
            //2. Выбрать эти данные из локальной базы данных
            //    2.1 если есть то отдать на сервер status 1
            //    2.2 если НЕТ то отдать на сервер status 0



            try
            {
                var last_local_ids = new ListIDs();
                //1. Получаем последний ID из локальной БД
                if (!CHECK_DB)
                {
                    var last_local_ID = suitTableAdapter.GetLastIDlist();
                    last_local_ids.localid = (int)last_local_ID.Rows[0][0];
                    last_local_ids.local_total = (int)last_local_ID.Rows[0][1];
                    last_local_ids.local_parziale = (int)last_local_ID.Rows[0][2];

                }
                else
                {
                    var last_local_ID = suittabAd.GetLastIDList();
                    last_local_ids.localid = (int)last_local_ID.Rows[0][0];
                    last_local_ids.local_total = (int)last_local_ID.Rows[0][1];
                    last_local_ids.local_parziale = (int)last_local_ID.Rows[0][2];

                }

                    try
                    {
                        eventLog1.WriteEntry(string.Format("ID:{0}; Totale_ID:{1}; Parziale_ID: {2}",
                        last_local_ids.localid,
                        last_local_ids.local_total,
                        last_local_ids.local_parziale), EventLogEntryType.Information, 10);
                    }
                    catch(Exception exc)
                    {
                        eventLog1.WriteEntry(string.Format("{0}",exc.Message), EventLogEntryType.Error, 3);
                    }
                //2. Получаем последний ID из большой базы
                var last_outer_ID = GetLastIDFromBIGDB(ID_Machina, adress, "Suitcase");
                ServerSuitResponse resp = JsonConvert.DeserializeObject<ServerSuitResponse>(last_outer_ID);



                if (resp.data.polycommid == 0 && resp.data.totalid == 0 && resp.data.partialid == 0)// если сервер вернул нули, то есть в БД еще нед упаково от данной машины
                {
                    string jsonSuitcases_str = "";
                    if (!CHECK_DB)
                    {
                        jsonSuitcases_str = GetSuitcasesfromlocalDB(resp.data.polycommid, suitTableAdapter);
                    }
                    else
                        if (CHECK_DB) { jsonSuitcases_str = GetSuitcasesfromlocalDB(resp.data.polycommid, suittabAd); }

                    if (!String.IsNullOrEmpty(jsonSuitcases_str))
                    {
                        //Отправляем все данные что есть в локальной бд
                        Send_data(ID_Machina, jsonSuitcases_str, adress, "Suitcase");
                    }
                }
                else
                {
                    if (!CHECK_DB)
                    {
                        // если пришли не нули, то проверяем есть ли данная цепочка идентификаторов в локальной БД 
                        if ((int)suitTableAdapter.GetValidData(resp.data.polycommid, resp.data.totalid, resp.data.partialid) == 0)
                        {
                            // если нет, то шлем на сервер статуст 0
                            Check_status_send(ID_Machina, 0, adress);
                            eventLog1.WriteEntry("Data flow from this machine was stoped by machine by Suitcase", EventLogEntryType.Error, 1);
                        }
                        else
                        {
                            //если цепочка есть, то отправляем статус 1 на сервер и на основании ответа    
                            var status = Check_status_send(ID_Machina, 1, adress);
                            if (status)
                            {
                                //выбираем все записи из локальной БД больше 
                                if (last_local_ids.localid > resp.data.polycommid)
                                {
                                    string jsonSuitcases_str = GetSuitcasesfromlocalDB(resp.data.polycommid, suitTableAdapter);
                                    if (!String.IsNullOrEmpty(jsonSuitcases_str))
                                    {
                                        // Отправляем данные на сервер методом POST
                                        Send_data(ID_Machina, jsonSuitcases_str, adress, "Suitcase");
                                    }
                                }
                                else
                                {
                                    eventLog1.WriteEntry("Don`t have new suitcases", EventLogEntryType.Information, 2);
                                }


                            }
                            else
                            {
                                eventLog1.WriteEntry("Data flow from this machine was stoped by server", EventLogEntryType.Error, 3);
                            }

                        }
                    }
                    else
                    {
                        // если пришли не нули, то проверяем есть ли данная цепочка идентификаторов в локальной БД 
                        if ((int)suittabAd.GetValidData(resp.data.polycommid, resp.data.totalid, resp.data.partialid) == 0)
                        {
                            // если нет, то шлем на сервер статуст 0
                            Check_status_send(ID_Machina, 0, adress);
                            eventLog1.WriteEntry("Data flow from this machine was stoped by machine by Suitcase", EventLogEntryType.Error, 1);
                        }
                        else
                        {
                            //если цепочка есть, то отправляем статус 1 на сервер и на основании ответа    
                            var status = Check_status_send(ID_Machina, 1, adress);
                            if (status)
                            {
                                //выбираем все записи из локальной БД больше 
                                if (last_local_ids.localid > resp.data.polycommid)
                                {
                                    string jsonSuitcases_str = GetSuitcasesfromlocalDB(resp.data.polycommid, suittabAd);
                                    if (!String.IsNullOrEmpty(jsonSuitcases_str))
                                    {
                                        // Отправляем данные на сервер методом POST
                                        Send_data(ID_Machina, jsonSuitcases_str, adress, "Suitcase");
                                    }
                                }
                                else
                                {
                                    eventLog1.WriteEntry("Don`t have new suitcases", EventLogEntryType.Information, 2);
                                }


                            }
                            else
                            {
                                eventLog1.WriteEntry("Data flow from this machine was stoped by server", EventLogEntryType.Error, 3);
                            }

                        }
                    }

                }
            }
            catch(Exception exc)
            {
                eventLog1.WriteEntry(string.Format("Trouble: {0} ",exc.ToString()), EventLogEntryType.Error, 3);
            }

        }

        /// <summary>
        /// Обработка таблицы алармов
        /// </summary>
        private void AlarmProcess()
        {
            if (!CHECK_DB)
            {
                //1. Получаем последний ID из локальной БД
                var last_local_ID = alrmTableAdapter.GetLastIDList();

                //2. Получаем последний ID из большой базы
                var last_outer_ID = GetLastIDFromBIGDB(ID_Machina, adress, "Allarmi");
                ServerSuitResponse resp = JsonConvert.DeserializeObject<ServerSuitResponse>(last_outer_ID);

                int local_polyciomid = (int)last_local_ID.Rows[0][0];
                int local_total_suit = (int)last_local_ID.Rows[0][1];

                if (resp.data.polycommid == 0 && resp.data.total == 0)// если сервер вернул нули, то есть в БД еще нед упаково от данной машины
                {
                    string jsonAlarms_str = GetAlarmifromlocalDB(resp.data.polycommid, alrmTableAdapter);
                    if (!String.IsNullOrEmpty(jsonAlarms_str))
                    {
                        //Отправляем все данные что есть в локальной бд
                        Send_data(ID_Machina, jsonAlarms_str, adress, "Allarmi");
                    }
                }
                else
                {
                    // если пришли не нули, то проверяем есть ли данная цепочка идентификаторов в локальной БД 
                    if ((int)alrmTableAdapter.CheckAlarms(resp.data.polycommid, (int)resp.data.total) == 0)
                    {
                        Check_status_send(ID_Machina, 0, adress);
                        eventLog1.WriteEntry("Data flow from this machine was stoped by machine by alarms", EventLogEntryType.Error, 1);
                    }
                    else
                    {
                        //если цепочка есть, то отправляем статус 1 на сервер и на основании ответа    
                        var status = Check_status_send(ID_Machina, 1, adress);
                        if (status)
                        {
                            //выбираем все записи из локальной БД больше 
                            if (local_polyciomid > resp.data.polycommid)
                            {
                                string jsonAlarms_str = GetAlarmifromlocalDB(resp.data.polycommid, alrmTableAdapter);
                                if (!String.IsNullOrEmpty(jsonAlarms_str))
                                {
                                    //Отправляем все данные что есть в локальной бд
                                    Send_data(ID_Machina, jsonAlarms_str, adress, "Allarmi");
                                }
                            }
                            else
                            {
                                eventLog1.WriteEntry("Don`t have new alarms", EventLogEntryType.Information, 2);
                            }


                        }
                        else
                        {
                            eventLog1.WriteEntry("Data flow from this machine was stoped by server", EventLogEntryType.Error, 3);
                        }

                    }
                }
            }
            else
            {
                //Обработка БД Packfly
                //1. Получаем последний ID из локальной БД
                var last_local_ID = alarmtabAd.GetLastIDList();

                //2. Получаем последний ID из большой базы
                var last_outer_ID = GetLastIDFromBIGDB(ID_Machina, adress, "Allarmi");
                ServerSuitResponse resp = JsonConvert.DeserializeObject<ServerSuitResponse>(last_outer_ID);

                int local_polyciomid = (int)last_local_ID.Rows[0][0];
                // int local_total_suit = (int)last_local_ID.Rows[0][1];

                if (resp.data.polycommid == 0 )// если сервер вернул нули, то есть в БД еще нед упаково от данной машины
                {
                    string jsonAlarms_str = GetAlarmifromlocalDB(resp.data.polycommid, alarmtabAd);
                    if (!String.IsNullOrEmpty(jsonAlarms_str))
                    {
                        //Отправляем все данные что есть в локальной бд
                        Send_data(ID_Machina, jsonAlarms_str, adress, "Allarmi");
                    }
                }
                else
                {
                    ///!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!ИСПРАВИТЬ!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
                    // если пришли не нули, то проверяем есть ли данная цепочка идентификаторов в локальной БД 
                    if ((int)alarmtabAd.CheckAlarms(resp.data.polycommid) == 0)
                    {
                        Check_status_send(ID_Machina, 0, adress);
                        eventLog1.WriteEntry("Data flow from this machine was stoped by machine by alarms", EventLogEntryType.Error, eventId++);
                    }
                    else
                    {
                        //если цепочка есть, то отправляем статус 1 на сервер и на основании ответа    
                        var status = Check_status_send(ID_Machina, 1, adress);
                        if (status)
                        {
                            //выбираем все записи из локальной БД больше 
                            if (local_polyciomid > resp.data.polycommid)
                            {
                                string jsonAlarms_str = GetAlarmifromlocalDB(resp.data.polycommid, alarmtabAd);
                                if (!String.IsNullOrEmpty(jsonAlarms_str))
                                {
                                    //Отправляем все данные что есть в локальной бд
                                    Send_data(ID_Machina, jsonAlarms_str, adress, "Allarmi");
                                }
                            }
                            else
                            {
                                eventLog1.WriteEntry("Don`t have new alarms", EventLogEntryType.Information, 2);
                            }


                        }
                        else
                        {
                            eventLog1.WriteEntry("Data flow from this machine was stoped by server", EventLogEntryType.Error, 3);
                        }

                    }
                }
            }

            

        }
            /// </summary>
            private void Timer_Elapsed(object sender, ElapsedEventArgs e)

            {
                SuitcaseProcess();
                AlarmProcess();
                Send_sate_of_service(ID_Machina, adress, 3);

            }
        /// <summary>
        /// Types of state:
        /// 1 - service start
        /// 2 - service shutdown
        /// 3 - service all ok
        /// </summary>
        protected override void OnStop()
        {
            Send_sate_of_service(ID_Machina, adress, 2);
            eventLog1.WriteEntry("In OnStop.");
        }
        

    }
    
}
