using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Net;
using Newtonsoft.Json;
using System.Data;
using System.Diagnostics;
using Microsoft.Win32;
using System.Configuration;
using System.Data.OleDb;
namespace PNFService
{
   
    public partial class Service1 
    {
        /// <summary>
        /// класс для обработки результата от рута упаковок
        /// </summary>

        class ServerSuitResponse
        {
            public int responseCode { get; set; }
            public string responseMessage { get; set; }
            public DataSuitResponse data { get; set; }
            
        }
        /// <summary>
        /// подкласс для обработки результата от рута упаковок
        /// </summary>
        class DataSuitResponse
        {
            public int polycommid { get; set; }
            public int totalid { get; set; }
            public int partialid { get; set; }
            public int total { get; set; }
            public int polycomm_id { get; set; }
            public bool status { get; set; }
            public string db_pass { get; set; }

        }
        

        /// <summary>
        /// Метод преобразования таблицы к строке в формате JSON
        /// </summary>
        /// <param name="table">DataTable</param>
        /// <returns></returns>
        /// 


        public string DataTableToJSONWithJSONNet(DataTable table)
        {
            string JSONString = string.Empty;
            JSONString = JsonConvert.SerializeObject(table);
            return JSONString;
        }
        public void SetSettings(string ID_machine)
        {
            

            RegistryKey adaskey = Registry.LocalMachine.OpenSubKey(lsbkey, false);
            try
            {
                adaskey.SetValue("ID_Machine", ID_machine,RegistryValueKind.String);
            }
            catch (NullReferenceException ne)
            {

                eventLog1.WriteEntry("Some trouble "+ne.Message, EventLogEntryType.Error, 4);
            }
        }
        /// <summary>
        /// Поулчение настроек из реестра
        /// HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\services\PNFService
        /// </summary>
        public void GetSettings()
        {
            //string lsbkey = @"SYSTEM\CurrentControlSet\services\PNFService";

            try

            {
                
                RegistryKey adaskey = Registry.LocalMachine.OpenSubKey(lsbkey, false);
                object regurl = adaskey.GetValue("URL");
                object regdev_id = adaskey.GetValue("ID_Machine");
                adress = regurl.ToString();
                ID_Machina = regdev_id.ToString();
                eventLog1.WriteEntry(adress + " " + ID_Machina, EventLogEntryType.Information, 2000);
            }
            catch (NullReferenceException ne)
            {
                adress = null;
                ID_Machina = null;
                eventLog1.WriteEntry(@"Aborting Service, URL or ID_Machine doesn't exist in"+
                    lsbkey + " " + ne.Message, EventLogEntryType.Error, 4);
                //status = 0;

            }
        }

        /// <summary>
        /// Проверка статуса разрешения отправки данных в большую БД
        /// </summary>
        /// <param name="machine_id"></param>
        /// <param name="status"></param>
        /// <param name="server_uri"></param>
        /// <returns></returns>
        public bool Check_status_send(string machine_id, int status, string server_uri)
        {
           
            //String username = "import";
            //String password = "136&Poly689";
            //String encoded = System.Convert.ToBase64String(System.Text.Encoding.GetEncoding("ISO-8859-1").GetBytes(username + ":" + password));
            //httpWebRequest.Headers.Add("Authorization", "Basic " + encoded);
            string return_var = string.Empty;
            StringBuilder strb = new StringBuilder();

            try
            {
                strb.AppendFormat("{0}/check?id={1}&status={2}", server_uri, machine_id, status);
                WebRequest request = WebRequest.Create(strb.ToString()); //
                WebResponse response = request.GetResponse();
                using (Stream stream = response.GetResponseStream())
                {
                    using (StreamReader reader = new StreamReader(stream))
                    {
                        return_var = reader.ReadToEnd();
                    }
                }
                response.Close();
            }
            catch (WebException exc)
            {
                eventLog1.WriteEntry(exc.Message, EventLogEntryType.Error,8);
            }
            if (return_var == String.Empty)
            {
                eventLog1.WriteEntry("Server was not responded status", EventLogEntryType.Error, 7);
            }
            else
            {
                eventLog1.WriteEntry("All ok " + return_var, EventLogEntryType.SuccessAudit, 0);
            }
            ServerSuitResponse resp = JsonConvert.DeserializeObject<ServerSuitResponse>(return_var);
            return resp.data.status;

        }
        /// <summary>
        /// Метод отправки данных на сревер PNF. Реализует метод POST на endpiont сервера
        /// </summary>
        /// <param name="json"></param>
        public void Send_data(string Id ,string json, string server_uri, string type)
        {
            try
            {
                var httpWebRequest = (HttpWebRequest)WebRequest.Create(string.Format("{0}",server_uri));//
                httpWebRequest.ContentType = "application/json";
                httpWebRequest.Method = "POST";
                
                //String username = "import";
                //String password = "136&Poly689";
                //String encoded = System.Convert.ToBase64String(System.Text.Encoding.GetEncoding("ISO-8859-1").GetBytes(username + ":" + password));
                //httpWebRequest.Headers.Add("Authorization", "Basic " + encoded);

                using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
                {
                   
                    json = "{" + string.Format("\"machineId\": \"{2}\", \"dataType\": \"{1}\", \"records\": {0}", json, type, Id) + "}";
                    streamWriter.Write(json);
                    
                }
               
                var httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
                using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
                {
                    var result = streamReader.ReadToEnd();
                    eventLog1.WriteEntry(result);
                }
            }
            catch (Exception exc)
            {
                eventLog1.WriteEntry("Send data Exeption: " + exc.ToString(), EventLogEntryType.Error, 6);
            }
        }
       /// <summary>
       /// State of PNF srvice. Booting information, stopped information to server.
       /// </summary>
       /// <param name="machine_id"></param>
       /// <param name="address"></param>
       /// <param name="state"></param>
        public void Send_sate_of_service(string machine_id,string address, int state )
        {
            string return_var = "";
            StringBuilder strb = new StringBuilder();

            try
            {
                strb.AppendFormat("{0}/state?id={1}&stateval={2}", address, machine_id, state);
                WebRequest request = WebRequest.Create(strb.ToString()); //
                WebResponse response = request.GetResponse();
                using (Stream stream = response.GetResponseStream())
                {
                    using (StreamReader reader = new StreamReader(stream))
                    {
                        return_var = reader.ReadToEnd();
                    }
                }
                response.Close();
                if (return_var == String.Empty)
                {
                    eventLog1.WriteEntry("Server dont respond", EventLogEntryType.Error, 5);
                }
                else
                {
                    eventLog1.WriteEntry("All ok " + return_var, EventLogEntryType.Information, 0);
                }
            }
            catch (WebException exc)
            {
                eventLog1.WriteEntry(exc.Message, EventLogEntryType.Error, 9);
            }
            

        }
        /// <summary>
        /// Запрос последней записи об упаковке на сервере
        /// </summary>
        /// <param name="machine_id"></param>
        /// <returns></returns>
        public string GetLastIDFromBIGDB(string machine_id, string address, string tb_name)
        {
            string return_var = string.Empty;
            StringBuilder strb = new StringBuilder();

            try
            {
                strb.AppendFormat("{0}?id={1}&tb_name={2}", address, machine_id, tb_name);
                WebRequest request = WebRequest.Create(strb.ToString()); //
                WebResponse response = request.GetResponse();
                using (Stream stream = response.GetResponseStream())
                {
                    using (StreamReader reader = new StreamReader(stream))
                    {
                        return_var = reader.ReadToEnd();
                    }
                }
                response.Close();
            }
            catch (WebException exc)
            {
                eventLog1.WriteEntry(exc.Message);
            }
            if (return_var == String.Empty)
            {
                eventLog1.WriteEntry("Server was not recived last list of IDs",EventLogEntryType.Error, 5);
            }
            else
            {
                eventLog1.WriteEntry("All ok " + return_var, EventLogEntryType.SuccessAudit, 0);
            }

            return return_var;
        }
        /// <summary>
        /// Метод получения записей из таблицы Suitcase локальной базы данных Polycomm
        /// </summary>
        /// <param name="local_id">ID записи, больше котрой надо верунть строки из базы</param>
        /// <returns></returns>
        public string GetSuitcasesfromlocalDB(int local_id, PolycommDataSetTableAdapters.SuitcaseTableAdapter ta)
        {

            var table = ta.GetByID(local_id);
            eventLog1.WriteEntry(string.Format("Выборка размер: {0}", table.Rows.Count), EventLogEntryType.SuccessAudit, 11);
            var out_str = DataTableToJSONWithJSONNet(table);

            return out_str;
        }
        /// <summary>
        /// Метод получения записей из таблицы Suitcase локальной базы данных Packflay
        /// </summary>
        /// <param name="local_id">ID записи, больше котрой надо верунть строки из базы</param>
        /// <returns>String JSON, таблица представленная в формате JSON</returns>
        public string GetSuitcasesfromlocalDB(int local_id, PackflyDataSetTableAdapters.SuitcaseTableAdapter ta)
        {

            var table = ta.GetByID(local_id);
            var out_str = DataTableToJSONWithJSONNet(table);

            return out_str;
        }
        /// <summary>
        /// Метод получения записей из таблицы Alarm локальной базы данных Polycomm
        /// </summary>
        /// <param name="local_id">ID записи, больше котрой надо верунть строки из базы</param>
        /// <returns></returns>
        public string GetAlarmifromlocalDB(int local_id, PolycommDataSetTableAdapters.AllarmiTableAdapter ta)
        {

            var table = ta.GetByID(local_id);
            var out_str = DataTableToJSONWithJSONNet(table);
            return out_str;
        }
        /// <summary>
        /// Метод получения записей из таблицы Alarm локальной базы данных Packfly
        /// </summary>
        /// <param name="local_id">ID записи, больше котрой надо верунть строки из базы</param>
        /// <returns></returns>

        public string GetAlarmifromlocalDB(int local_id, PackflyDataSetTableAdapters.AllarmiTableAdapter ta)
        {

            var table = ta.GetByID(local_id);
            var out_str = DataTableToJSONWithJSONNet(table);
            return out_str;
        }

        public string GetCurrentVersion()
        {
            var file_name = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
            FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(file_name);
            String encoded = System.Convert.ToBase64String(System.Text.Encoding.GetEncoding("ISO-8859-1")
                                                            .GetBytes(fvi.CompanyName + ":" + fvi.FileVersion));
            return encoded;
        }
        /// <summary>
        /// Запрос пароля к БД
        /// </summary>
        /// <returns></returns>
        public string GetDBPass(string address_,string machine_id)
        {
            string return_var = "";
            StringBuilder strb = new StringBuilder();
            try
            {
                strb.AppendFormat("{0}/db?id={1}", address_, machine_id);
                eventLog1.WriteEntry(strb.ToString(), EventLogEntryType.SuccessAudit, 408);
                WebRequest request = WebRequest.Create(strb.ToString()); //
                WebResponse response = request.GetResponse();
                using (Stream stream = response.GetResponseStream())
                {
                    using (StreamReader reader = new StreamReader(stream))
                    {
                        return_var = reader.ReadToEnd();
                    }
                }
                response.Close();
                if (return_var== String.Empty)
                {
                    eventLog1.WriteEntry("Server does not recived parametrs for database", EventLogEntryType.Error, 5);
                }
                else
                {
                    String encoded = Convert.ToBase64String(Encoding.GetEncoding(65001)
                                                                .GetBytes(return_var));
                    eventLog1.WriteEntry(encoded, EventLogEntryType.SuccessAudit, 0);
                }
            }
            catch (Exception exc)
            {
                eventLog1.WriteEntry(exc.Message,EventLogEntryType.Warning,22);
            }
           
            ServerSuitResponse resp = JsonConvert.DeserializeObject<ServerSuitResponse>(return_var);
            return resp.data.db_pass;

           
        }


    }
}
