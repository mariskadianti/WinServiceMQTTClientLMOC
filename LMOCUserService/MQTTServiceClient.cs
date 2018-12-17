using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;


namespace LMOCUserService
{
    public partial class MQTTServiceClient : ServiceBase
    {
        public class UserStatus
        {
            public string UserId { get; set; }
            public string Status { get; set; }
            public string MacAddress { get; set; }
        }

        public class ServiceLoginStatus
        {
            public string user_id { get; set; }
            public int status { get; set; }
        }

        public class ResultMessage
        {
            public string result { get; set; }
            public string message { get; set; }
        }

        public MQTTServiceClient()
        {
            InitializeComponent();
        }

        public void OnDebug()
        {
            OnStart(null);
        }

        private System.Threading.Thread _thread;

        protected override void OnStart(string[] args)
        {
            try
            {

                _thread = new System.Threading.Thread(DoWork);

                // Start the thread.
                _thread.Start();
                
            }
            catch (Exception ex){
                StreamWriter sw = new StreamWriter(AppDomain.CurrentDomain.BaseDirectory + "serviceLogs.txt", true);
                sw.WriteLine(DateTime.Now.ToString() + " " + ex.Message, EventLogEntryType.Error);
                sw.Flush();
                sw.Close();
                
            }
        }

        private void DoWork()
        {            
            StreamWriter sw = new StreamWriter(AppDomain.CurrentDomain.BaseDirectory + "serviceLogs.txt", true);
            sw.WriteLine(DateTime.Now.ToString() + " " + "ServiceStarted");
            sw.Flush();
            //sw.Close();

            string mqttServer = ConfigurationManager.AppSettings["MQTTServer"].ToString();
            MqttClient client = new MqttClient(mqttServer);
            try
            {
                byte code = client.Connect(Guid.NewGuid().ToString(), "vsnbroker", "Password1!");
            }
            catch {
                sw.WriteLine(DateTime.Now.ToString() + " " + "Broker Disconnect");
                sw.Flush();
            } 
            Console.WriteLine("Connecting To " + mqttServer);
            if (client.IsConnected)
            {
                Console.WriteLine("Client connection success");
            }
            else
            {
                Console.WriteLine("Client connection failed");
            }

            string topic = ConfigurationManager.AppSettings["TopicSubscribe"].ToString();
            sw.WriteLine(DateTime.Now.ToString() + " " + "subscribe " + topic);
            sw.Flush();
            client.MqttMsgPublishReceived += Client_MqttMsgPublishReceived;
            ushort msgId = client.Subscribe(new string[] { topic },
                 new byte[] { MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE });
            Console.WriteLine("Connect " + topic);
            //StreamWriter sw = new StreamWriter(AppDomain.CurrentDomain.BaseDirectory + "serviceLogs.txt", true);


            sw.WriteLine(DateTime.Now.ToString() + " " + "Connected To " + mqttServer);
            sw.Flush();
            sw.Close();
        }

        private static void Client_MqttMsgPublishReceived(object sender, MqttMsgPublishEventArgs e)
        {
            //update status DB
            Console.WriteLine("Received Message");
            Console.WriteLine("Topic: " + e.Topic);
            string getTopic = Encoding.UTF8.GetString(e.Message);
            string UserId = getTopic.Split(',')[0];
            string Status = getTopic.Split(',')[1];
            string MacAddress = Status == "online" ? getTopic.Split(',')[2] : Status == "offline"? " "  : getTopic.Split(',')[2];
            Console.WriteLine("UserId: " + UserId + " - " + Status + " - " + MacAddress);
            StreamWriter sw = new StreamWriter(AppDomain.CurrentDomain.BaseDirectory + "serviceLogs.txt", true);
            sw.WriteLine(DateTime.Now.ToString() + " " + "UserId: " + UserId + " - " + Status + " - " + MacAddress);
            sw.Flush();
            sw.Close();
            try
            {
                if (Status == "login")
                {
                    int stat = Convert.ToInt32(getTopic.Split(',')[2]);
                    RunAsyncLogin(UserId, stat).Wait();
                }
                else
                {
                    RunAsync(UserId, Status, MacAddress).Wait();
                }
                
            }
            catch (Exception ex)
            {
                sw.WriteLine(DateTime.Now.ToString() + " - " + ex.ToString());
                sw.Flush();
                sw.Close();
            }
        }

        static async Task<ResultMessage> UpdateUserStatusAsync(UserStatus UserStatus)
        {
            string apiurl = ConfigurationManager.AppSettings["APIURL"].ToString();
            var client = new HttpClient();
            client.BaseAddress = new Uri(apiurl);
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
            //Console.WriteLine("testtt");
            HttpResponseMessage response = await client.PostAsJsonAsync(
                $"api/Transaction?updateStatusUser", UserStatus);
            Console.WriteLine("User Status Updated Successfully.");
            StreamWriter sw = new StreamWriter(AppDomain.CurrentDomain.BaseDirectory + "serviceLogs.txt", true);
            sw.WriteLine(DateTime.Now.ToString() + " Update Status Success");
            sw.Flush();
            sw.Close();
            // Deserialize the updated product from the response body.
            ResultMessage result = new ResultMessage();
            result = await response.Content.ReadAsAsync<ResultMessage>();
            return result;
        }
        static async Task RunAsync(string UserId, string Status, string MacAddress)
        {
            // Update port # in the following line.            
            UserStatus UserStatus = new UserStatus
            {
                UserId = UserId,
                Status = Status,
                MacAddress = MacAddress
            };

            Console.WriteLine("Updating status user online...");
            await UpdateUserStatusAsync(UserStatus);

        }

        static async Task RunAsyncLogin(string UserId, int Status)
        {
            // Update port # in the following line.            
            ServiceLoginStatus UserStatusLogin = new ServiceLoginStatus
            {
                user_id = UserId,
                status = Status
            };

            Console.WriteLine("Updating status user login...");
            await UpdateUserStatusLoginAsync(UserStatusLogin);

        }
        static async Task<ResultMessage> UpdateUserStatusLoginAsync(ServiceLoginStatus UserStatusLogin)
        {
            string apiurl = ConfigurationManager.AppSettings["APIURL"].ToString();
            var client = new HttpClient();
            client.BaseAddress = new Uri(apiurl);
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
            //Console.WriteLine("testtt");
            HttpResponseMessage response = await client.PostAsJsonAsync(
                $"api/Transaction?updateUserIsLoged", UserStatusLogin);
            Console.WriteLine("User Status Updated Successfully.");
            StreamWriter sw = new StreamWriter(AppDomain.CurrentDomain.BaseDirectory + "serviceLogs.txt", true);
            sw.WriteLine(DateTime.Now.ToString() + " Update Status Success");
            sw.Flush();
            sw.Close();
            // Deserialize the updated product from the response body.
            ResultMessage result = new ResultMessage();
            result = await response.Content.ReadAsAsync<ResultMessage>();
            return result;
        }

        protected override void OnStop()
        {
            StreamWriter sw = new StreamWriter(AppDomain.CurrentDomain.BaseDirectory + "serviceLogs.txt", true);
            sw.WriteLine(DateTime.Now.ToString() + " - " + "Service stopped");
            sw.Flush();
            sw.Close();
        }
    }
}
